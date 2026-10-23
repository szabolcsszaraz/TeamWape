using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Skills
{
    /// <summary>
    /// Per-Editor-process cache of the most recent AVAILABLE_SKILLS_V1 payload
    /// from the backend. Populated on each new chat session.
    ///
    /// Used by developer-tools UI to render per-backend-skill enable/disable
    /// toggles. The cache also holds a delegate that lets external code
    /// (dev-tools) supply a set of skill names to mark as disabled in the
    /// outgoing SkillsResponseV1.
    /// </summary>
    static class BackendSkillsCache
    {
        static readonly object s_Lock = new();
        static IReadOnlyList<SkillMetaData> s_Skills = Array.Empty<SkillMetaData>();

        /// <summary>Fired (on the main thread) whenever the cache is updated.</summary>
        public static event Action OnUpdated;

        /// <summary>
        /// Optional callback supplied by developer-tools to indicate which
        /// backend skills should be reported as disabled in the next
        /// SkillsResponseV1. Returns a set of skill names. Null = no overrides.
        /// </summary>
        public static Func<IReadOnlyCollection<string>> DisabledSkillsProvider { get; set; }

        public static IReadOnlyList<SkillMetaData> Skills
        {
            get
            {
                lock (s_Lock) return s_Skills;
            }
        }

        public static void Update(IReadOnlyList<SkillMetaData> skills)
        {
            lock (s_Lock) s_Skills = skills ?? Array.Empty<SkillMetaData>();
            MainThread.DispatchIfNeeded(() => OnUpdated?.Invoke());
        }
    }
}
