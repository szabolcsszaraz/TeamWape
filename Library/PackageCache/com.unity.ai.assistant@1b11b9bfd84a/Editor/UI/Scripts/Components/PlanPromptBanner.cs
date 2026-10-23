using System;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Assistant.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    sealed class PlanPromptBanner : ManagedTemplate
    {
        readonly Action m_OnYes;
        readonly Action m_OnNo;

        Button m_AllowButton;

        public PlanPromptBanner(Action onYes, Action onNo)
            : base(AssistantUIConstants.UIModulePath)
        {
            m_OnYes = onYes;
            m_OnNo = onNo;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var subtitle = view.Q<Label>("subtitle");
            
            var planPromptFile = ServiceRegistry.GetService<IPlanPromptFile>();
            if (planPromptFile == null)
            {
                InternalLog.LogError($"[ContextEntry] {nameof(IPlanPromptFile)} service not registered - is {nameof(AssistantWindow)} open?");
                return;
            }
            
            subtitle.text = $"Assistant wants to build: {planPromptFile.GetPathForDisplay()}";

            view.SetupButton("denyButton", _ => m_OnNo?.Invoke());
            m_AllowButton = view.SetupButton("allowButton", _ => m_OnYes?.Invoke());
        }

        internal void SetYesButtonEnabled(bool enabled)
        {
            m_AllowButton.SetEnabled(enabled);
        }
    }
}
