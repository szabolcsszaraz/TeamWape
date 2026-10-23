using System.Collections.Generic;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    sealed class NewSkillsNotificationPanel : ManagedTemplate
    {
        Label m_TitleLabel;
        Label m_BodyLabel;

        public NewSkillsNotificationPanel()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_TitleLabel = view.Q<Label>("titleLabel");
            m_BodyLabel = view.Q<Label>("bodyLabel");

            view.SetupButton("dismissButton", _ => OnDismissClicked());
            view.SetupButton("openPrefsButton", _ => OnOpenPrefsClicked());
            view.SetupButton("enableAllButton", _ => OnEnableAllClicked());
        }

        /// <summary>
        /// Refreshes the panel content from the current awaiting-notification list.
        /// Hides the panel when the list is empty; shows it otherwise.
        /// </summary>
        public void Refresh()
        {
            var skills = AssistantEditorPreferences.GetNewSkillsAwaitingNotification();
            if (skills.Count == 0)
            {
                this.SetDisplay(false);
                return;
            }

            UpdateContent(skills);
            this.SetDisplay(true);
        }

        void UpdateContent(IReadOnlyList<string> skills)
        {
            var count = skills.Count;
            m_TitleLabel.text = count == 1
                ? "1 skill awaiting review"
                : $"{count} skills awaiting review";

            m_BodyLabel.text = BuildBodyText(skills);
        }

        static string BuildBodyText(IReadOnlyList<string> skills)
        {
            const int maxListed = 5;
            var listed = skills.Count <= maxListed ? skills.Count : maxListed;
            var names = new System.Text.StringBuilder();
            for (var i = 0; i < listed; i++)
            {
                if (i > 0) names.Append(", ");
                // Display names may be "Category: Name" — show only the part after ':'
                var displayName = skills[i];
                var colon = displayName.IndexOf(':');
                names.Append(colon >= 0 ? displayName.Substring(colon + 1).Trim() : displayName);
            }
            var extra = skills.Count - listed;
            if (extra > 0)
                names.Append($", and {extra} more");

            return $"New skills are denied by default: {names}. Review and allow them in Preferences.";
        }

        void OnDismissClicked() => AssistantEditorPreferences.DismissNewSkillsNotification();

        void OnOpenPrefsClicked()
        {
            // Intentional: opening Preferences counts as acting on the notification.
            AssistantEditorPreferences.DismissNewSkillsNotification();
            SettingsService.OpenUserPreferences("Preferences/AI/Skills");
        }

        void OnEnableAllClicked() => AssistantEditorPreferences.EnableAllNewSkillsAndDismiss();
    }
}
