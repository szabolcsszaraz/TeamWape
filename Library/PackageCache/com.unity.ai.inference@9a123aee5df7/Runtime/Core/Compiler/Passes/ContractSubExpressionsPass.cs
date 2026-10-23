using System;
using Unity.InferenceEngine.Graph;
using UnityEngine;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    /// <summary>
    /// Uses subgraph matching to replace the expanded graphs that come out of the onnx converter back down to individual ops.
    /// </summary>
    class ContractSubExpressionPass : GraphPass
    {
        static GraphModule CreateGraphModule(Func<GraphModule, Node[], Node> func, DataType[] dataTypes)
        {
            var gm = new GraphModule();
            var inputs = new Node[dataTypes.Length];
            for (var i = 0; i < dataTypes.Length; i++)
                inputs[i] = gm.Input(i.ToString(), dataTypes[i], DynamicTensorShape.DynamicRank);
            var y = func(gm, inputs);
            gm.Outputs(new[] { "output" }, new[] { y });
            return gm;
        }

        static GraphModule CreateGraphModule(Func<GraphModule, Node, Node> func) => CreateGraphModule((gm, inputs) => func(gm, inputs[0]), new[] { DataType.Float });

        public override void Run(GraphModule gm)
        {
            // rsqrt
            var rsqrtPattern = CreateGraphModule((g, x) => g.Reciprocal(g.Sqrt(x)));
            SubgraphRewriter.ReplacePattern(gm, rsqrtPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, x) => g.Rsqrt(x)).graph);

            // not equal
            var notEqualPattern = CreateGraphModule((g, inputs) => g.Not(g.Equal(inputs[0], inputs[1])), new[] { DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, notEqualPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, inputs) => g.NotEqual(inputs[0], inputs[1]), new[] { DataType.Float, DataType.Float }).graph);

            // atan2
            var atan2Pattern = CreateGraphModule((g, inputs) =>
            {
                var y = inputs[0];
                var x = inputs[1];
                var div = g.Div(y, x);
                var atan = g.Atan(div);
                var add = g.Add(atan, g.Constant(3.1415927410125732f));
                var sub = g.Sub(atan, g.Constant(3.1415927410125732f));
                var where = g.Where(g.Greater(y, g.Constant(0f)), add, sub);
                return g.Where(g.Less(x, g.Constant(0f)), where, atan);
            }, new[] { DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, atan2Pattern, replacementCallback: (_, _, _) => CreateGraphModule((g, inputs) => g.Atan2(inputs[0], inputs[1]), new[] { DataType.Float, DataType.Float }).graph);

            // floor div int
            var floorDivIntPattern = CreateGraphModule((g, inputs) =>
            {
                var x = inputs[0];
                var y = inputs[1];
                var xor = g.Xor(g.Less(x, g.Constant(0)), g.Less(y, g.Constant(0)));
                var condition = g.And(xor, g.NotEqual(g.Mod(x, y, false), g.Constant(0))); // not equal replacer has to run before this
                var div = g.Div(x, y);
                return g.Where(condition, g.Sub(div, g.Constant(1)), div);
            }, new[] { DataType.Int, DataType.Int });
            SubgraphRewriter.ReplacePattern(gm, floorDivIntPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, inputs) => g.FloorDiv(inputs[0], inputs[1]), new[] { DataType.Int, DataType.Int }).graph);

            // floor div float
            var floorDivFloatPattern = CreateGraphModule((g, inputs) => g.Floor(g.Div(inputs[0], inputs[1])), new[] { DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, floorDivFloatPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, inputs) => g.FloorDiv(inputs[0], inputs[1]), new[] { DataType.Float, DataType.Float }).graph);

            // trunc div float
            var truncDivFloatPattern = CreateGraphModule((g, inputs) => g.Cast(g.Cast(g.Div(inputs[0], inputs[1]), DataType.Int), DataType.Float), new[] { DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, truncDivFloatPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, inputs) => g.TruncDiv(inputs[0], inputs[1]), new[] { DataType.Float, DataType.Float }).graph);

            // swish
            var swishPattern = CreateGraphModule((g, x) => g.Mul(x, g.Sigmoid(x)));
            SubgraphRewriter.ReplacePattern(gm, swishPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, x) => g.Swish(x, 1.0f)).graph);

            // gelu
            var geluPattern = CreateGraphModule((g, x) => g.Mul(g.Mul(x, g.Add(g.Erf(g.Div(x, g.Constant(Mathf.Sqrt(2f)))), g.Constant(1f))), g.Constant(0.5f)));
            SubgraphRewriter.ReplacePattern(gm, geluPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, x) => g.Gelu(x)).graph);

            // gelu fast: y = 0.5x * (1 + tanh(sqrt(2/Pi) * (x + 0.044715 x^3)))
            var geluFastPattern = CreateGraphModule((g, x) =>
            {
                var y = g.Pow(x, g.Constant(3f));
                y = g.Mul(y, g.Constant(0.044714998453855515f));
                y = g.Add(x, y);
                y = g.Mul(y, g.Constant(0.7978845834732056f));
                y = g.Tanh(y);
                y = g.Add(y, g.Constant(1f));
                return g.Mul(g.Mul(x, g.Constant(0.5f)), y);
            });
            SubgraphRewriter.ReplacePattern(gm, geluFastPattern, replacementCallback: (_, _, _) => CreateGraphModule((g, x) => g.GeluFast(x)).graph);

            // layer normalization
            var layerNormalizationPattern = CreateGraphModule((g, inputs) =>
            {
                var x = inputs[0];
                var scale = inputs[1];
                var bias = inputs[2];
                var epsilon = inputs[3];
                var mean = g.ReduceMean(x, g.Constant(new[] { -1 }), true, false);
                var y = g.Sub(x, mean);
                var variance = g.ReduceMean(g.Pow(y, g.Constant(2.0f)), g.Constant(-1), true, false);
                var v = g.Div(y, g.Sqrt(g.Add(variance, epsilon)));
                return g.Add(g.Mul(v, scale), bias);
            }, new[] { DataType.Float, DataType.Float, DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, layerNormalizationPattern, replacementCallback: (match, _, _) =>
            {
                return CreateGraphModule((g, inputs) => g.LayerNormalization(inputs[0], inputs[1], inputs[2], match.placeholderNodes[3].partialTensor.Get<float>().value), new[] { DataType.Float, DataType.Float, DataType.Float, DataType.Float }).graph;
            });

            // RMS normalization
            var rmsNormalizationPattern = CreateGraphModule((g, inputs) =>
            {
                var x = inputs[0];
                var scale = inputs[1];
                var epsilon = inputs[2];
                var pow = g.Pow(x, g.Constant(2.0f));
                var reduceMean = g.ReduceMean(pow, g.Constant(new[] { -1 }), true, false);
                var add = g.Add(reduceMean, epsilon);
                var sqrt = g.Sqrt(add);
                var div = g.Div(g.Constant(1.0f), sqrt);
                var mul = g.Mul(x, div);
                return g.Mul(scale, mul);
            }, new[] { DataType.Float, DataType.Float, DataType.Float });
            SubgraphRewriter.ReplacePattern(gm, rmsNormalizationPattern, replacementCallback: (match, _, _) =>
            {
                return CreateGraphModule((g, inputs) => g.RMSNormalization(inputs[0], inputs[1], match.placeholderNodes[2].partialTensor.Get<float>().value), new[] { DataType.Float, DataType.Float, DataType.Float }).graph;
            });
        }
    }
}
