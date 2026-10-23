using System;
using System.Threading;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PlanReviewHeaderActions : ManagedTemplate
    {
        public event Action DenyClicked;
        public event Action ApproveClicked;

        const string k_CopyIconClass = "mui-icon-copy";
        const string k_CheckmarkIconClass = "mui-icon-checkmark";

        readonly string m_PlanContent;
        Button m_CopyButton;
        Image m_CopyIconImage;
        CancellationTokenSource m_CopyActiveTokenSource;

        public PlanReviewHeaderActions(string planContent)
            : base(AssistantUIConstants.UIModulePath)
        {
            m_PlanContent = planContent ?? string.Empty;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            view.SetupButton("denyButton", _ => DenyClicked?.Invoke());
            view.SetupButton("approveButton", _ => ApproveClicked?.Invoke());
            m_CopyButton = view.SetupButton("planCopyButton", _ => OnCopyClicked());
            m_CopyIconImage = view.Q<Image>("planCopyIcon");

            RegisterAttachEvents(_ => { }, OnDetach);
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            m_CopyActiveTokenSource?.Cancel();
            m_CopyActiveTokenSource?.Dispose();
            m_CopyActiveTokenSource = null;
        }

        void OnCopyClicked()
        {
            GUIUtility.systemCopyBuffer = m_PlanContent;
            m_CopyIconImage.RemoveFromClassList(k_CopyIconClass);
            m_CopyIconImage.AddToClassList(k_CheckmarkIconClass);
            m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, true);
            TimerUtils.DelayedAction(ref m_CopyActiveTokenSource, () =>
            {
                m_CopyButton.EnableInClassList(AssistantUIConstants.ActiveActionButtonClass, false);
                m_CopyIconImage.RemoveFromClassList(k_CheckmarkIconClass);
                m_CopyIconImage.AddToClassList(k_CopyIconClass);
            });
        }
    }
}
