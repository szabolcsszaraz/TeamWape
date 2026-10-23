using System.Collections.Generic;
using UnityEngine;

namespace Unity.GraphAuthoring.Editor.ProviderSystem.Hints
{
    internal static class Param
    {
        internal const string kAccessModifier = "AccessModifier";
        internal const string kCustomEditor = "CustomEditor";

        internal const string kStatic = "sg:Static";
        internal const string kLocal = "sg:Local";
        internal const string kLiteral = "sg:Literal";
        internal const string kColor = "sg:Color";
        internal const string kRange = "sg:Range";
        internal const string kDropdown = "sg:Dropdown";
        internal const string kDefault = "sg:Default";
        internal const string kExternal = "sg:External";

        internal const string kSetting = "sg:Setting";
        internal const string kLinkage = "sg:Linkage";
        internal const string kDynamic = "sg:DynamicVector";
        internal const string kReferable = "sg:Referable";

        internal const string kCustomBinding = "sg:CustomBinding";

        internal static class Ref
        {
            internal const string kUV = "UV";
            internal const string kPosition = "Position";
            internal const string kNormal = "Normal";
            internal const string kBitangent = "Bitangent";
            internal const string kTangent = "Tangent";
            internal const string kViewDirection = "ViewDirection";
            internal const string kScreenPosition = "ScreenPosition";
            internal const string kVertexColor = "VertexColor";
        }
    }

    [StrongHint] sealed class Local : Flag
    {
        internal Local() : base(Param.kLocal, new string[] { Param.kAccessModifier }) { }
    }

    [StrongHint]
    internal class Range : IStrongHint<IShaderField>
    {
        public string Key => Param.kRange;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };
        public IReadOnlyCollection<string> Synonyms { get; } = new string[] { "Slider" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Range() { }

        // Direct-value constructor.
        internal Range(float[] resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;

            if (!field.IsInput) { Message = "Expected input parameter."; return; }

            if (field.ShaderType.Name != "half" && field.ShaderType.Name != "float")
            {
                Message = $"Expected floating point scalar, but found '{field.ShaderType.Name}'.";
                return;
            }

            float min = 0;
            float max = 1;

            if (string.IsNullOrWhiteSpace(rawValue)) { Value = new float[] { min, max }; IsResolved = true; return; }

            float[] values = HintUtils.LazyTokenFloat(rawValue);

            if (values.Length > 2 || !string.IsNullOrEmpty(rawValue) && values.Length == 0)
            {
                Message = $"Expected 0, 1, or 2 floating point values, but found '{values.Length}'.";
                return;
            }

            if (values.Length == 1)
            {
                if (values[0] < 0) { min = values[0]; max = 0; }
                else max = values[0];
            }
            else if (values.Length >= 2)
            {
                min = Mathf.Min(values);
                max = Mathf.Max(values);
            }

            Value = new float[2] { min, max };
            IsResolved = true;
            if (min == max)
                Message = $"Expected min and max to be different values, but both are '{min}'.";
        }
    }

    [StrongHint]
    internal class Dropdown : IStrongHint<IShaderField>
    {
        public string Key => Param.kDropdown;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };
        public IReadOnlyCollection<string> Synonyms { get; } = new string[] { "Enum" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Dropdown() { }

        // Direct-value constructor.
        internal Dropdown(string[] options)
        {
            Value = options;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }

            switch (field.ShaderType.Name)
            {
                case "int": case "uint": case "float": case "half": break;
                default: Message = $"Expected numeric scalar, but found '{field.ShaderType.Name}'."; return;
            }

            string[] options = HintUtils.LazyTokenString(rawValue);
            if (options.Length == 0) { Message = "Expected at least 1 comma separated option, but found none."; return; }

            Value = options;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class Color : IStrongHint<IShaderField>
    {
        public string Key => Param.kColor;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Color() { }

        // Direct-value constructor (flag present).
        internal Color(bool resolved)
        {
            IsResolved = resolved;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }

            switch (field.ShaderType.Name)
            {
                case "float3": case "float4": case "half3": case "half4":
                    Value = rawValue; IsResolved = true; break;
                default:
                    Message = $"Expected floating point vector of length 3 or 4, but found '{field.ShaderType.Name}'."; break;
            }
        }
    }

    [StrongHint]
    internal class Literal : IStrongHint<IShaderField>
    {
        public string Key => Param.kLiteral;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Literal() { }

        // Direct-value constructor.
        internal Literal(bool resolved)
        {
            IsResolved = resolved;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            switch (field.ShaderType.Name)
            {
                case "float": case "half": case "int": case "uint":
                    Value = rawValue; IsResolved = true; break;
                default:
                    Message = $"Expected numeric scalar, but found '{field.ShaderType.Name}'."; break;
            }
        }
    }

    [StrongHint]
    internal class Static : IStrongHint<IShaderField>
    {
        public string Key => Param.kStatic;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kAccessModifier };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Static() { }

        // Direct-value constructor.
        internal Static(bool resolved)
        {
            IsResolved = resolved;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }

            switch (field.ShaderType.Name)
            {
                case "float": case "half": case "int": case "uint": case "bool":
                    Value = rawValue; IsResolved = true; break;

                case "float3": case "float4": case "half3": case "half4":
                    if (rawHints?.ContainsKey(Param.kColor) == true) { Value = rawValue; IsResolved = true; break; }
                    Message = $"Requires '{Param.kColor}' to support '{field.ShaderType.Name}'."; break;

                default:
                    Message = $"Expected '{Param.kColor}' or scalar, but found '{field.ShaderType.Name}'."; break;
            }
        }
    }

    [StrongHint]
    internal class External : IStrongHint<IShaderField>
    {
        public string Key => Param.kExternal;
        public IReadOnlyCollection<string> Synonyms { get; } = new string[] { "ExternalNamespace" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal External() { }

        // Direct-value constructor.
        internal External(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            Value = rawValue;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class Default : IStrongHint<IShaderField>
    {
        public string Key => Param.kDefault;

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Default() { }

        // Direct-value constructor.
        internal Default(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            // TODO(SVFXG-868): Multiple formats to support; pass along raw value for now.
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }
            Value = rawValue;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class Dynamic : IStrongHint<IShaderField>
    {
        public string Key => Param.kDynamic;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };
        public IReadOnlyCollection<string> Synonyms { get; } = new string[] { "Dynamic" };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Dynamic() { }

        // Direct-value constructor.
        internal Dynamic(bool resolved)
        {
            IsResolved = resolved;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            switch (field.ShaderType.Name)
            {
                case "float": case "float2": case "float3": case "float4":
                case "half": case "half2": case "half3": case "half4":
                    Value = rawValue; IsResolved = true; break;
                default:
                    Message = $"Expected floating point vector or scalar, but found {field.ShaderType.Name}."; break;
            }
        }
    }

    [StrongHint]
    internal class Linkage : IStrongHint<IShaderField>
    {
        public string Key => Param.kLinkage;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kAccessModifier };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Linkage() { }

        // Direct-value constructor.
        internal Linkage(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }

            if (field.ShaderType.Name != "bool") { Message = $"Expected type bool, but found '{field.ShaderType.Name}'."; return; }
            if (rawValue == field.Name) { Message = "Cannot check linkage state against itself."; return; }

            // Validate the target parameter exists on the enclosing function.
            // The provider here is a FieldHintContext whose Definition is the already-built
            // unresolved function, so this is safe and non-recursive.
            var funcProvider = provider as IProvider<IShaderFunction>;
            if (funcProvider?.Definition != null)
            {
                bool hasMatch = false;
                foreach (var param in funcProvider.Definition.Parameters)
                    if (param.Name == rawValue) { hasMatch = true; break; }

                if (!hasMatch) { Message = $"Could not find expected parameter '{rawValue}'."; return; }
            }

            Value = rawValue;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class Referable : IStrongHint<IShaderField>
    {
        public string Key => Param.kReferable;

        static HashSet<string> s_commonReferables;

        private static HashSet<string> GetCommonReferables()
        {
            if (s_commonReferables == null)
            {
                s_commonReferables = new HashSet<string>() {
                    Param.Ref.kUV,
                    Param.Ref.kPosition,
                    Param.Ref.kTangent,
                    Param.Ref.kNormal,
                    Param.Ref.kBitangent,
                    Param.Ref.kViewDirection,
                    Param.Ref.kVertexColor,
                    Param.Ref.kScreenPosition,
                };
            }
            return s_commonReferables;
        }

        public IReadOnlyCollection<string> Synonyms => GetCommonReferables();
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal Referable() { }

        // Direct-value constructor.
        internal Referable(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            object value = Key == actualHintKey ? rawValue : actualHintKey;

            if (!field.IsInput) { Message = "Expected input parameter."; return; }
            if (value == null) { Message = "Could not resolve a referable type."; return; }
            if (!GetCommonReferables().Contains((string)value)) { Message = $"'{value}' is not a supported referable key."; return; }

            switch ((string)value)
            {
                case Param.Ref.kUV:
                    if (field.ShaderType.Name != "half2" && field.ShaderType.Name != "float2")
                    { Message = $"'{value}' expects floating point vector of length 2, but found '{field.ShaderType.Name}'."; return; }
                    break;
                case Param.Ref.kVertexColor:
                case Param.Ref.kScreenPosition:
                    if (field.ShaderType.Name != "half4" && field.ShaderType.Name != "float4")
                    { Message = $"'{value}' expects floating point vector of length 4, but found '{field.ShaderType.Name}'."; return; }
                    break;
                default:
                    if (field.ShaderType.Name != "half3" && field.ShaderType.Name != "float3")
                    { Message = $"'{value}' expects floating point vector of length 3, but found '{field.ShaderType.Name}'."; return; }
                    break;
            }

            Value = value;
            IsResolved = true;
        }
    }

    [StrongHint]
    internal class CustomBinding : IStrongHint<IShaderField>
    {
        public string Key => Param.kCustomBinding;
        public IReadOnlyCollection<string> Conflicts { get; } = new string[] { Param.kCustomEditor };

        public object Value { get; private set; }
        public string Message { get; private set; }
        public bool IsResolved { get; private set; }

        // Default constructor — used by TypeCache auto-discovery.
        internal CustomBinding() { }

        // Direct-value constructor.
        internal CustomBinding(string resolvedValue)
        {
            Value = resolvedValue;
            IsResolved = true;
        }

        public void Initialize(bool found, string rawValue, IShaderField field, IProvider provider, string actualHintKey, IReadOnlyDictionary<string, string> rawHints)
        {
            if (!found) return;
            if (!field.IsInput) { Message = "Expected input parameter."; return; }
            if (string.IsNullOrEmpty(rawValue)) { Message = "Invalid value."; return; }
            Value = rawValue;
            IsResolved = true;
        }
    }
}
