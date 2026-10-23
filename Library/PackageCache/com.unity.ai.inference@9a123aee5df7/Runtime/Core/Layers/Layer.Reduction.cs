using System;

namespace Unity.InferenceEngine.Layers
{
    /// <summary>
    /// Represents a `ReduceL1` reduction layer along the given axes: f(x1, x2 ... xn) = |x1| + |x2| + ... + |xn|.
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceL1 : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            var reduceOverEmptySet = noopWithEmptyAxes && (axes == null || axes.Length == 0);
            var shape = reduceOverEmptySet ? X.shape : X.shape.Reduce(axes, keepdims);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
            {
                ctx.backend.MemClear(O);
                return;
            }

            if (reduceOverEmptySet)
            {
                if (X is Tensor<int>)
                    ctx.backend.Abs(X as Tensor<int>, O as Tensor<int>);
                else
                    ctx.backend.Abs(X as Tensor<float>, O as Tensor<float>);
            }
            else
            {
                if (X is Tensor<int>)
                    ctx.backend.ReduceL1(X as Tensor<int>, O as Tensor<int>, axes);
                else
                    ctx.backend.ReduceL1(X as Tensor<float>, O as Tensor<float>, axes);
            }
        }
    }

    /// <summary>
    /// Represents a `ReduceL2` reduction layer along the given axes: f(x1, x2 ... xn) = sqrt(x1² + x2² + ... + xn²).
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceL2 : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var axes = ctx.storage.GetInts(inputs[1], null);
            var reduceOverEmptySet = noopWithEmptyAxes && (axes == null || axes.Length == 0);
            var shape = reduceOverEmptySet ? X.shape : X.shape.Reduce(axes, keepdims);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
            {
                ctx.backend.MemClear(O);
                return;
            }

            if (reduceOverEmptySet)
                ctx.backend.Abs(X, O);
            else
                ctx.backend.ReduceL2(X, O, axes);
        }
    }

    /// <summary>
    /// Represents a `ReduceLogSum` reduction layer along the given axes: f(x1, x2 ... xn) = log(x1 + x2 + ... + xn).
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceLogSum : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var axes = ctx.storage.GetInts(inputs[1], null);
            var reduceOverEmptySet = noopWithEmptyAxes && (axes == null || axes.Length == 0);
            var shape = reduceOverEmptySet ? X.shape : X.shape.Reduce(axes, keepdims);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
            {
                ctx.backend.MemSet(O, float.NegativeInfinity);
                return;
            }

            if (reduceOverEmptySet)
                ctx.backend.Log(X, O);
            else
                ctx.backend.ReduceLogSum(X, O, axes);
        }
    }

    /// <summary>
    /// Represents a `ReduceLogSumExp` reduction layer along the given axes: f(x1, x2 ... xn) = log(e^x1 + e^x2 + ... + e^xn).
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceLogSumExp : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
                ctx.backend.MemSet(O, float.NegativeInfinity);
            else
                ctx.backend.ReduceLogSumExp(X, O, axes);
        }
    }

    /// <summary>
    /// Represents a `ReduceMax` reduction layer along the given axes: f(x1, x2 ... xn) = max(x1, x2, ... , xn).
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceMax : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<int>, int.MinValue);
                else
                    ctx.backend.ReduceMax(X as Tensor<int>, O as Tensor<int>, axes);
            }
            else
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<float>, float.NegativeInfinity);
                else
                    ctx.backend.ReduceMax(X as Tensor<float>, O as Tensor<float>, axes);
            }
        }
    }

    /// <summary>
    /// Represents a `ReduceMean` reduction layer along the given axes: f(x1, x2 ... xn) = (x1 + x2 + ... + xn) / n.
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceMean : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
                ctx.backend.MemClear(O);
            else
                ctx.backend.ReduceMean(X, O, axes);
        }
    }

    /// <summary>
    /// Represents a `ReduceMin` reduction layer along the given axes: f(x1, x2 ... xn) = min(x1, x2, ... , xn).
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceMin : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<int>, int.MaxValue);
                else
                    ctx.backend.ReduceMin(X as Tensor<int>, O as Tensor<int>, axes);
            }
            else
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<float>, float.PositiveInfinity);
                else
                    ctx.backend.ReduceMin(X as Tensor<float>, O as Tensor<float>, axes);
            }
        }
    }

    /// <summary>
    /// Represents a `ReduceProd` reduction layer along the given axes: f(x1, x2 ... xn) = x1 * x2 * ... * xn.
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceProd : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X is Tensor<int>)
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<int>, 1);
                else
                    ctx.backend.ReduceProd(X as Tensor<int>, O as Tensor<int>, axes);
            }
            else
            {
                if (X.shape.HasZeroDims())
                    ctx.backend.MemSet(O as Tensor<float>, 1);
                else
                    ctx.backend.ReduceProd(X as Tensor<float>, O as Tensor<float>, axes);
            }
        }
    }

    /// <summary>
    /// Represents a `ReduceSum` reduction layer along the given axes: f(x1, x2 ... xn) = x1 + x2 + ... + xn.
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceSum : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, X.dataType, ctx.backend.backendType);
                ctx.backend.MemCopy(X, copyX);
                return;
            }
            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
            {
                ctx.backend.MemClear(O);
                return;
            }
            if (X is Tensor<int>)
                ctx.backend.ReduceSum(X as Tensor<int>, O as Tensor<int>, axes);
            else
                ctx.backend.ReduceSum(X as Tensor<float>, O as Tensor<float>, axes);
        }
    }

    /// <summary>
    /// Represents a `ReduceSumSquare` reduction layer along the given axes: f(x1, x2 ... xn) = x1² + x2² + ... + xn².
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceSumSquare : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]);
            var axes = ctx.storage.GetInts(inputs[1], null);
            var reduceOverEmptySet = noopWithEmptyAxes && (axes == null || axes.Length == 0);
            var shape = reduceOverEmptySet ? X.shape : X.shape.Reduce(axes, keepdims);

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], shape, X.dataType, ctx.backend.backendType);
            if (O.shape.HasZeroDims())
                return;
            if (X.shape.HasZeroDims())
            {
                ctx.backend.MemClear(O);
                return;
            }

            if (reduceOverEmptySet)
            {
                if (X is Tensor<int>)
                    ctx.backend.Square(X as Tensor<int>, O as Tensor<int>);
                else
                    ctx.backend.Square(X as Tensor<float>, O as Tensor<float>);
            }
            else
            {
                if (X is Tensor<int>)
                    ctx.backend.ReduceSumSquare(X as Tensor<int>, O as Tensor<int>, axes);
                else
                    ctx.backend.ReduceSumSquare(X as Tensor<float>, O as Tensor<float>, axes);
            }
        }
    }

    /// <summary>
    /// Represents a `ReduceVariance` reduction layer along the given axes: f(x1, x2, ..., xn) = ((x1 - μ)**2 + (x2 - μ)**2 + ... + (xn - μ)**2) / N where μ = (x1 + x2 + ... + xn) / N (use 1/(N-1) instead of 1/N for unbiased variance)
    /// </summary>
    [Operator(category = "Reduction")]
    [Inputs(names = new[] { "data", "axes" }, inputCPURead = new[] { 1 })]
    partial class ReduceVariance : Layer
    {
        public bool keepdims;
        public bool noopWithEmptyAxes;
        public float correction;

        internal static PartialTensor InferPartial(PartialTensor data, PartialTensor axes, bool keepdims, bool noopWithEmptyAxes, float correction)
        {
            return PartialTensor.Reduce(data, axes, keepdims, noopWithEmptyAxes);
        }

        internal override void Execute(ExecutionContext ctx)
        {
            var X = ctx.storage.GetTensor(inputs[0]) as Tensor<float>;
            var axes = ctx.storage.GetInts(inputs[1], null);
            if (noopWithEmptyAxes && (axes == null || axes.Length == 0))
            {
                var copyX = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
                ctx.backend.MemCopy(X, copyX);
                return;
            }

            var O = ctx.storage.AllocateTensorAndStore(outputs[0], X.shape.Reduce(axes, keepdims), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            if (O.shape.HasZeroDims())
                return;

            var N = X.shape.length / O.shape.length;
            var divisor = N - correction;
            if (X.shape.HasZeroDims() || divisor <= 0)
            {
                ctx.backend.MemClear(O);
                return;
            }

            var mean = ctx.storage.AllocateTensor(X.shape.Reduce(axes, true), DataType.Float, ctx.backend.backendType) as Tensor<float>;
            var subMean = ctx.storage.AllocateTensor(X.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;
            var reduceSumSquare = ctx.storage.AllocateTensor(O.shape, DataType.Float, ctx.backend.backendType) as Tensor<float>;

            ctx.backend.ReduceMean(X, mean, axes);
            ctx.backend.Sub(X, mean, subMean);
            ctx.backend.ReduceSumSquare(subMean, reduceSumSquare, axes);
            ctx.backend.ScalarMad(reduceSumSquare, O, 1 / divisor, 0);

            ctx.storage.Dispose(mean);
            ctx.storage.Dispose(subMean);
            ctx.storage.Dispose(reduceSumSquare);
        }
    }
}
