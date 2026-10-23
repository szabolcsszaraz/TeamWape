using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(RunReadOnlyCommandTool), nameof(RunReadOnlyCommandTool.ExecuteReadOnlyCommand), Emphasized = true)]
    class RunReadOnlyCommandFunctionCallElement : RunCommandFunctionCallElement
    {
        public RunReadOnlyCommandFunctionCallElement()
        {
            SetElementType(typeof(RunCommandFunctionCallElement));
        }

        protected override string DefaultTitle => "Query Project";
    }
}
