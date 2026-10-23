using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class CompletedActionsSection : ManagedTemplate
    {
        const int k_DefaultVisibleCount = 3;

        readonly List<CompletedActionEntry> m_Entries = new();

        Label m_HeaderLabel;
        VisualElement m_ContentContainer;
        Label m_MoreLabel;
        Label m_LessLabel;

        int m_ActiveCount;
        bool m_ShowExpanded;

        public CompletedActionsSection()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_HeaderLabel = view.Q<Label>("completedActionsHeader");
            m_ContentContainer = view.Q("completedActionsContent");

            m_MoreLabel = new Label();
            m_MoreLabel.AddToClassList("mui-completed-actions-toggle-label");
            m_MoreLabel.RegisterCallback<ClickEvent>(OnMoreClicked);
            m_ContentContainer.Add(m_MoreLabel);

            m_LessLabel = new Label { text = "show less" };
            m_LessLabel.AddToClassList("mui-completed-actions-toggle-label");
            m_LessLabel.RegisterCallback<ClickEvent>(OnLessClicked);
            m_ContentContainer.Add(m_LessLabel);
        }

        public void SetData(List<CompletedActionData> actions)
        {
            if (actions == null || actions.Count == 0)
            {
                Hide();
                return;
            }

            for (int i = 0; i < actions.Count; i++)
            {
                CompletedActionEntry entry;

                if (i >= m_Entries.Count)
                {
                    entry = new CompletedActionEntry();
                    entry.Initialize(Context);
                    m_Entries.Add(entry);
                    // Insert entries before the toggle labels
                    m_ContentContainer.Insert(m_ContentContainer.IndexOf(m_MoreLabel), entry);
                }
                else
                {
                    entry = m_Entries[i];
                }

                entry.SetData(actions[i]);
            }

            for (int i = actions.Count; i < m_Entries.Count; i++)
            {
                m_Entries[i].Hide();
            }

            m_ActiveCount = actions.Count;
            m_ShowExpanded = false;
            UpdateVisibleEntries();

            Show();
            m_HeaderLabel.text = "Completed actions (" + actions.Count + ")";
        }

        void UpdateVisibleEntries()
        {
            bool hasOverflow = m_ActiveCount > k_DefaultVisibleCount;
            int visibleCount = m_ShowExpanded || !hasOverflow ? m_ActiveCount : k_DefaultVisibleCount;

            for (int i = 0; i < m_ActiveCount; i++)
            {
                if (i < visibleCount)
                    m_Entries[i].Show();
                else
                    m_Entries[i].Hide();
            }

            if (hasOverflow && !m_ShowExpanded)
            {
                int remaining = m_ActiveCount - k_DefaultVisibleCount;
                m_MoreLabel.text = remaining + " more action" + (remaining != 1 ? "s" : "") + "...";
                m_MoreLabel.SetDisplay(true);
                m_LessLabel.SetDisplay(false);
            }
            else if (hasOverflow && m_ShowExpanded)
            {
                m_MoreLabel.SetDisplay(false);
                m_LessLabel.SetDisplay(true);
            }
            else
            {
                m_MoreLabel.SetDisplay(false);
                m_LessLabel.SetDisplay(false);
            }
        }

        void OnMoreClicked(ClickEvent evt)
        {
            m_ShowExpanded = true;
            UpdateVisibleEntries();
            this.ReportHeightChange();
        }

        void OnLessClicked(ClickEvent evt)
        {
            m_ShowExpanded = false;
            UpdateVisibleEntries();
            this.ReportHeightChange();
        }
    }
}
