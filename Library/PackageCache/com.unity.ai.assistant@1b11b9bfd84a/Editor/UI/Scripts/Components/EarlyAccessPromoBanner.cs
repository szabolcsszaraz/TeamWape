using System;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.UIElements.Experimental;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    // Temporary — delete this file and every EARLY_ACCESS_PROMO_START/END block when Unity AI Desktop ships.
    class EarlyAccessPromoBanner : ManagedTemplate
    {
        const string k_SuccessClass = "mui-early-access-promo--success";
        const string k_InvalidClass = "mui-early-access-promo--invalid";
        const string k_EmailHasValueClass = "mui-early-access-promo-email--has-value";

        const string k_DismissedPrefKey = "muse.early_access_promo.dismissed";
        const string k_SubmittedPrefKey = "muse.early_access_promo.submitted";

        VisualElement m_Root;
        VisualElement m_EmailFieldWrapper;
        TextField m_EmailField;

        bool m_Dismissed;
        bool m_Submitted;
        bool m_Blocked;

        public event Action ShownStateChanged;

        public bool IsShowing => IsShown && !m_Blocked;

        public EarlyAccessPromoBanner()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            // Seed hidden: autoShowControl:false skips Show(), and ApplyVisibility's equal-state path skips Hide().
            style.display = DisplayStyle.None;

            m_Root = view.Q<VisualElement>("promoRoot");
            m_EmailFieldWrapper = view.Q<VisualElement>("emailFieldWrapper");
            m_EmailField = view.Q<TextField>("emailField");

            m_EmailField.RegisterValueChangedCallback(evt =>
            {
                m_EmailFieldWrapper.EnableInClassList(k_EmailHasValueClass, !string.IsNullOrEmpty(evt.newValue));
                if (m_Root.ClassListContains(k_InvalidClass))
                    m_Root.RemoveFromClassList(k_InvalidClass);
            });

            view.SetupButton("submitButton", _ => OnSubmit());

            m_EmailField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode is not (KeyCode.Return or KeyCode.KeypadEnter))
                    return;
                evt.StopPropagation();
                OnSubmit();
            }, TrickleDown.TrickleDown);

            var closeButton = view.SetupButton("closeButton", evt => evt.StopPropagation());
            closeButton.clicked += Dismiss;

            var disclaimer = view.Q<Label>("disclaimer");
            disclaimer.text = "By submitting, you agree that Unity may email you about early access to the desktop AI agent. See our <color=#7BAEFA><link=\"" + AssistantUIConstants.FeedbackPrivacyPolicyUrl + "\">Privacy Policy</link></color> for how we handle your data and how to unsubscribe.";
            disclaimer.RegisterCallback<PointerDownLinkTagEvent>(evt =>
            {
                if (!string.IsNullOrEmpty(evt.linkID))
                    Application.OpenURL(evt.linkID);
            });
            disclaimer.RegisterCallback<PointerOverLinkTagEvent>(_ => disclaimer.AddToClassList(AssistantUIConstants.RichTextLinkHoverClass));
            disclaimer.RegisterCallback<PointerOutLinkTagEvent>(_ => disclaimer.RemoveFromClassList(AssistantUIConstants.RichTextLinkHoverClass));

            m_Dismissed = EditorPrefs.GetBool(k_DismissedPrefKey, false);
            m_Submitted = EditorPrefs.GetBool(k_SubmittedPrefKey, false);

            ApplyVisibility();
        }

        public void SetBlocked(bool blocked)
        {
            m_Blocked = blocked;
            ApplyVisibility();
        }

        void OnSubmit()
        {
            if (m_Submitted)
                return;

            var email = m_EmailField.value?.Trim();
            if (!IsValidEmail(email))
            {
                m_Root.AddToClassList(k_InvalidClass);
                m_EmailField.Focus();
                return;
            }

            m_Root.RemoveFromClassList(k_InvalidClass);
            AIAssistantAnalytics.ReportEarlyAccessSignup(email);

            m_Submitted = true;
            EditorPrefs.SetBool(k_SubmittedPrefKey, true);
            m_Root.AddToClassList(k_SuccessClass);
        }

        void Dismiss()
        {
            m_Dismissed = true;
            EditorPrefs.SetBool(k_DismissedPrefKey, true);
            ApplyVisibility();
        }

        void ApplyVisibility()
        {
            var shouldShow = !m_Dismissed && !m_Blocked && !m_Submitted;
            if (shouldShow == IsShown)
                return;

            if (shouldShow)
                Show();
            else
                Hide();

            ShownStateChanged?.Invoke();
        }

        static bool IsValidEmail(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return false;

            var at = email.IndexOf('@');
            if (at <= 0 || at != email.LastIndexOf('@') || at == email.Length - 1)
                return false;

            var domain = email.Substring(at + 1);
            var dot = domain.IndexOf('.');
            return dot > 0 && dot < domain.Length - 1;
        }

    }
}
