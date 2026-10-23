using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Skills
{
    /// <summary>
    /// Registry for local skills (SkillDefinition) to be sent to the backend.
    /// Skills are scanned and loaded asynchronously on editor startup and domain reload.
    /// Callers of <see cref="GetSkills"/> implicitly wait for any in-progress scan before receiving results.
    /// All candidates (including same-name duplicates from different paths) are stored; deduplication
    /// happens lazily in <see cref="GetFilteredSnapshotLocked"/> using a priority ordering.
    /// </summary>
    static class SkillsRegistry
    {
        // Timeout, at which GetSkills() returns partial results
        const int k_GetSkillsTimeoutMs = 5000;

        static readonly object s_Lock = new(); // guards s_CandidateSkills, s_FilteredSkillsCache, s_DuplicateIssues, s_BackgroundScanTask
        static readonly List<SkillDefinition> s_CandidateSkills = new();
        static readonly List<SkillFileIssue> s_DuplicateIssues = new();

        // Filtered (opt-in allowed) read-only snapshot; null means dirty, rebuilt lazily in GetFilteredSnapshotLocked().
        static IReadOnlyDictionary<string, SkillDefinition> s_FilteredSkillsCache;

        // Optional filter applied when building the snapshot. Set from Editor code via SetSkillFilter.
        // Volatile so reads outside s_Lock see the latest value without a full lock.
        static volatile Func<SkillDefinition, bool> s_SkillFilter;

        // Written under s_Lock to keep the read-modify-write atomic; volatile so readers outside the lock
        // (IsLoadComplete, GetSkills, GetSkillsAsync) always see the latest value.
        static volatile Task s_BackgroundScanTask = Task.CompletedTask;

        /// <summary>
        /// True once the background skill scan has finished.
        /// </summary>
        internal static bool IsLoadComplete => s_BackgroundScanTask.IsCompleted;

        /// <summary>
        /// Called by SkillsScanner when a background scan starts, so <see cref="GetSkills"/> knows to wait for it.
        /// Replaces any previously registered task; callers are responsible for composing concurrent scans
        /// (e.g. <c>Task.WhenAll</c>) before registering. Superseded scans are already cancelled and their
        /// results discarded, so there is no value in continuing to wait for them.
        /// </summary>
        internal static void RegisterBackgroundScan(Task task)
        {
            lock (s_Lock)
                s_BackgroundScanTask = task ?? Task.CompletedTask;
        }

        // Deduplicates same-name candidates by source priority (User > Project > Package),
        // applies the opt-in filter ("Allowed" skills), and returns the resulting name->skill snapshot.
        // Info issue emitted when a name appears in multiple locations; Warning when multiple pass the filter (one wins).
        // Must be called with s_Lock held.
        static IReadOnlyDictionary<string, SkillDefinition> GetFilteredSnapshotLocked()
        {
            Debug.Assert(Monitor.IsEntered(s_Lock), "GetFilteredSnapshotLocked must be called with s_Lock held.");
            if (s_FilteredSkillsCache != null)
                return s_FilteredSkillsCache;

            var filter = s_SkillFilter; // read volatile - safe outside lock
            s_DuplicateIssues.Clear();

            var byName = new Dictionary<string, List<SkillDefinition>>(StringComparer.Ordinal);
            foreach (var skill in s_CandidateSkills)
            {
                if (!byName.TryGetValue(skill.MetaData.Name, out var bucket))
                    byName[skill.MetaData.Name] = bucket = new List<SkillDefinition>();
                bucket.Add(skill);
            }

            var result = new Dictionary<string, SkillDefinition>(StringComparer.Ordinal);

            foreach (var kv in byName)
            {
                var name = kv.Key;
                var candidates = kv.Value;

                if (candidates.Count > 1)
                {
                    var paths = BuildPathList(candidates);
                    s_DuplicateIssues.Add(new SkillFileIssue(name, candidates[0].Path,
                        $"<b>Skill found in multiple locations:</b> '{name}'\n\n{paths}",
                        SkillFileIssue.ErrorLevel.Info));
                }

                var allowed = new List<SkillDefinition>(candidates.Count);
                foreach (var skill in candidates)
                {
                    if (filter == null || filter(skill))
                        allowed.Add(skill);
                }

                if (allowed.Count == 0)
                    continue;

                if (allowed.Count == 1)
                {
                    result[name] = allowed[0];
                    continue;
                }

                allowed.Sort(ComparePriority);
                result[name] = allowed[0];
                var winner = allowed[0];
                var sb = new System.Text.StringBuilder();
                sb.Append($"<b>Allowed name clash:</b> '{name}'\n\n");
                sb.Append($"<b>Active skill:</b> [{SourceLabel(winner)}] {winner.Path ?? "(no path)"}\n\n");
                for (int i = 1; i < allowed.Count; i++)
                    sb.Append($"<b>Overridden:</b> [{SourceLabel(allowed[i])}] {allowed[i].Path ?? "(no path)"}\n\n");
                sb.Append("<b>Priority:</b> User > Project > Package; ties resolved alphabetically by path.");
                s_DuplicateIssues.Add(new SkillFileIssue(name, winner.Path,
                    sb.ToString(),
                    SkillFileIssue.ErrorLevel.Warning));
            }

            return s_FilteredSkillsCache = new ReadOnlyDictionary<string, SkillDefinition>(result);
        }

        static string BuildPathList(List<SkillDefinition> skills)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < skills.Count; i++)
            {
                if (i > 0) sb.Append("\n");
                sb.Append(skills[i].Path ?? "(no path)");
            }
            return sb.ToString();
        }

        static int ComparePriority(SkillDefinition a, SkillDefinition b)
        {
            var pa = GetSourcePriority(a);
            var pb = GetSourcePriority(b);
            if (pa != pb) return pa.CompareTo(pb);
            // Tie: alphabetical by normalized path for deterministic winner
            return string.Compare(
                a.Path?.Replace('\\', '/') ?? "",
                b.Path?.Replace('\\', '/') ?? "",
                StringComparison.OrdinalIgnoreCase);
        }

        static int GetSourcePriority(SkillDefinition skill)
        {
            if (skill.Tags.Contains(SkillRegistryTags.User)) return 0;
            if (skill.Tags.Contains(SkillRegistryTags.Project)) return 1;
            if (skill.Tags.Contains(SkillRegistryTags.Package)) return 2;
            return 3; // Internal or API-registered skills
        }

        static string SourceLabel(SkillDefinition skill)
        {
            if (skill.Tags.Contains(SkillRegistryTags.User)) return "User";
            if (skill.Tags.Contains(SkillRegistryTags.Project)) return "Project";
            if (skill.Tags.Contains(SkillRegistryTags.Package)) return "Package";
            return "Internal";
        }

        /// <summary>
        /// Returns all registered skills, waiting up to <c>k_GetSkillsTimeoutMs</c> for any in-progress scan.
        /// On timeout, returns partial results and logs a warning; <see cref="IsLoadComplete"/> stays false.
        /// </summary>
        public static IReadOnlyDictionary<string, SkillDefinition> GetSkills()
        {
            var task = s_BackgroundScanTask; // volatile read, atomic without s_Lock
            if (!task.IsCompleted)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    var finished = task.Wait(k_GetSkillsTimeoutMs);
                    stopwatch.Stop();
                    if (finished)
                        InternalLog.Log($"[SkillsRegistry] GetSkills() waited {stopwatch.ElapsedMilliseconds}ms for background scan");
                    else
                        UnityEngine.Debug.LogWarning($"[SkillsRegistry] GetSkills() timed out after {stopwatch.ElapsedMilliseconds}ms - returning partial results. Check the skill folder(s) for slow or unreachable drives.");
                }
                catch (AggregateException ex)
                {
                    stopwatch.Stop();
                    UnityEngine.Debug.LogWarning($"[SkillsRegistry] GetSkills(): background scan faulted ({ex.InnerException?.Message}) - returning partial results.");
                }
            }

            lock (s_Lock)
                return GetFilteredSnapshotLocked();
        }

        /// <summary>
        /// Asynchronously waits for any in-progress scan and returns a snapshot of registered skills.
        /// Prefer over <see cref="GetSkills"/> when calling from an async context or the main thread.
        /// </summary>
        internal static async Task<IReadOnlyDictionary<string, SkillDefinition>> GetSkillsAsync()
        {
            var task = s_BackgroundScanTask; // volatile read, atomic without s_Lock
            if (!task.IsCompleted)
            {
                var winner = await Task.WhenAny(task, Task.Delay(k_GetSkillsTimeoutMs)).ConfigureAwait(false);
                if (winner != task)
                    UnityEngine.Debug.LogWarning($"[SkillsRegistry] GetSkillsAsync() timed out after {k_GetSkillsTimeoutMs}ms - returning partial results. Check the skill folder(s) for slow or unreachable drives.");
                else if (task.IsFaulted)
                    UnityEngine.Debug.LogWarning($"[SkillsRegistry] GetSkillsAsync(): background scan faulted ({task.Exception?.InnerException?.Message}) - returning partial results.");
                else
                    InternalLog.Log($"[SkillsRegistry] GetSkillsAsync() waited for background scan to complete");
            }

            lock (s_Lock)
                return GetFilteredSnapshotLocked();
        }

        /// <summary>
        /// Returns a snapshot of currently registered skills without waiting. See <see cref="IsLoadComplete"/>.
        /// Any active skill filter is applied.
        /// </summary>
        internal static IReadOnlyDictionary<string, SkillDefinition> GetSkillsNoWait()
        {
            lock (s_Lock)
                return GetFilteredSnapshotLocked();
        }

        /// <summary>
        /// Returns all registered skill candidates, including duplicates, bypassing the opt-in filter.
        /// Use for settings UI and new-skill detection where all skills must be visible regardless of allowed state.
        /// Thread-safe.
        /// </summary>
        internal static IReadOnlyList<SkillDefinition> GetAllSkillsNoWait()
        {
            lock (s_Lock)
                return s_CandidateSkills.AsReadOnly();
        }

        /// <summary>
        /// Returns issues produced during the last dedup pass (Info = duplicates exist, Warning = conflict among allowed skills).
        /// Triggers a cache rebuild if the snapshot is dirty. Thread-safe.
        /// </summary>
        internal static IReadOnlyList<SkillFileIssue> GetDuplicateIssues()
        {
            lock (s_Lock)
            {
                GetFilteredSnapshotLocked(); // ensure cache is fresh
                return s_DuplicateIssues.AsReadOnly();
            }
        }

        /// <summary>
        /// Sets an optional per-skill filter applied when building the <see cref="GetSkills"/> snapshot.
        /// Pass <c>null</c> to remove filtering. Called from Editor code to enforce the opt-in allowlist.
        /// Thread-safe; invalidates the cached snapshot so callers receive fresh results.
        /// </summary>
        internal static void SetSkillFilter(Func<SkillDefinition, bool> filter)
        {
            s_SkillFilter = filter; // volatile write
            lock (s_Lock)
                s_FilteredSkillsCache = null;
        }

        /// <summary>
        /// Invalidates the cached skills snapshot so it is rebuilt on the next access.
        /// Call after the external filter's allowlist changes without skills being added or removed.
        /// </summary>
        internal static void InvalidateCache()
        {
            lock (s_Lock)
                s_FilteredSkillsCache = null;
        }

        /// <summary>
        /// Registers a single skill. Thread-safe.
        /// </summary>
        public static void RegisterSkill(SkillDefinition skill)
        {
            if (skill == null || string.IsNullOrEmpty(skill.MetaData.Name))
                return;

            lock (s_Lock)
                AddSkillLocked(skill);
        }

        static void AddSkillLocked(SkillDefinition skill)
        {
            Debug.Assert(Monitor.IsEntered(s_Lock), "AddSkillLocked must be called with s_Lock held.");
            if (skill == null || string.IsNullOrEmpty(skill.MetaData.Name))
                return;

            s_CandidateSkills.Add(skill);
            s_FilteredSkillsCache = null;
        }

        /// <summary>
        /// Swaps skills by removing all with one kind of tag and adding skills. Thread-safe.
        /// </summary>
        internal static void ReplaceSkillsByTag(string tag, List<SkillDefinition> newSkills)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            lock (s_Lock)
            {
                RemoveByTagLocked(tag);
                AddSkillsLocked(newSkills);
            }
        }

        /// <summary>
        /// Removes all skills with the given tag. Thread-safe.
        /// </summary>
        public static void RemoveByTag(string tag)
        {
            if (string.IsNullOrEmpty(tag))
                return;

            lock (s_Lock)
                RemoveByTagLocked(tag);
        }

        static void RemoveByTagLocked(string tag)
        {
            Debug.Assert(Monitor.IsEntered(s_Lock), "RemoveByTagLocked must be called with s_Lock held.");
            var before = s_CandidateSkills.Count;
            s_CandidateSkills.RemoveAll(s => s.Tags.Contains(tag));
            if (s_CandidateSkills.Count != before)
                s_FilteredSkillsCache = null;
        }

        /// <summary>
        /// Removes all registered skills. Thread-safe.
        /// </summary>
        public static void Clear()
        {
            lock (s_Lock)
            {
                s_CandidateSkills.Clear();
                s_FilteredSkillsCache = null;
            }
        }

        /// <summary>
        /// Adds a batch of skills. Invalid skills are skipped with a warning. Thread-safe.
        /// </summary>
        public static void AddSkills(List<SkillDefinition> skills)
        {
            if (skills == null || skills.Count == 0)
                return;

            lock (s_Lock)
                AddSkillsLocked(skills);
        }

        static void AddSkillsLocked(List<SkillDefinition> skills)
        {
            Debug.Assert(Monitor.IsEntered(s_Lock), "AddSkillsLocked must be called with s_Lock held.");
            if (skills == null)
                return;
            foreach (var skill in skills)
            {
                if (skill != null && skill.IsValid)
                    AddSkillLocked(skill);
                else
                    InternalLog.LogWarning("[SkillsRegistry] Skipped NULL, unnamed, or otherwise invalid skill when adding skills to registry. Look at any previous logs for failed SkillDefinition building steps.");
            }
        }
    }
}
