using System;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Minimal chat record for the write_todos tool call.
    /// Progress tracking is handled by <see cref="TodoProgressInteractionElement"/> in the bottom interaction bar.
    /// </summary>
    [FunctionCallRenderer("Unity.WriteTodos", Emphasized = true)]
    class ChatElementTodoList : DefaultFunctionCallRenderer
    {
        public override string Title { get; protected set; } = "Update Todos";
        public override bool Expanded { get; protected set; } = true;

        public override void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
        }

        public override void OnCallError(string functionId, Guid callId, string error)
        {
        }
    }
}
