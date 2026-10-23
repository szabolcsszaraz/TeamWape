using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Skills;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    static partial class AssistantEditorPreferences
    {
        // EditorPrefs keys use composite "name:normalizedPath"; see CompositeKey() / NormalizePath().
        // Each skill instance gets its own allowed/seen state even when multiple share the same name.
        const string k_SkillSeenPrefix = k_SettingsPrefix + "SkillSeen.";
        const string k_SkillAllowedPrefix = k_SettingsPrefix + "SkillAllowed.";

        // Global key: user-scoped skills (AppData), shared across all projects on this machine.
        const string k_CurrentSkillKeysKey = k_SettingsPrefix + "CurrentSkillKeys";
        // Project-scoped key: suffixed with a stable hash of dataPath to isolate per-project.
        const string k_CurrentProjectSkillKeysKeyPrefix = k_SettingsPrefix + "CurrentProjectSkillKeys.";

        const string k_SkillsAwaitingNotificationKey = k_SettingsPrefix + "SkillsAwaitingNotification";
        const string k_ProjectSkillsAwaitingNotificationKeyPrefix = k_SettingsPrefix + "ProjectSkillsAwaitingNotification.";

        const string k_NotifyOnNewSkillsKey = k_SettingsPrefix + "NotifyOnNewSkills";

        // Hash128.Compute is deterministic across processes and domain reloads; string.GetHashCode() is not.
        static readonly string s_ProjectSuffix = Hash128.Compute(Application.dataPath).ToString();

        static string CurrentProjectSkillKeysKey => k_CurrentProjectSkillKeysKeyPrefix + s_ProjectSuffix;
        static string ProjectSkillsAwaitingNotificationKey => k_ProjectSkillsAwaitingNotificationKeyPrefix + s_ProjectSuffix;

        // Immutable HashSet snapshots keyed by composite key; replaced atomically so IsSkillAllowedFilter
        // can read from any thread without a lock (HashSet reads are safe on a frozen instance).
        static volatile HashSet<string> s_AllowedSkillNamesCache;

        // Ordered list of display names for skills that have been detected as new but not yet dismissed
        // by the user. Persisted to EditorPrefs so the notification panel survives domain reloads.
        static List<string> s_SkillsAwaitingNotification;

        public static event System.Action SkillAllowedStateChanged;

        /// <summary>
        /// Fired whenever the set of skills awaiting a "new skill" notification changes
        /// (skills added during a rescan, or dismissed by the user).
        /// </summary>
        public static event System.Action NewSkillsAwaitingNotificationChanged;

        // Resets the skill filter to null (no filtering). Use in test TearDown to undo any
        // per-test SetSkillFilter calls without assuming InitSkillOptIn fired during the test run.
        internal static void RestoreDefaultFilter() => SkillsRegistry.SetSkillFilter(null);

        [InitializeOnLoadMethod]
        static void InitSkillOptIn()
        {
            // Eagerly populate the cache (optimization: runs before GetSkills() or the settings UI accesses it).
            // IsSkillAllowedFilter also calls EnsureAllowedKeysLoaded() as a safety net if this fires late.
            EnsureAllowedKeysLoaded();

            // Pre-populate the awaiting-notification list so the notification panel is correct before the first rescan.
            EnsureAwaitingLoaded();

            SkillsRegistry.SetSkillFilter(IsSkillAllowedFilter);
            SkillsScanner.OnSkillsRescanned += OnSkillsRescanned;
        }

        // Reads persisted composite keys from EditorPrefs rather than live registry candidates,
        // so the filter is correct before the first scan completes. No-op if already loaded.
        // Use RebuildAllowedSkillsCache() after a scan to sync from actual registered skills.
        static void EnsureAllowedKeysLoaded()
        {
            if (s_AllowedSkillNamesCache != null)
                return;
            var storedKeys = LoadCurrentSkillKeys();
            var cache = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var key in storedKeys)
            {
                if (EditorPrefs.GetBool(k_SkillAllowedPrefix + key, false))
                    cache.Add(key);
            }
            s_AllowedSkillNamesCache = cache;
        }

        // Registered with SkillsRegistry.SetSkillFilter; gates which skills GetSkills() returns.
        // Internal and BuiltIn skills always pass; others must be explicitly allowed in EditorPrefs.
        static bool IsSkillAllowedFilter(SkillDefinition skill)
        {
            if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                return true;
            if (s_AllowedSkillNamesCache == null)
                EnsureAllowedKeysLoaded();
            var cache = s_AllowedSkillNamesCache;
            return cache != null && cache.Contains(CompositeKey(skill));
        }

        static bool IsUserSkill(SkillDefinition skill) => skill.Tags.Contains(SkillRegistryTags.User);

        static void OnSkillsRescanned()
        {
            var allSkills = SkillsRegistry.GetAllSkillsNoWait();

            // Always rebuild so GetSkills() is correctly filtered after every partial scan.
            RebuildAllowedSkillsCache(allSkills);

            // New-skill detection only runs once all scans have finished.
            if (!SkillsRegistry.IsLoadComplete)
                return;

            CleanupRemovedSkills(allSkills);

            EnsureAwaitingLoaded();

            // Prune any names no longer present in the current skill set.
            var currentNames = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var skill in allSkills)
            {
                if (!SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    currentNames.Add(skill.MetaData.Name);
            }
            var prunedCount = s_SkillsAwaitingNotification.RemoveAll(n => !currentNames.Contains(n));

            // Add new skills to the awaiting list without marking seen; set only to seen on explicit dismiss.
            // If notifications are off, mark seen immediately instead.
            var added = false;
            var newThisScan = new List<string>();
            var notifyEnabled = NotifyOnNewSkills;
            foreach (var skill in allSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;

                var key = CompositeKey(skill);
                if (!GetSkillSeen(key))
                {
                    if (notifyEnabled && !s_SkillsAwaitingNotification.Contains(skill.MetaData.Name))
                    {
                        s_SkillsAwaitingNotification.Add(skill.MetaData.Name);
                        newThisScan.Add(skill.MetaData.Name);
                        added = true;
                    }
                    else if (!notifyEnabled)
                    {
                        MarkSkillSeen(key);
                    }
                }
            }

            if (added || prunedCount > 0)
            {
                PersistAwaitingNotification();
                NewSkillsAwaitingNotificationChanged?.Invoke();
            }

            if (added)
            {
                const int maxListed = 10;
                var listed = newThisScan.Count <= maxListed
                    ? newThisScan
                    : newThisScan.GetRange(0, maxListed);
                var extra = newThisScan.Count - listed.Count;
                var nameList = string.Join(", ", listed) + (extra > 0 ? $", and {extra} more" : "");
                var header = newThisScan.Count == 1
                    ? "A new AI Assistant skill was discovered"
                    : $"{newThisScan.Count} new AI Assistant skills were discovered";
                var message = $"{header}: {nameList}. New skills are denied by default. Open Preferences/AI/Skills to review and allow them.";

                Debug.Log($"[AI Assistant] {message}");
            }
        }

        /// <summary>
        /// Returns a snapshot of the display names of skills that have been detected as new
        /// and not yet dismissed by the user.
        /// </summary>
        public static IReadOnlyList<string> GetNewSkillsAwaitingNotification()
        {
            EnsureAwaitingLoaded();
            return s_SkillsAwaitingNotification;
        }

        /// <summary>
        /// Gets or sets whether the user wants to be notified when new skills are discovered.
        /// Defaults to <c>true</c>. When set to <c>false</c>, newly discovered skills are
        /// immediately marked as seen and will not accumulate in the awaiting-notification list.
        /// </summary>
        public static bool NotifyOnNewSkills
        {
            get => EditorPrefs.GetBool(k_NotifyOnNewSkillsKey, true);
            set
            {
                if (NotifyOnNewSkills != value)
                    EditorPrefs.SetBool(k_NotifyOnNewSkillsKey, value);
            }
        }

        /// <summary>
        /// Marks all currently awaiting skills as seen and clears the notification list.
        /// Skills remain in their current allowed/denied state.
        /// Fires <see cref="NewSkillsAwaitingNotificationChanged"/> if the list was non-empty.
        /// </summary>
        public static void DismissNewSkillsNotification()
        {
            EnsureAwaitingLoaded();
            if (s_SkillsAwaitingNotification.Count == 0)
                return;

            // Mark every awaiting skill as seen so it is not re-added on next rescan.
            // Using a HashSet ensures all skills sharing a given name are marked seen.
            var awaitingNames = new HashSet<string>(s_SkillsAwaitingNotification, System.StringComparer.Ordinal);
            var allSkills = SkillsRegistry.GetAllSkillsNoWait();

            foreach (var skill in allSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;
                if (awaitingNames.Contains(skill.MetaData.Name))
                    MarkSkillSeen(CompositeKey(skill));
            }

            s_SkillsAwaitingNotification.Clear();
            PersistAwaitingNotification();
            NewSkillsAwaitingNotificationChanged?.Invoke();
        }

        /// <summary>
        /// Enables all skills that are currently awaiting a notification, marks them as seen, and clears the notification list.
        /// Fires both <see cref="SkillAllowedStateChanged"/> and <see cref="NewSkillsAwaitingNotificationChanged"/>.
        /// </summary>
        public static void EnableAllNewSkillsAndDismiss()
        {
            EnsureAwaitingLoaded();
            if (s_SkillsAwaitingNotification.Count == 0)
                return;

            var awaitingNames = new HashSet<string>(s_SkillsAwaitingNotification, System.StringComparer.Ordinal);
            var allSkills = SkillsRegistry.GetAllSkillsNoWait();

            foreach (var skill in allSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;
                if (!awaitingNames.Contains(skill.MetaData.Name))
                    continue;

                var key = CompositeKey(skill);
                EditorPrefs.SetBool(k_SkillAllowedPrefix + key, true);
                MarkSkillSeen(key);
            }

            // Rebuild the allowed-skills cache once for all the changes above.
            RebuildAllowedSkillsCache(allSkills);
            SkillAllowedStateChanged?.Invoke();

            s_SkillsAwaitingNotification.Clear();
            PersistAwaitingNotification();
            NewSkillsAwaitingNotificationChanged?.Invoke();
        }

        static void EnsureAwaitingLoaded()
        {
            if (s_SkillsAwaitingNotification != null)
                return;

            // Merge-on-read: union global and project-scoped notification lists, global first.
            var merged = new List<string>();
            var encountered = new HashSet<string>(System.StringComparer.Ordinal);

            var globalRaw = EditorPrefs.GetString(k_SkillsAwaitingNotificationKey, "");
            if (!string.IsNullOrEmpty(globalRaw))
            {
                foreach (var name in globalRaw.Split('\n'))
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (encountered.Add(name))
                        merged.Add(name);
                }
            }

            var projectRaw = EditorPrefs.GetString(ProjectSkillsAwaitingNotificationKey, "");
            if (!string.IsNullOrEmpty(projectRaw))
            {
                foreach (var name in projectRaw.Split('\n'))
                {
                    if (string.IsNullOrEmpty(name)) continue;
                    if (encountered.Add(name))
                        merged.Add(name);
                }
            }

            s_SkillsAwaitingNotification = merged;
        }

        static void PersistAwaitingNotification()
        {
            // Split-on-write: partition into global vs project-scoped buckets so other projects
            // only see their own notifications. Unknown names fall back to project-scoped.
            var allSkills = SkillsRegistry.GetAllSkillsNoWait();
            var nameIsUser = new Dictionary<string, bool>(System.StringComparer.Ordinal);
            foreach (var skill in allSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;
                var name = skill.MetaData.Name;
                var isUser = IsUserSkill(skill);
                if (!nameIsUser.ContainsKey(name) || isUser)
                    nameIsUser[name] = isUser;
            }

            var globalNames = new List<string>();
            var projectNames = new List<string>();
            foreach (var name in s_SkillsAwaitingNotification)
            {
                if (nameIsUser.TryGetValue(name, out var isUser) && isUser)
                    globalNames.Add(name);
                else
                    projectNames.Add(name);
            }

            EditorPrefs.SetString(k_SkillsAwaitingNotificationKey,
                globalNames.Count > 0 ? string.Join("\n", globalNames) : "");
            EditorPrefs.SetString(ProjectSkillsAwaitingNotificationKey,
                projectNames.Count > 0 ? string.Join("\n", projectNames) : "");
        }

        static void RebuildAllowedSkillsCache(IReadOnlyList<SkillDefinition> skills = null)
        {
            skills ??= SkillsRegistry.GetAllSkillsNoWait();
            var newCache = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var skill in skills)
            {
                if (!SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags) && GetSkillAllowed(skill))
                    newCache.Add(CompositeKey(skill));
            }
            s_AllowedSkillNamesCache = newCache;
            SkillsRegistry.InvalidateCache();
        }

        public static bool GetSkillAllowed(SkillDefinition skill)
            => EditorPrefs.GetBool(k_SkillAllowedPrefix + CompositeKey(skill), false);

        public static void SetSkillAllowed(SkillDefinition skill, bool allowed)
        {
            if (GetSkillAllowed(skill) == allowed)
                return;

            EditorPrefs.SetBool(k_SkillAllowedPrefix + CompositeKey(skill), allowed);

            // Copy-on-write update so in-flight filter reads remain safe.
            var compositeKey = CompositeKey(skill);
            var current = s_AllowedSkillNamesCache;
            var updated = current != null
                ? new HashSet<string>(current, System.StringComparer.Ordinal)
                : new HashSet<string>(System.StringComparer.Ordinal);

            if (allowed)
                updated.Add(compositeKey);
            else
                updated.Remove(compositeKey);

            s_AllowedSkillNamesCache = updated;

            SkillsRegistry.InvalidateCache();
            SkillAllowedStateChanged?.Invoke();
        }

        static bool GetSkillSeen(string compositeKey) => EditorPrefs.GetBool(k_SkillSeenPrefix + compositeKey, false);
        static void MarkSkillSeen(string compositeKey) => EditorPrefs.SetBool(k_SkillSeenPrefix + compositeKey, true);

        // Forgets skills no longer present so they are rediscovered as new if re-added.
        static void CleanupRemovedSkills(IReadOnlyList<SkillDefinition> currentSkills)
        {
            var previousGlobal = LoadCurrentSkillKeysFromKey(k_CurrentSkillKeysKey);
            var previousProject = LoadCurrentSkillKeysFromKey(CurrentProjectSkillKeysKey);
            var previous = new HashSet<string>(previousGlobal, System.StringComparer.Ordinal);
            previous.UnionWith(previousProject);
            var currentGlobal = new HashSet<string>(System.StringComparer.Ordinal);
            var currentProject = new HashSet<string>(System.StringComparer.Ordinal);
            foreach (var skill in currentSkills)
            {
                if (SkillRegistryTags.IsInternalOrBuiltIn(skill.Tags))
                    continue;
                var key = CompositeKey(skill);
                if (IsUserSkill(skill))
                    currentGlobal.Add(key);
                else
                    currentProject.Add(key);
            }

            var currentAll = new HashSet<string>(currentGlobal, System.StringComparer.Ordinal);
            currentAll.UnionWith(currentProject);

            var anyRemoved = false;
            foreach (var key in previous)
            {
                if (currentAll.Contains(key))
                    continue;
                EditorPrefs.DeleteKey(k_SkillSeenPrefix + key);
                EditorPrefs.DeleteKey(k_SkillAllowedPrefix + key);
                anyRemoved = true;
            }

            if (!currentGlobal.SetEquals(previousGlobal))
                EditorPrefs.SetString(k_CurrentSkillKeysKey, string.Join("\n", currentGlobal));

            if (!currentProject.SetEquals(previousProject))
                EditorPrefs.SetString(CurrentProjectSkillKeysKey, string.Join("\n", currentProject));

            if (anyRemoved)
                RebuildAllowedSkillsCache(currentSkills);
        }

        // Merge-on-read: union global + project keys; pre-split keys in global are re-bucketed on next cleanup.
        static HashSet<string> LoadCurrentSkillKeys()
        {
            var result = LoadCurrentSkillKeysFromKey(k_CurrentSkillKeysKey);
            result.UnionWith(LoadCurrentSkillKeysFromKey(CurrentProjectSkillKeysKey));
            return result;
        }

        static HashSet<string> LoadCurrentSkillKeysFromKey(string editorPrefsKey)
        {
            var raw = EditorPrefs.GetString(editorPrefsKey, "");
            return string.IsNullOrEmpty(raw)
                ? new HashSet<string>(System.StringComparer.Ordinal)
                : new HashSet<string>(raw.Split('\n'), System.StringComparer.Ordinal);
        }

        static string CompositeKey(SkillDefinition skill)
            => skill.MetaData.Name + ":" + NormalizePath(skill.Path);

        static string NormalizePath(string path)
            => string.IsNullOrEmpty(path) ? "" : Path.GetFullPath(path).ToLowerInvariant().Replace('\\', '/');
    }
}
