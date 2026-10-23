using System;
using System.Collections.Generic;
using System.Linq;
using TorchPt2;
using Unity.InferenceEngine.Graph;
using Argument = TorchPt2.Argument;
using Node = TorchPt2.Node;

namespace Unity.InferenceEngine.Editor.Torch
{
    static class TorchUtilities
    {
        public static DataType ScalarTypeToDataType(ScalarType scalarType)
        {
            return scalarType switch
            {
                ScalarType.HALF or ScalarType.FLOAT or ScalarType.DOUBLE or ScalarType.BFLOAT16 or ScalarType.FLOAT8E4M3FN or ScalarType.FLOAT8E5M2 => DataType.Float,
                ScalarType.BYTE or ScalarType.CHAR or ScalarType.SHORT or ScalarType.INT or ScalarType.LONG or ScalarType.BOOL or ScalarType.UINT16 => DataType.Int,
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    class TorchContext
    {
        public Graph.GraphModule gm = new();
        public Dictionary<string, Graph.Node> tensorNodes = new();
        public Dictionary<string, string> unsupportedTensorNodes = new();
        public Dictionary<string, Graph.Node> symIntNodes = new();
        public Dictionary<string, Graph.Node> symFloatNodes = new();
        public Dictionary<string, Graph.Node> symBoolNodes = new();
        public Dictionary<string, DynamicTensorDim> symbolicDims = new();
        public Dictionary<Graph.Node, ScalarType> scalarTypes = new();

        public void AddTensor(string name, Graph.Node node, ScalarType dtype)
        {
            scalarTypes[node] = dtype;
            tensorNodes[name] = node;
        }

        public void AddUnsupportedTensor(string name, string errorMessage)
        {
            unsupportedTensorNodes[name] = errorMessage;
        }

        public void AddSymInt(string name, Graph.Node node)
        {
            scalarTypes[node] = TorchPt2.ScalarType.LONG;
            symIntNodes[name] = node;
        }

        public void AddSymFloat(string name, Graph.Node node)
        {
            scalarTypes[node] = TorchPt2.ScalarType.FLOAT;
            symFloatNodes[name] = node;
        }

        public Graph.Node GetTensorNode(string name)
        {
            if (!tensorNodes.TryGetValue(name, out var node))
            {
                var message = unsupportedTensorNodes.TryGetValue(name, out var errMessage) ? errMessage : $"Tensor node not found for node \"{name}\"";
                throw new TorchImportException(message);
            }

            return node;
        }

        public Graph.Node GetSymIntNode(string name)
        {
            if (!symIntNodes.TryGetValue(name, out var node))
                throw new TorchImportException($"Symbolic int node not found for node \"{name}\"");
            return node;
        }

        public Graph.Node GetSymFloatNode(string name)
        {
            if (!symFloatNodes.TryGetValue(name, out var node))
                throw new TorchImportException($"Symbolic float node not found for node \"{name}\"");
            return node;
        }

        public Graph.Node GetSymBoolNode(string name)
        {
            if (!symBoolNodes.TryGetValue(name, out var node))
                throw new TorchImportException($"Symbolic bool node not found for node \"{name}\"");
            return node;
        }

        public DynamicTensorDim GetSymbolicDim(string expression)
        {
            if (symbolicDims.TryGetValue(expression, out var dim))
                return dim;
            dim = DynamicTensorDim.Param((byte)symbolicDims.Count);
            symbolicDims[expression] = dim;
            return dim;
        }

        public ScalarType ScalarType(Graph.Node node)
        {
            if (!scalarTypes.TryGetValue(node, out var scalarType))
                throw new TorchImportException($"Scalar type not found for node \"{node.name}\"");
            return scalarType;
        }
    }

    class ArgAccessor
    {
        TorchContext m_Ctx;
        readonly List<TorchArgument> m_Args;

        public ArgAccessor(TorchContext ctx, List<TorchArgument> args)
        {
            m_Ctx = ctx;
            m_Args = args;
        }

        public TorchArgument this[int index] => index < m_Args.Count ? m_Args[index] : new TorchArgument(m_Ctx, null);
    }

    class KeywordArgAccessor
    {
        TorchContext m_Ctx;
        readonly Dictionary<string, TorchArgument> m_Kwargs;

        public KeywordArgAccessor(TorchContext ctx, Dictionary<string, TorchArgument> kwargs)
        {
            m_Ctx = ctx;
            m_Kwargs = kwargs;
        }

        public TorchArgument this[string key] => m_Kwargs.GetValueOrDefault(key, new TorchArgument(m_Ctx, null));
    }

    class TorchNode
    {
        TorchContext m_Ctx;
        Node m_Node;
        ArgAccessor m_Args;
        KeywordArgAccessor m_Kwargs;

        public ArgAccessor Args => m_Args;
        public KeywordArgAccessor KeywordArgs => m_Kwargs;

        public TorchNode(TorchContext ctx, Node node)
        {
            m_Ctx = ctx;
            m_Node = node;
            var args = new List<TorchArgument>();
            var kwargs = new Dictionary<string, TorchArgument>();
            foreach (var namedArg in node.inputs)
            {
                if (namedArg.kind == ArgumentKind.POSITIONAL)
                {
                    args.Add(new TorchArgument(m_Ctx, namedArg.arg, target, namedArg.name));
                }
                else if (namedArg.kind == ArgumentKind.KEYWORD)
                {
                    kwargs[namedArg.name] = new TorchArgument(m_Ctx, namedArg.arg, target, namedArg.name);
                }
            }
            m_Args = new ArgAccessor(m_Ctx, args);
            m_Kwargs = new KeywordArgAccessor(m_Ctx, kwargs);
        }

        public string target => m_Node.target;

        public Argument GetOutput(int index)
        {
            return m_Node.outputs[index];
        }

        public bool HasNoOutputs => m_Node.outputs is null || m_Node.outputs.Count == 0;
    }

    class TorchArgument
    {
        readonly TorchContext m_Ctx;
        readonly Argument m_Argument;
        readonly string m_TargetName;
        readonly string m_ArgName;

        public TorchArgument(TorchContext ctx, Argument argument, string targetName = null, string argName = null)
        {
            m_Ctx = ctx;
            m_Argument = argument;
            m_TargetName = targetName;
            m_ArgName = argName;
        }

        bool IsNone => m_Argument is null || m_Argument.kind == "as_none";

        bool IsStridedLayout => m_Argument?.kind == "as_layout" && (Layout)m_Argument?.value == Layout.Strided;

        bool IsDeviceCpu => m_Argument?.kind == "as_device" && ((Device)m_Argument?.value).type == "cpu";

        public bool Ignore => IsNone || IsStridedLayout || IsDeviceCpu;

        public Graph.Node SymIntAsTensor(Graph.Node defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_int")
            {
                return m_Ctx.gm.Constant((int)m_Argument.value);
            }
            if (m_Argument.kind == "as_sym_int")
            {
                var symInt = (SymIntArgument)m_Argument.value;
                switch (symInt.kind)
                {
                    case "as_name":
                    {
                        var name = (string)symInt.value;
                        return m_Ctx.GetSymIntNode(name);
                    }
                    case "as_int":
                    {
                        return m_Ctx.gm.Constant((int)symInt.value);
                    }
                }
            }
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, sym int, or None, got \"{m_Argument.kind}\"");
        }

        public int SymIntAsInt()
        {
            if (m_Argument is null)
                throw new TorchArgumentImportException(m_TargetName, m_ArgName, "Error accessing sym int");
            if (m_Argument.kind == "as_int")
                return (int)m_Argument.value;
            if (m_Argument.kind == "as_sym_int")
            {
                var symInt = (SymIntArgument)m_Argument.value;
                if (symInt.kind == "as_name")
                    throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Symbolic value not supported for argument {(string)symInt.value}");
                return (int)symInt.value;
            }
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int or sym int, got \"{m_Argument.kind}\"");
        }

        public int[] SymIntsAsInts()
        {
            if (m_Argument is null)
                throw new TorchArgumentImportException(m_TargetName, m_ArgName, "Error accessing sym ints");
            if (m_Argument.kind == "as_ints")
            {
                var ints = (List<int>)m_Argument.value;
                return ints.ToArray();
            }
            if (m_Argument.kind == "as_sym_ints")
            {
                var symInts = (List<SymIntArgument>)m_Argument.value;
                if (symInts.Any(i => i.kind == "as_name"))
                    throw new TorchArgumentImportException(m_TargetName, m_ArgName, "Symbolic value not supported");
                return symInts.Select(i => (int)i.value).ToArray();
            }
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected ints or sym ints, got \"{m_Argument.kind}\"");
        }

        public Graph.Node SymIntsAsTensor(Graph.Node defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_ints")
            {
                var ints = (List<int>)m_Argument.value;
                var values = ints.ToArray();
                return m_Ctx.gm.Constant(values);
            }
            if (m_Argument.kind == "as_sym_ints")
            {
                var symInts = (List<SymIntArgument>)m_Argument.value;
                var concatNodes = new Graph.Node[symInts.Count];
                for (var i = 0; i < symInts.Count; i++)
                {
                    switch (symInts[i].kind)
                    {
                        case "as_name":
                        {
                            var name = (string)symInts[i].value;
                            concatNodes[i] = m_Ctx.gm.Unsqueeze(m_Ctx.GetSymIntNode(name), m_Ctx.gm.Constant(new[] { 0 }));
                            break;
                        }
                        case "as_int":
                        {
                            concatNodes[i] = m_Ctx.gm.Constant(new[] { (int)symInts[i].value });
                            break;
                        }
                    }
                }

                return m_Ctx.gm.Concat(concatNodes, 0);
            }
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected ints or sym ints, got \"{m_Argument.kind}\"");
        }

        public Graph.Node Tensor()
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return null;

            if (m_Argument.kind == "as_tensor")
            {
                var tensorName = ((TensorArgument)m_Argument.value).name;
                if (string.IsNullOrEmpty(tensorName))
                    return null;
                return m_Ctx.GetTensorNode(tensorName);
            }

            return ScalarAsTensor();
        }

        public Graph.Node[] Tensors()
        {
            if (m_Argument is null)
                throw new TorchArgumentImportException(m_TargetName, m_ArgName, "Error accessing tensors");
            if (m_Argument.kind == "as_tensors")
            {
                var tensorArgs = (List<TensorArgument>)m_Argument.value;
                var tensors = new Graph.Node[tensorArgs.Count];
                for (var i = 0; i < tensorArgs.Count; i++)
                {
                    var tensorName = tensorArgs[i].name;
                    if (string.IsNullOrEmpty(tensorName))
                        continue;
                    tensors[i] = m_Ctx.GetTensorNode(tensorName);
                }

                return tensors;
            }
            if (m_Argument.kind == "as_optional_tensors")
            {
                var optionalTensorArgs = (List<OptionalTensorArgument>)m_Argument.value;
                var tensors = new Graph.Node[optionalTensorArgs.Count];
                for (var i = 0; i < optionalTensorArgs.Count; i++)
                {
                    var optionalTensorArg = optionalTensorArgs[i];
                    if (optionalTensorArg.kind == "as_tensor")
                    {
                        var tensorName = ((TensorArgument)optionalTensorArg.value)?.name;
                        if (string.IsNullOrEmpty(tensorName))
                            continue;
                        tensors[i] = m_Ctx.GetTensorNode(tensorName);
                    }
                    else if (optionalTensorArg.kind == "as_none")
                    {
                        tensors[i] = null;
                    }
                }

                return tensors;
            }
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected tensors, got \"{m_Argument.kind}\"");
        }

        public Graph.Node ScalarAsTensor(int? defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue.HasValue ? m_Ctx.gm.Constant(defaultValue.Value) : null;
            if (m_Argument.kind == "as_int")
                return m_Ctx.gm.Constant((int)m_Argument.value);
            if (m_Argument.kind == "as_sym_int")
            {
                var symint = (SymIntArgument)m_Argument.value;
                if (symint.kind == "as_name")
                    return m_Ctx.GetSymIntNode((string)symint.value);
                if (symint.kind == "as_int")
                    return m_Ctx.gm.Constant((int)symint.value);
            }
            if (m_Argument.kind == "as_float")
                return m_Ctx.gm.Constant((float)m_Argument.value);
            if (m_Argument.kind == "as_sym_float")
            {
                var symFloat = (SymFloatArgument)m_Argument.value;
                if (symFloat.kind == "as_name")
                    return m_Ctx.GetSymFloatNode((string)symFloat.value);
                if (symFloat.kind == "as_float")
                    return m_Ctx.gm.Constant((float)symFloat.value);
            }
            if (m_Argument.kind == "as_bool")
                return m_Ctx.gm.Constant((bool)m_Argument.value ? 1 : 0);
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Could not get scalar as tensor for argument of type \"{m_Argument.kind}\"");
        }

        public ScalarType ScalarType()
        {
            if (m_Argument is null)
                throw new TorchArgumentImportException(m_TargetName, m_ArgName, "Could not get scalar type");
            return m_Argument.kind switch
            {
                "as_float" => TorchPt2.ScalarType.FLOAT,
                "as_int" => TorchPt2.ScalarType.LONG,
                "as_bool" => TorchPt2.ScalarType.BOOL,
                _ => throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, or bool, got \"{m_Argument.kind}\"")
            };
        }

        public int ScalarAsInt(int? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_int")
                return (int)m_Argument.value;
            if (m_Argument?.kind == "as_float")
                return (int)(float)m_Argument.value;
            if (m_Argument?.kind == "as_bool")
                return (bool)m_Argument.value ? 1 : 0;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, bool, or None, got \"{m_Argument?.kind}\"");
        }

        public float ScalarAsFloat(float? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_int")
                return (int)m_Argument.value;
            if (m_Argument?.kind == "as_float")
                return (float)m_Argument.value;
            if (m_Argument?.kind == "as_bool")
                return (bool)m_Argument.value ? 1.0f : 0.0f;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, bool, or None, got \"{m_Argument?.kind}\"");
        }

        public int ScalarAsBoolInt()
        {
            if (m_Argument?.kind == "as_int")
                return (int)m_Argument.value != 0 ? 1 : 0;
            if (m_Argument?.kind == "as_float")
                return (float)m_Argument.value != 0.0f ? 1 : 0;
            if (m_Argument?.kind == "as_bool")
                return (bool)m_Argument.value ? 1 : 0;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, or bool, got \"{m_Argument?.kind}\"");
        }

        public int ScalarAsUInt8()
        {
            if (m_Argument?.kind == "as_int")
                return (byte)(int)m_Argument.value;
            if (m_Argument?.kind == "as_float")
                return (byte)(float)m_Argument.value;
            if (m_Argument?.kind == "as_bool")
                return (bool)m_Argument.value ? 1 : 0;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, or bool, got \"{m_Argument?.kind}\"");
        }

        public int ScalarAsIntType(ScalarType scalarType)
        {
            return scalarType switch
            {
                TorchPt2.ScalarType.BOOL => ScalarAsBoolInt(),
                TorchPt2.ScalarType.BYTE => ScalarAsUInt8(),
                _ => ScalarAsInt()
            };
        }

        public bool Bool(bool? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_bool")
                return (bool)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected bool or None, got \"{m_Argument?.kind}\"");
        }

        public float Float(float? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_int")
                return (int)m_Argument.value;
            if (m_Argument?.kind == "as_float")
                return (float)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, float, or None, got \"{m_Argument?.kind}\"");
        }

        public float[] Floats(float[] defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_ints")
                return ((List<int>)m_Argument.value).Select(v => (float)v).ToArray();
            if (m_Argument.kind == "as_floats")
                return ((List<float>)m_Argument.value).ToArray();
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected list of ints, list of floats, or None, got \"{m_Argument?.kind}\"");
        }

        public int? OptionalInt(int? defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_int")
                return (int)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int or None, got \"{m_Argument?.kind}\"");
        }

        public bool? OptionalBool(bool? defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_bool")
                return (bool)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected bool or None, got \"{m_Argument?.kind}\"");
        }

        public int Int(int? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_int")
                return (int)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected int, got \"{m_Argument?.kind}\"");
        }

        public int[] Ints(int[] defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument?.kind == "as_ints")
                return ((List<int>)m_Argument.value).ToArray();
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected list of ints, got \"{m_Argument?.kind}\"");
        }

        public string String(string defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_string")
                return (string)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected string, got \"{m_Argument?.kind}\"");
        }

        public ScalarType ScalarType(ScalarType? defaultValue = null)
        {
            if ((m_Argument is null || m_Argument.kind == "as_none") && defaultValue.HasValue)
                return defaultValue.Value;
            if (m_Argument?.kind == "as_scalar_type")
                return (ScalarType)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected ScalarType, got \"{m_Argument?.kind}\"");
        }

        public ScalarType? OptionalScalarType(ScalarType? defaultValue = null)
        {
            if (m_Argument is null || m_Argument.kind == "as_none")
                return defaultValue;
            if (m_Argument.kind == "as_scalar_type")
                return (ScalarType)m_Argument.value;
            throw new TorchArgumentImportException(m_TargetName, m_ArgName, $"Expected ScalarType or None, got \"{m_Argument.kind}\"");
        }
    }
}
