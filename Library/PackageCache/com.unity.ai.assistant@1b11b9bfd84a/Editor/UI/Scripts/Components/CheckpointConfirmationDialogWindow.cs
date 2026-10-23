using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Checkpoint;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class CheckpointConfirmationDialogWindow : EditorWindow
    {
        static readonly Vector2 k_WindowSize = new(480f, 460f);

        IReadOnlyList<CheckpointFileChange> m_FileChanges;
        string m_PromptText;
        long m_CheckpointTimestampMs;
        Action m_OnConfirm;
        Action m_OnCancel;
        bool m_Resolved;

        public static void Show(IReadOnlyList<CheckpointFileChange> fileChanges, string promptText, long checkpointTimestampMs, Action onConfirm, Action onCancel)
        {
            var window = CreateInstance<CheckpointConfirmationDialogWindow>();
            window.m_FileChanges = fileChanges;
            window.m_PromptText = promptText;
            window.m_CheckpointTimestampMs = checkpointTimestampMs;
            window.m_OnConfirm = onConfirm;
            window.m_OnCancel = onCancel;
            window.titleContent = new GUIContent("Confirm rollback");
            window.position = GetCenteredRect(k_WindowSize);
            window.ShowModalUtility();
        }

        public static async void ShowRestoreDialogAsync(AssistantMessageId responseMessageId, string promptText, Func<Task> onConfirm)
        {
            var checkpointInfo = await AssistantCheckpoints.GetCheckpointInfoForMessageAsync(
                responseMessageId.ConversationId, responseMessageId.FragmentId);

            if (checkpointInfo == null)
            {
                InternalLog.LogError("Cannot open revert dialog: checkpoint hash not found for message.");
                return;
            }

            // Flush dirty scenes to disk so the diff reflects what the restore will actually overwrite.
            var saveResult = await AssistantCheckpoints.SaveModifiedAssetsAsync();
            if (saveResult == AssetSaveResult.UserCancelled)
            {
                return;
            }
            if (saveResult == AssetSaveResult.Failed)
            {
                EditorUtility.DisplayDialog(
                    "Checkpoint Restore",
                    "Could not save modified scenes before computing the change list. Restore aborted.",
                    "OK");
                return;
            }

            try
            {
                var changes = await AssistantCheckpoints.GetRestoreChangesAsync(checkpointInfo.Value.Hash);

                Show(
                    changes,
                    promptText,
                    checkpointInfo.Value.Timestamp,
                    onConfirm: async () =>
                    {
                        try
                        {
                            await onConfirm();
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogError("Checkpoint restore failed: " + ex);
                            EditorUtility.DisplayDialog(
                                "Checkpoint Restore",
                                "Checkpoint restore failed. See console for details.",
                                "OK");
                        }
                    },
                    onCancel: () => { });
            }
            catch (Exception ex)
            {
                InternalLog.LogError("Checkpoint restore could not compute changes: " + ex);
                EditorUtility.DisplayDialog(
                    "Checkpoint Restore",
                    "Could not compute the changes for this checkpoint. Restore aborted. See console for details.",
                    "OK");
            }
        }

        void CreateGUI()
        {
            var dialog = new CheckpointConfirmationDialog();
            dialog.SetData(m_FileChanges, m_PromptText, m_CheckpointTimestampMs, m_OnConfirm, m_OnCancel);
            dialog.RequestClose += () =>
            {
                m_Resolved = true;
                Close();
            };
            dialog.Initialize(null);
            dialog.InitializeThemeAndStyle();
            rootVisualElement.Add(dialog);
        }

        void OnDestroy()
        {
            if (!m_Resolved)
            {
                m_OnCancel?.Invoke();
            }
        }

        static Rect GetCenteredRect(Vector2 size)
        {
            var editorMainWindowRect = EditorGUIUtility.GetMainWindowPosition();
            return new Rect(
                editorMainWindowRect.x + (editorMainWindowRect.width - size.x) * 0.5f,
                editorMainWindowRect.y + (editorMainWindowRect.height - size.y) * 0.5f,
                size.x,
                size.y
            );
        }
    }
}
