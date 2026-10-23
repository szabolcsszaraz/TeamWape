using System.Collections.Generic;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    internal interface IShaderType : IDefinition { }

    internal interface IShaderField : IDefinition
    {
        bool IsInput { get; }
        bool IsOutput { get; }
        IShaderType ShaderType { get; }
    }

    internal interface IShaderFunction : IDefinition
    {
        IShaderType ReturnType { get; }
        string FunctionBody { get; }
        IEnumerable<IShaderField> Parameters { get; }
    }
}
