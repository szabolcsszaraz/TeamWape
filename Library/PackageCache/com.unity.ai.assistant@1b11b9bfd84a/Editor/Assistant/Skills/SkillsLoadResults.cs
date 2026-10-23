using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using Unity.AI.Assistant.Skills;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Holds parse errors produced by skill scans.
    /// Thread-safe: <see cref="StoreIssues"/> and <see cref="ClearIssues"/> are called from
    /// background scan threads; <see cref="SkillParsingIssues"/> is read on the main thread.
    /// </summary>
    class SkillsLoadResults
    {
        readonly object m_Lock = new();
        readonly Dictionary<string, List<SkillFileIssue>> m_ParsingIssuesByTag = new();
        ReadOnlyCollection<SkillFileIssue> m_SortedIssues;
        bool m_ParsingIssuesDirty = true;

        /// <summary>
        /// All parse errors across all skill sources, sorted by skill folder name. Thread-safe.
        /// </summary>
        internal IReadOnlyList<SkillFileIssue> SkillParsingIssues
        {
            get
            {
                lock (m_Lock)
                {
                    if (m_ParsingIssuesDirty)
                    {
                        SortIssuesLocked();
                        m_ParsingIssuesDirty = false;
                    }

                    return m_SortedIssues;
                }
            }
        }

        void SortIssuesLocked()
        {
            Debug.Assert(Monitor.IsEntered(m_Lock), "SortIssuesLocked must be called with m_Lock held.");
            var issues = new List<SkillFileIssue>();
            foreach (var list in m_ParsingIssuesByTag.Values)
                issues.AddRange(list);
            issues.Sort((a, b) =>
            {
                if (string.IsNullOrEmpty(a.DisplayName) && string.IsNullOrEmpty(b.DisplayName)) return 0;
                if (string.IsNullOrEmpty(a.DisplayName)) return -1;
                if (string.IsNullOrEmpty(b.DisplayName)) return 1;
                return string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase);
            });
            m_SortedIssues = issues.AsReadOnly();
        }

        /// <summary>
        /// Stores the issues from scanning one type (tag) of skills. Thread-safe.
        /// </summary>
        internal void StoreIssues(string tag, List<SkillFileIssue> issues)
        {
            lock (m_Lock)
            {
                if (issues == null)
                {
                    if (m_ParsingIssuesByTag.Remove(tag))
                        m_ParsingIssuesDirty = true;
                    return;
                }

                m_ParsingIssuesByTag[tag] = issues;
                m_ParsingIssuesDirty = true;
            }
        }

        /// <summary>
        /// Removes all stored issues for the given tag. Thread-safe.
        /// </summary>
        internal void ClearIssues(string tag)
        {
            lock (m_Lock)
            {
                if (m_ParsingIssuesByTag.Remove(tag))
                    m_ParsingIssuesDirty = true;
            }
        }
    }
}
