using System;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    class RetryInteractionContent : InteractionContentView
    {
        Button m_DismissButton;
        Button m_RetryButton;
        Action m_OnRetry;

        public void SetRetryData(Action onRetry)
        {
            m_OnRetry = onRetry;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_DismissButton = view.SetupButton("dismissButton", _ => OnDismissClicked());
            m_RetryButton = view.SetupButton("retryButton", _ => OnRetryClicked());
        }

        void OnRetryClicked()
        {
            m_RetryButton.SetEnabled(false);
            m_DismissButton.SetEnabled(false);
            m_OnRetry?.Invoke();
            InvokeCompleted();
        }

        void OnDismissClicked()
        {
            m_RetryButton.SetEnabled(false);
            m_DismissButton.SetEnabled(false);
            InvokeCompleted();
        }
    }
}
