using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(PlanModeTools), nameof(PlanModeTools.EditPlan))]
    class EditPlanFunctionCallElement : CodeEditFunctionCallElement
    {
        const string k_FunctionDisplayName = "Edit Plan";

        public override string Title => k_FunctionDisplayName;

        public EditPlanFunctionCallElement() : base(typeof(CodeEditFunctionCallElement)) { }
    }
}
