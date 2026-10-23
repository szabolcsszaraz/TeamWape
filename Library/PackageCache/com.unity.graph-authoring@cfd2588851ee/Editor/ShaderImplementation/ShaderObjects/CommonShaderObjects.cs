using System.Collections.Generic;
using System.Collections.Immutable;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    // TODO(SVFXG-869): ShaderType to provide more robust typing information and improved type handling.
    internal struct ShaderType : IShaderType
    {
        public bool IsValid { get; private set; }
        public string Name { get; private set; }
        internal ShaderType(string name)
        {
            IsValid = true;
            Name = name;
        }
    }

    internal struct ShaderField : IShaderField
    {
        public bool IsValid { get; private set; }
        public string Name { get; private set; }
        public bool IsInput { get; private set; }
        public bool IsOutput { get; private set; }
        public IShaderType ShaderType { get; private set; }
        public IReadOnlyCollection<IStrongHint> Hints { get; private set; }

        internal ShaderField(string name, bool isInput, bool isOutput, IShaderType shaderType, IReadOnlyCollection<IStrongHint> hints)
        {
            IsValid = true;
            Name = name;
            IsInput = isInput;
            IsOutput = isOutput;
            ShaderType = shaderType;
            Hints = hints ?? ImmutableArray<IStrongHint>.Empty;
        }
    }

    internal struct ShaderFunction : IShaderFunction
    {
        public bool IsValid { get; private set; }
        public string Name { get; private set; }
        public IShaderType ReturnType { get; private set; }
        public string FunctionBody { get; private set; }
        public IEnumerable<string> Namespace { get; private set; }
        public IEnumerable<IShaderField> Parameters { get; private set; }
        public IReadOnlyCollection<IStrongHint> Hints { get; private set; }

        internal ShaderFunction(string name, IEnumerable<string> namespaces, IEnumerable<IShaderField> parameters, IShaderType returnType, string functionBody, IReadOnlyCollection<IStrongHint> hints)
        {
            IsValid = true;
            Name = name;
            ReturnType = returnType;
            FunctionBody = functionBody;

            Parameters = parameters ?? ImmutableArray<IShaderField>.Empty;
            Namespace = namespaces ?? ImmutableArray<string>.Empty;
            Hints = hints ?? ImmutableArray<IStrongHint>.Empty;
        }
    }
}
