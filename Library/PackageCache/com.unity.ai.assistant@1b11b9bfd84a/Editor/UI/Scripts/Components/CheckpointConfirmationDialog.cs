using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class CheckpointConfirmationDialog : ManagedTemplate
    {
        const string k_ClassRoot = "checkpoint-confirmation-dialog";

        VisualElement m_Root;
        IReadOnlyList<CheckpointFileChange> m_FileChanges;
        string m_PromptText;
        long m_CheckpointTimestampMs;
        Action m_OnConfirm;
        Action m_OnCancel;

        public event Action RequestClose;

        public CheckpointConfirmationDialog() : base(AssistantUIConstants.UIModulePath) { }

        public void SetData(
            IReadOnlyList<CheckpointFileChange> fileChanges,
            string promptText,
            long checkpointTimestampMs,
            Action onConfirm,
            Action onCancel)
        {
            m_FileChanges = fileChanges;
            m_PromptText = promptText;
            m_CheckpointTimestampMs = checkpointTimestampMs;
            m_OnConfirm = onConfirm;
            m_OnCancel = onCancel;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var files = m_FileChanges ?? new List<CheckpointFileChange>();

            var bodyLabel = view.Q<Label>("bodyLabel");
            if (m_CheckpointTimestampMs > 0)
            {
                var timeAgo = FormatTimeAgo(DateTimeOffset.FromUnixTimeMilliseconds(m_CheckpointTimestampMs).UtcDateTime);
                bodyLabel.text = $"Are you sure you want to restore the Editor to the checkpoint before \"{m_PromptText}\", created {timeAgo}?";
            }
            else
            {
                bodyLabel.text = $"Are you sure you want to restore the Editor to the checkpoint before \"{m_PromptText}\"?";
            }

            var warningLabel = view.Q<Label>("warningLabel");
            var fileWord = files.Count == 1 ? "file" : "files";
            warningLabel.text = $"{files.Count} {fileWord} will be reverted to an earlier state. This cannot be undone";

            var fileListContainer = view.Q<ScrollView>("fileListContainer");
            BuildSections(fileListContainer, files);

            var cancelButton = view.SetupButton("cancelButton", OnCancelClicked);
            view.SetupButton("confirmButton", OnConfirmClicked);

            cancelButton.Focus();
        }

        public void InitializeThemeAndStyle()
        {
            LoadStyle(this, EditorGUIUtility.isProSkin ? AssistantUIConstants.AssistantSharedStyleDark : AssistantUIConstants.AssistantSharedStyleLight);
            LoadStyle(this, AssistantUIConstants.AssistantBaseStyle, true);
        }

        static void BuildSections(ScrollView container, IReadOnlyList<CheckpointFileChange> files)
        {
            var deleted = files.Where(f => f.Type == CheckpointFileChangeType.Deleted).ToList();
            var modified = files.Where(f => f.Type == CheckpointFileChangeType.Modified).ToList();
            var added = files.Where(f => f.Type == CheckpointFileChangeType.Added).ToList();

            if (deleted.Count > 0)
            {
                container.Add(MakeSection(deleted, "Deleted", "mui-icon-trash", k_ClassRoot + "__section-icon--error"));
            }

            if (modified.Count > 0)
            {
                container.Add(MakeSection(modified, "Modified", "mui-icon-pen", null));
            }

            if (added.Count > 0)
            {
                container.Add(MakeSection(added, "Added", "mui-icon-plus", k_ClassRoot + "__section-icon--success"));
            }
        }

        static Foldout MakeSection(List<CheckpointFileChange> files, string label, string iconClass, string iconTintClass)
        {
            var foldout = new Foldout
            {
                text = label + " (" + files.Count + ")",
                value = true
            };

            foldout.AddToClassList(k_ClassRoot + "__section-foldout");
            InjectSectionHeaderIcon(foldout, iconClass, iconTintClass);

            foreach (var file in files)
            {
                var row = new CheckpointAffectedFileRow();
                row.Initialize(null);
                row.SetData(file.Path, iconClass);
                foldout.Add(row);
            }

            return foldout;
        }

        static void InjectSectionHeaderIcon(Foldout foldout, string iconClass, string iconTintClass)
        {
            var headerLabel = foldout.Q<Label>(className: "unity-foldout__text");
            if (headerLabel == null)
            {
                return;
            }

            var icon = new Image();
            icon.AddToClassList(k_ClassRoot + "__section-icon");
            icon.AddToClassList(iconClass);
            if (!string.IsNullOrEmpty(iconTintClass))
            {
                icon.AddToClassList(iconTintClass);
            }

            var parent = headerLabel.parent;
            parent.Insert(parent.IndexOf(headerLabel), icon);
        }

        static string FormatTimeAgo(DateTime createdAt)
        {
            var elapsed = DateTime.UtcNow - createdAt;

            if (elapsed.TotalSeconds < 60)
            {
                return "just now";
            }

            if (elapsed.TotalMinutes < 60)
            {
                var minutes = (int)elapsed.TotalMinutes;
                return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
            }

            if (elapsed.TotalHours < 24)
            {
                var hours = (int)elapsed.TotalHours;
                return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
            }

            var days = (int)elapsed.TotalDays;
            return days == 1 ? "1 day ago" : $"{days} days ago";
        }

        void OnCancelClicked(PointerUpEvent evt)
        {
            m_OnCancel?.Invoke();
            RequestClose?.Invoke();
        }

        void OnConfirmClicked(PointerUpEvent evt)
        {
            m_OnConfirm?.Invoke();
            RequestClose?.Invoke();
        }
    }
}
