using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction
{
    class ApprovalInteractionContent : InteractionContentView
    {
        const string k_ScopeOnce = "once";
        const string k_ScopeConversation = "conversation";

        static readonly List<AssistantDropdownItemData> k_ScopeItems = new()
        {
            new AssistantDropdownItemData(k_ScopeOnce, "Just once"),
            new AssistantDropdownItemData(k_ScopeConversation, "For conversation")
        };

        Button m_ScopeButton;
        Label m_ScopeLabel;
        AssistantDropdown m_ScopeMenu;
        string m_SelectedScopeId = k_ScopeOnce;

        Button m_DenyButton;
        Button m_AllowButton;
        Button m_ViewButton;

        bool m_ShowScope = true;
        string m_AllowLabel;
        string m_DenyLabel;
        Action<PermissionUserAnswer> m_OnAnswer;

        string m_ExpandedTitle;
        Func<VisualElement> m_ExpandedContentFactory;

        public void SetApprovalData(string allowLabel, string denyLabel, Action<PermissionUserAnswer> onAnswer, bool showScope = true)
        {
            m_AllowLabel = allowLabel;
            m_DenyLabel = denyLabel;
            m_OnAnswer = onAnswer;
            m_ShowScope = showScope;

            if (m_AllowButton != null)
            {
                ApplyLabels();
                m_ScopeButton.SetDisplay(m_ShowScope);
            }
        }

        public void SetExpandedContent(string title, Func<VisualElement> contentFactory)
        {
            m_ExpandedTitle = title;
            m_ExpandedContentFactory = contentFactory;

            if (m_ViewButton != null)
                m_ViewButton.SetDisplay(m_ExpandedContentFactory != null);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var root = view.Q<VisualElement>("approvalContentRoot");

            m_ScopeButton = view.SetupButton("scopeButton", _ => ToggleScopeMenu());
            m_ScopeLabel = m_ScopeButton.Q<Label>("scopeButtonLabel");
            m_ScopeLabel.text = k_ScopeItems[0].DisplayText;

            m_ScopeMenu = new AssistantDropdown();
            m_ScopeMenu.Initialize(Context, autoShowControl: false);
            m_ScopeMenu.SetItems(k_ScopeItems, m_SelectedScopeId);
            m_ScopeMenu.ItemSelected += OnScopeSelected;
            root.Add(m_ScopeMenu);

            m_DenyButton = view.SetupButton("denyButton", _ => OnDenyClicked());
            m_AllowButton = view.SetupButton("allowButton", _ => OnAllowClicked());
            m_ViewButton = view.SetupButton("viewButton", _ => OnViewClicked());

            m_ScopeButton.SetDisplay(m_ShowScope);
            m_ViewButton.SetDisplay(m_ExpandedContentFactory != null);
            ApplyLabels();
        }

        public void ResetScope()
        {
            SetScope(k_ScopeOnce);
        }

        void ApplyLabels()
        {
            m_AllowButton.text = m_AllowLabel ?? "Allow";
            m_DenyButton.text = m_DenyLabel ?? "Deny";
        }

        void ToggleScopeMenu()
        {
            if (m_ScopeMenu.IsShown)
            {
                m_ScopeMenu.HideMenu();
            }
            else
            {
                m_ScopeMenu.ShowAt(m_ScopeButton, m_ScopeButton);
            }
        }

        void OnScopeSelected(string id)
        {
            SetScope(id);
        }

        void SetScope(string id)
        {
            m_SelectedScopeId = id;
            m_ScopeMenu.SetSelectedId(id);

            var item = id == k_ScopeConversation ? k_ScopeItems[1] : k_ScopeItems[0];
            m_ScopeLabel.text = item.DisplayText;
        }

        void OnAllowClicked()
        {
            m_AllowButton.SetEnabled(false);

            var answer = ResolveAnswer(true);
            m_OnAnswer?.Invoke(answer);
            InvokeCompleted();
        }

        void OnDenyClicked()
        {
            m_AllowButton.SetEnabled(false);

            var answer = ResolveAnswer(false);
            m_OnAnswer?.Invoke(answer);
            InvokeCompleted();
        }

        void OnViewClicked()
        {
            if (m_ExpandedContentFactory == null)
                return;

            var content = m_ExpandedContentFactory();
            if (content == null)
                return;

            AssistantEvents.Send(new EventExpandedViewRequested(
                m_ExpandedTitle,
                content,
                scrollMode: ScrollViewMode.Vertical));
        }

        public static VisualElement CreateTextExpandedContent(string action, string detail)
        {
            var root = new VisualElement();
            root.AddToClassList("approval-expanded-content");

            var label = new Label();
            label.AddToClassList("approval-expanded-content-label");
            var hasAction = !string.IsNullOrEmpty(action);
            var hasDetail = !string.IsNullOrEmpty(detail);
            label.text = hasAction && hasDetail
                ? action + "\n\n" + detail
                : action ?? detail ?? "";
            label.selection.isSelectable = true;
            root.Add(label);
            return root;
        }

        PermissionUserAnswer ResolveAnswer(bool isAllow)
        {
            var isConversationScope = m_SelectedScopeId == k_ScopeConversation;

            if (isAllow)
            {
                return isConversationScope ? PermissionUserAnswer.AllowAlways : PermissionUserAnswer.AllowOnce;
            }

            return isConversationScope ? PermissionUserAnswer.DenyAlways : PermissionUserAnswer.DenyOnce;
        }
    }
}
