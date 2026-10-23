using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;

// created from https://github.com/pytorch/pytorch/blob/release/2.8/torch/_export/serde/schema.yaml
namespace TorchPt2
{
    class AOTInductorModelPickleData
    {
        public string library_basename;
        public List<string> input_names;
        public List<string> output_names;
        public int? floating_point_input_dtype;
        public int? floating_point_output_dtype;
        public bool? aot_inductor_model_is_cpu;
    }

    [JsonConverter(typeof(ArgumentConverter))]
    class Argument : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_none" => typeof(bool),
                "as_tensor" => typeof(TensorArgument),
                "as_tensors" => typeof(List<TensorArgument>),
                "as_int" => typeof(int),
                "as_ints" => typeof(List<int>),
                "as_float" => typeof(float),
                "as_floats" => typeof(List<float>),
                "as_string" => typeof(string),
                "as_strings" => typeof(List<string>),
                "as_sym_int" => typeof(SymIntArgument),
                "as_sym_ints" => typeof(List<SymIntArgument>),
                "as_sym_bool" => typeof(SymBoolArgument),
                "as_sym_bools" => typeof(List<SymBoolArgument>),
                "as_optional_tensor" => typeof(OptionalTensorArgument),
                "as_optional_tensors" => typeof(List<OptionalTensorArgument>),
                "as_custom_obj" => typeof(CustomObjArgument),
                "as_operator" => typeof(string),
                "as_sym_float" => typeof(SymFloatArgument),
                "as_sym_floats" => typeof(List<SymFloatArgument>),
                "as_graph" => typeof(GraphArgument),
                "as_scalar_type" => typeof(ScalarType),
                "as_memory_format" => typeof(MemoryFormat),
                "as_layout" => typeof(Layout),
                "as_device" => typeof(Device),
                "as_bool" => typeof(bool),
                "as_bools" => typeof(List<bool>),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class ArgumentConverter : UnionConverter<Argument> { }

    enum ArgumentKind
    {
        UNKNOWN = 0,
        POSITIONAL = 1,
        KEYWORD = 2
    }

    class BufferMutationSpec
    {
        public TensorArgument arg;
        public string buffer_name;
    }

    [JsonConverter(typeof(ConstantValueConverter))]
    class ConstantValue : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_none" => typeof(bool),
                "as_int" => typeof(int),
                "as_float" => typeof(float),
                "as_string" => typeof(string),
                "as_bool" => typeof(bool),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class ConstantValueConverter : UnionConverter<ConstantValue> { }

    class CustomObjArgument
    {
        public string name;
        public string class_fqn;
    }

    class Device
    {
        public string type;
        public int? index;
    }

    class ExportedProgram
    {
        public GraphModule graph_module { get; set; }
        public Dictionary<string, int> opset_version { get; set; }
        public Dictionary<string, RangeConstraint> range_constraints { get; set; }
        public SchemaVersion schema_version { get; set; }
        public List<string> verifiers { get; set; }
        public string torch_version { get; set; }
    }

    class ExternKernelNode
    {
        public string name;
        public Node node;
    }

    class ExternKernelNodes
    {
        public List<ExternKernelNode> nodes;
    }

    class GradientToParameterSpec
    {
        public TensorArgument arg;
        public string parameter_name;
    }

    class GradientToUserInputSpec
    {
        public TensorArgument arg;
        public string user_input_name;
    }

    class Graph
    {
        public List<Argument> inputs { get; set; }
        public List<Argument> outputs { get; set; }
        public List<Node> nodes { get; set; }
        public Dictionary<string, TensorMeta> tensor_values { get; set; }
        public Dictionary<string, SymInt> sym_int_values { get; set; }
        public Dictionary<string, SymBool> sym_bool_values { get; set; }
        public bool is_single_tensor_return { get; set; }
        public Dictionary<string, CustomObjArgument> custom_obj_values { get; set; }
        public Dictionary<string, SymFloat> sym_float_values { get; set; }
    }

    class GraphArgument
    {
        public string name;
        public Graph graph;
    }

    class GraphModule
    {
        public Graph graph { get; set; }
        public GraphSignature signature { get; set; }
        public List<ModuleCallEntry> module_call_graph { get; set; }
        public Dictionary<string, string> metadata { get; set; }
        public Dictionary<string, NamedTupleDef> treespec_namedtuple_fields { get; set; }
    }

    class GraphSignature
    {
        public List<InputSpec> input_specs;
        public List<OutputSpec> output_specs;
    }

    [JsonConverter(typeof(InputSpecConverter))]
    class InputSpec : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "user_input" => typeof(UserInputSpec),
                "parameter" => typeof(InputToParameterSpec),
                "buffer" => typeof(InputToBufferSpec),
                "tensor_constant" => typeof(InputToTensorConstantSpec),
                "custom_obj" => typeof(InputToCustomObjSpec),
                "token" => typeof(InputTokenSpec),
                "constant_input" => typeof(InputToConstantInputSpec),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class InputSpecConverter : UnionConverter<InputSpec> { }

    class InputToBufferSpec
    {
        public TensorArgument arg;
        public string buffer_name;
        public bool persistent;
    }

    class InputToConstantInputSpec
    {
        public string name;
        public ConstantValue value;
    }

    class InputToCustomObjSpec
    {
        public CustomObjArgument arg;
        public string custom_obj_name;
    }

    class InputToParameterSpec
    {
        public TensorArgument arg;
        public string parameter_name;
    }

    class InputToTensorConstantSpec
    {
        public TensorArgument arg;
        public string tensor_constant_name;
    }

    class InputTokenSpec
    {
        public TokenArgument arg;
    }

    enum Layout
    {
        Unknown = 0,
        SparseCoo = 1,
        SparseCsr = 2,
        SparseCsc = 3,
        SparseBsr = 4,
        SparseBsc = 5,
        _mkldnn = 6,
        Strided = 7,
    }

    class LossOutputSpec
    {
        public TensorArgument arg;
    }

    enum MemoryFormat
    {
        Unknown = 0,
        ContiguousFormat = 1,
        ChannelsLast = 2,
        ChannelsLast3d = 3,
        PreserveFormat = 4,
    }

    class Model
    {
        public string name;
        public Dictionary<string, string> tensorPaths { get; set; }
        public Program program;
        public Dictionary<string, Program> delegates;
        public Dictionary<string, string> deviceAllocationMap;
        public Dictionary<string, string> constantPaths;
    }

    class ModuleCallEntry
    {
        public string fqn;
        [CanBeNull]
        public ModuleCallSignature signature;
    }

    class ModuleCallSignature
    {
        public List<Argument> inputs;
        public List<Argument> outputs;
        public string in_spec;
        public string out_spec;
        [CanBeNull]
        public List<string> forward_arg_names;
    }

    class NamedArgument
    {
        public string name { get; set; }
        public Argument arg { get; set; }
        public ArgumentKind? kind { get; set; }
    }

    class NamedTupleDef
    {
        public List<string> field_names;
    }

    class Node
    {
        public string target;
        public List<NamedArgument> inputs;
        public List<Argument> outputs;
        public Dictionary<string, string> metadata { get; set; }
        public bool? is_hop_single_tensor_return { get; set; }
    }

    [JsonConverter(typeof(OptionalTensorArgumentConverter))]
    class OptionalTensorArgument : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_tensor" => typeof(TensorArgument),
                "as_none" => typeof(bool),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class OptionalTensorArgumentConverter : UnionConverter<OptionalTensorArgument> { }

    [JsonConverter(typeof(OutputSpecConverter))]
    class OutputSpec : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "user_output" => typeof(UserOutputSpec),
                "loss_output" => typeof(LossOutputSpec),
                "buffer_mutation" => typeof(BufferMutationSpec),
                "gradient_to_parameter" => typeof(GradientToParameterSpec),
                "gradient_to_user_input" => typeof(GradientToUserInputSpec),
                "user_input_mutation" => typeof(UserInputMutationSpec),
                "token" => typeof(OutputTokenSpec),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class OutputSpecConverter : UnionConverter<OutputSpec> { }

    class OutputTokenSpec
    {
        public TokenArgument arg;
    }

    class Program
    {
        public Dictionary<string, ExportedProgram> fields;
    }

    class RangeConstraint
    {
        public int? min_val { get; set; }
        public int? max_val { get; set; }
    }

    enum ScalarType
    {
        UNKNOWN = 0,
        BYTE = 1,
        CHAR = 2,
        SHORT = 3,
        INT = 4,
        LONG = 5,
        HALF = 6,
        FLOAT = 7,
        DOUBLE = 8,
        COMPLEXHALF = 9,
        COMPLEXFLOAT = 10,
        COMPLEXDOUBLE = 11,
        BOOL = 12,
        BFLOAT16 = 13,
        UINT16 = 28,
        FLOAT8E4M3FN = 29,
        FLOAT8E5M2 = 30,
    }

    class SchemaVersion
    {
        public int major { get; set; }
        public int minor { get; set; }
    }

    [JsonConverter(typeof(SymBoolConverter))]
    class SymBool : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_expr" => typeof(SymExpr),
                "as_bool" => typeof(bool),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymBoolConverter : UnionConverter<SymBool> { }

    [JsonConverter(typeof(SymBoolArgumentConverter))]
    class SymBoolArgument : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_name" => typeof(string),
                "as_bool" => typeof(bool),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymBoolArgumentConverter : UnionConverter<SymBoolArgument> { }

    class SymExpr
    {
        public string expr_str;
        [CanBeNull]
        public SymExprHint hint;
    }

    [JsonConverter(typeof(SymExprHintConverter))]
    class SymExprHint : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_int" => typeof(int),
                "as_bool" => typeof(bool),
                "as_float" => typeof(float),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymExprHintConverter : UnionConverter<SymExprHint> { }

    [JsonConverter(typeof(SymFloatConverter))]
    class SymFloat : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_expr" => typeof(SymExpr),
                "as_float" => typeof(float),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymFloatConverter : UnionConverter<SymFloat> { }

    [JsonConverter(typeof(SymFloatArgumentConverter))]
    class SymFloatArgument : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_name" => typeof(string),
                "as_float" => typeof(float),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymFloatArgumentConverter : UnionConverter<SymFloatArgument> { }

    [JsonConverter(typeof(SymIntConverter))]
    class SymInt : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_expr" => typeof(SymExpr),
                "as_int" => typeof(int),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymIntConverter : UnionConverter<SymInt> { }

    [JsonConverter(typeof(SymIntArgumentConverter))]
    class SymIntArgument : Union
    {
        public override Type FromKind(string name)
        {
            return name switch
            {
                "as_name" => typeof(string),
                "as_int" => typeof(int),
                _ => throw new ArgumentOutOfRangeException(nameof(name), name, null)
            };
        }
    }

    class SymIntArgumentConverter : UnionConverter<SymIntArgument> { }

    class TensorArgument
    {
        public string name { get; set; }
    }

    class TensorMeta
    {
        public ScalarType dtype;
        public List<SymInt> sizes;
        public bool requires_grad;
        public Device device;
        public List<SymInt> strides;
        public SymInt storage_offset;
        public Layout layout;
    }

    class TokenArgument
    {
        public string name;
    }

    class UserInputMutationSpec
    {
        public TensorArgument arg;
        public string user_input_name;
    }

    class UserInputSpec
    {
        public Argument arg;
    }

    class UserOutputSpec
    {
        public Argument arg;
    }
}
