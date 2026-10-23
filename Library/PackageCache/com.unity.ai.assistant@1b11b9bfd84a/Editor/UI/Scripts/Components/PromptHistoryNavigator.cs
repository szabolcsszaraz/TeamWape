using System;
using System.Collections.Generic;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Pure, UI-agnostic state machine for stepping through previously submitted prompts.
    /// Has no Unity, editor, or UI dependencies, so it stays unit-testable in isolation without a
    /// panel or a real conversation model. Callers feed it the current field text and an ordered
    /// history source (oldest -> newest) and it returns the text that should be placed in the field.
    /// </summary>
    class PromptHistoryNavigator
    {
        // Frozen copy of the history entries captured when navigation started; null when inactive.
        List<string> m_Snapshot;

        // Current position into m_Snapshot; only meaningful while active.
        int m_Index;

        public bool IsActive => m_Snapshot != null;

        public int Index => m_Index;

        public int Count => m_Snapshot?.Count ?? 0;

        public string Label => IsActive ? $"History {m_Index + 1}/{Count}" : "";

        /// <summary>
        /// Steps to an older entry. When inactive, only activates if the field is empty and the
        /// source has entries, starting at the newest. When active, moves one step older (clamped
        /// at the oldest) and always succeeds.
        /// </summary>
        public bool TryNavigateOlder(string currentText, IReadOnlyList<string> historySource, out string textToSet)
        {
            if (!IsActive)
            {
                if (!string.IsNullOrEmpty(currentText) || historySource == null || historySource.Count == 0)
                {
                    textToSet = "";
                    return false;
                }

                m_Snapshot = new List<string>(historySource);
                m_Index = m_Snapshot.Count - 1;
                textToSet = m_Snapshot[m_Index];
                return true;
            }

            m_Index = Math.Max(0, m_Index - 1);
            textToSet = m_Snapshot[m_Index];
            return true;
        }

        /// <summary>
        /// Steps to a newer entry. No-op when inactive. When active, moves one step newer; stepping
        /// past the newest entry exits history mode and clears the field (signalled by an empty
        /// <paramref name="textToSet"/> and <see cref="IsActive"/> becoming false).
        /// </summary>
        public bool TryNavigateNewer(out string textToSet)
        {
            if (!IsActive)
            {
                textToSet = "";
                return false;
            }

            if (m_Index < m_Snapshot.Count - 1)
            {
                m_Index++;
                textToSet = m_Snapshot[m_Index];
                return true;
            }

            Exit();
            textToSet = "";
            return true;
        }

        /// <summary>
        /// Ends history mode if the field text no longer matches the loaded entry (i.e. the user edited it).
        /// </summary>
        public void NotifyTextChanged(string currentText)
        {
            if (IsActive && currentText != m_Snapshot[m_Index])
                Exit();
        }

        public void Exit()
        {
            m_Snapshot = null;
            m_Index = 0;
        }
    }
}
