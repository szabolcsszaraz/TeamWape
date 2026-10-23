using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Skills;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UpmPackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEngine;

namespace Unity.AI.Assistant.Editor
{
    /// <summary>
    /// Executes skill folder scans and owns all scan results and state.
    /// Triggered by <see cref="SkillsRegistryInitializer"/>. Scans run on background threads;
    /// only the <see cref="AssetDatabase"/> query for Project skills stays on the main thread.
    /// </summary>
    static class SkillsScanner
    {
        // Scans running longer than this are flagged with a visible warning in the settings view.
        const int k_SlowScanThresholdSecs = 2;
        internal const string PackageSkillsFolder = "AIAssistantSkills";

        internal static DateTime LastRescanTime { get; private set; }
        static readonly Stopwatch s_RescanElapsed = new();

        static readonly string s_UserAppDataFolder = ComputeUserAppDataFolder();
        internal static string UserAppDataFolder => s_UserAppDataFolder;

        internal static readonly SkillsLoadResults LoadResults = new();

        internal static event Action OnSkillsRescanned;

        // Tracks project skill paths from the last scan - read by SkillsRegistryInitializer for change detection.
        static readonly HashSet<string> s_LastProjectSkillPaths = new(StringComparer.OrdinalIgnoreCase);
        internal static HashSet<string> LastProjectSkillPaths => s_LastProjectSkillPaths;

        internal static bool InternalSkillsEnabled { get; private set; }

        static CancellationTokenSource s_ProjectScanCts = new();
        static CancellationTokenSource s_UserScanCts = new();
        static CancellationTokenSource s_PackageScanCts = new();

        /// <summary>
        /// A snapshot of a single in-flight (or recently completed) background scan.
        /// </summary>
        internal readonly struct ScanInfo
        {
            internal readonly string Category;
            internal readonly string Folder;
            readonly Task m_Task;

            internal ScanInfo(string category, string folder, Task task)
            {
                Category = category;
                Folder = folder;
                m_Task = task;
            }

            internal Task Task => m_Task;
            internal bool IsComplete => m_Task == null || m_Task.IsCompleted;
        }

        static readonly object s_ScanInfoLock = new();
        static readonly List<ScanInfo> s_CurrentScans = new();

        static readonly object s_PackageSkillFoldersLock = new();
        static readonly HashSet<string> s_KnownPackageSkillFolders = new(StringComparer.OrdinalIgnoreCase);

        static List<SkillFileIssue> s_TimeoutIssues;
        internal static List<SkillFileIssue> TimeoutIssues { get { lock (s_ScanInfoLock) return s_TimeoutIssues == null ? null : new List<SkillFileIssue>(s_TimeoutIssues); } }

        internal static bool PackageHadSkills(string resolvedPath)
        {
            if (string.IsNullOrEmpty(resolvedPath)) return false;
            var folder = Path.Combine(resolvedPath, PackageSkillsFolder);
            lock (s_PackageSkillFoldersLock)
                return s_KnownPackageSkillFolders.Contains(folder);
        }

        /// <summary>
        /// Atomically replaces the CancellationTokenSource, cancels and disposes the old one,
        /// and returns a token for the new scan. Thread-safe via Interlocked.Exchange.
        /// </summary>
        static CancellationToken BeginScan(ref CancellationTokenSource cts)
        {
            var newCts = new CancellationTokenSource();
            var old = Interlocked.Exchange(ref cts, newCts);
            old.Cancel();
            old.Dispose();
            return newCts.Token;
        }

        /// <summary>
        /// Fires a slow scan warning if the scan is still running after <see cref="k_SlowScanThresholdSecs"/>s.
        /// </summary>
        /// <param name="rescanTimestamp">Timestamp of the rescan starting this timer, used to detect if it has been superseded by a newer rescan.</param>
        static void ScheduleSlowScanWarning(DateTime rescanTimestamp)
        {
            Task.Delay(k_SlowScanThresholdSecs * 1000).ContinueWith(_ =>
            {
                // A newer rescan has superseded this one, or the scan finished within the threshold.
                if (LastRescanTime != rescanTimestamp || SkillsRegistry.IsLoadComplete)
                    return;

                var incompleteScans = GetIncompleteScans();
                if (incompleteScans.Count == 0)
                    return;

                foreach (var scan in incompleteScans)
                {
                    var issue = new SkillFileIssue(
                        $"Skill scan is slow ({scan.Category})",
                        scan.Folder,
                        $"Scanning has been running for over {k_SlowScanThresholdSecs}s. The folder may contain too many files or exist on a slow, network, or cloud-synced drive for example.",
                        SkillFileIssue.ErrorLevel.Critical);

                    lock (s_ScanInfoLock)
                    {
                        s_TimeoutIssues ??= new List<SkillFileIssue>();
                        s_TimeoutIssues.Add(issue);
                    }
                }

                var slowFolders = string.Join(", ", incompleteScans.Select(s => s.Folder));
                UnityEngine.Debug.LogWarning($"[SkillsScanner] Skill scan is slow (over {k_SlowScanThresholdSecs}s) - folder(s): {slowFolders}.");

                // ContinueWith runs on a thread-pool thread; Unity API calls require the main thread.
                EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
            });
        }

        internal static void ClearTimeoutIssues() { lock (s_ScanInfoLock) s_TimeoutIssues = null; }

        // Adds or replaces the tracked scan entry for the given category.
        static void TrackScan(string category, string folder, Task task)
        {
            lock (s_ScanInfoLock)
            {
                for (var i = 0; i < s_CurrentScans.Count; i++)
                {
                    if (s_CurrentScans[i].Category == category)
                    {
                        s_CurrentScans[i] = new ScanInfo(category, folder, task);
                        return;
                    }
                }
                s_CurrentScans.Add(new ScanInfo(category, folder, task));
            }
        }

        /// <summary>
        /// Returns a snapshot of background scans not yet completed.
        /// </summary>
        internal static IReadOnlyList<ScanInfo> GetIncompleteScans()
        {
            lock (s_ScanInfoLock)
                return s_CurrentScans.Where(s => !s.IsComplete).ToList();
        }

        // Returns a task that completes only when every currently tracked scan finishes.
        // s_CurrentScans holds one slot per category; TrackScan replaces, never appends duplicates.
        static Task GetCombinedScanTask()
        {
            lock (s_ScanInfoLock)
            {
                var tasks = s_CurrentScans
                    .Where(s => s.Task != null && !s.Task.IsCompleted)
                    .Select(s => s.Task)
                    .ToList();
                return tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
            }
        }

        /// <summary>
        /// Returns elapsed time and pending folder path(s) for in-progress scans, or null if all are done.
        /// </summary>
        internal static string GetPendingScansDescription()
        {
            if (SkillsRegistry.IsLoadComplete)
                return null;

            var incomplete = GetIncompleteScans();
            if (incomplete.Count == 0)
                return null;

            var elapsed = (int)s_RescanElapsed.Elapsed.TotalSeconds;
            var folders = string.Join(", ", incomplete.Select(s => s.Folder));
            return $"Skill scan did not finish after {elapsed}s - pending folder(s): {folders}.";
        }

        static string ComputeUserAppDataFolder()
        {
            var appDataRoot = Application.platform == RuntimePlatform.OSXEditor
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library", "Application Support")
                : Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            return Path.Combine(appDataRoot, "Unity", "AIAssistantSkills");
        }

        /// <summary>
        /// Kicks off asynchronous scans of both skill sources in parallel.
        /// Must be called from the main thread (AssetDatabase query for project paths).
        /// </summary>
        internal static void RescanAll()
        {
            LastRescanTime = DateTime.Now;
            s_RescanElapsed.Restart();
            ClearTimeoutIssues();
            var projectCt = BeginScan(ref s_ProjectScanCts);
            var userCt = BeginScan(ref s_UserScanCts);
            var packageCt = BeginScan(ref s_PackageScanCts);

            // AssetDatabase query must run on the main thread; file I/O is handed to a background thread.
            var projectFolder = Application.dataPath;
            var projectPaths = GatherProjectSkillPaths();
            var packageFolders = GatherPackageSkillFolders();
            var projectStopwatch = Stopwatch.StartNew();
            var projectTask = Task.Run(() => LoadProjectSkillsBackground(projectPaths, projectStopwatch, projectCt));

            var userStopwatch = Stopwatch.StartNew();
            var userTask = Task.Run(() => ScanUserAppDataFolderBackground(userStopwatch, userCt));

            var packageStopwatch = Stopwatch.StartNew();
            var packageTask = Task.Run(() => ScanPackageSkillsBackground(packageFolders, packageStopwatch, packageCt));

            TrackScan(SkillRegistryTags.Project, projectFolder, projectTask);
            TrackScan(SkillRegistryTags.User, UserAppDataFolder, userTask);
            TrackScan(SkillRegistryTags.Package, $"Packages/*/{PackageSkillsFolder}", packageTask);
            SkillsRegistry.RegisterBackgroundScan(GetCombinedScanTask());
            ScheduleSlowScanWarning(LastRescanTime);
            // Notify immediately so the UI can reflect that a scan is now in progress.
            // Each background task also fires OnSkillsRescanned on completion with final results.
            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        /// <summary>
        /// Asynchronously rescans only the Project (Assets/) skills.
        /// Must be called from the main thread.
        /// </summary>
        internal static void RescanProject()
        {
            LastRescanTime = DateTime.Now;
            s_RescanElapsed.Restart();
            ClearTimeoutIssues();
            var ct = BeginScan(ref s_ProjectScanCts);
            var projectFolder = Application.dataPath;
            var paths = GatherProjectSkillPaths();
            var stopwatch = Stopwatch.StartNew();
            var task = Task.Run(() => LoadProjectSkillsBackground(paths, stopwatch, ct));
            TrackScan(SkillRegistryTags.Project, projectFolder, task);
            SkillsRegistry.RegisterBackgroundScan(GetCombinedScanTask());
            ScheduleSlowScanWarning(LastRescanTime);
            // Notify immediately so the UI can reflect that a scan is now in progress.
            // The background task also fires OnSkillsRescanned on completion with final results.
            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        /// <summary>
        /// Asynchronously rescans only the User (AppData) skills. Must be called from the main thread.
        /// </summary>
        internal static void RescanUser()
        {
            LastRescanTime = DateTime.Now;
            s_RescanElapsed.Restart();
            ClearTimeoutIssues();
            var ct = BeginScan(ref s_UserScanCts);
            var stopwatch = Stopwatch.StartNew();
            var task = Task.Run(() => ScanUserAppDataFolderBackground(stopwatch, ct));
            TrackScan(SkillRegistryTags.User, UserAppDataFolder, task);
            SkillsRegistry.RegisterBackgroundScan(GetCombinedScanTask());
            ScheduleSlowScanWarning(LastRescanTime);
            // Notify immediately so the UI can reflect that a scan is now in progress.
            // The background task also fires OnSkillsRescanned on completion with final results.
            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        /// <summary>
        /// Queries AssetDatabase for SKILL.md paths, returns absolute paths for background loading on main thread.
        /// </summary>
        static List<string> GatherProjectSkillPaths()
        {
            var skillAssetPaths = AssetDatabase.FindAssets("t:TextAsset SKILL", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => Path.GetFileName(path) == "SKILL.md")
                .ToList();

            s_LastProjectSkillPaths.Clear();
            foreach (var ap in skillAssetPaths)
            {
                var folder = Path.GetDirectoryName(ap)?.Replace('\\', '/');
                if (!string.IsNullOrEmpty(folder))
                    s_LastProjectSkillPaths.Add(folder);
            }

            return skillAssetPaths
                .Select(path => Path.GetFullPath(Path.Combine(Application.dataPath, "..", path)))
                .ToList();
        }

        /// <summary>
        /// Finds all installed packages that contain a skills subfolder.
        /// Returns candidate folders - existence check deferred to the background thread.
        /// </summary>
        static List<string> GatherPackageSkillFolders()
        {
            return UpmPackageInfo.GetAllRegisteredPackages()
                .Where(p => !string.IsNullOrEmpty(p.resolvedPath))
                .Select(p => Path.Combine(p.resolvedPath, PackageSkillsFolder))
                .ToList();
        }

        /// <summary>
        /// Asynchronously rescans skills from all installed packages.
        /// </summary>
        internal static void RescanPackages()
        {
            LastRescanTime = DateTime.Now;
            s_RescanElapsed.Restart();
            ClearTimeoutIssues();
            var ct = BeginScan(ref s_PackageScanCts);
            var folders = GatherPackageSkillFolders();
            var stopwatch = Stopwatch.StartNew();
            var task = Task.Run(() => ScanPackageSkillsBackground(folders, stopwatch, ct));
            TrackScan(SkillRegistryTags.Package, $"Packages/*/{PackageSkillsFolder}", task);
            SkillsRegistry.RegisterBackgroundScan(GetCombinedScanTask());
            ScheduleSlowScanWarning(LastRescanTime);
            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        static void ScanPackageSkillsBackground(List<string> folders, Stopwatch stopwatch, CancellationToken ct)
        {
            var tag = SkillRegistryTags.Package;
            var allSkills = new List<SkillDefinition>();
            var allIssues = new List<SkillFileIssue>();

            var packagesWithSkills = 0;
            var foundFolders = new List<string>();
            foreach (var folder in folders)
            {
                if (ct.IsCancellationRequested) return;
                if (!Directory.Exists(folder)) continue;
                packagesWithSkills++;
                foundFolders.Add(folder);
                SkillUtils.LoadSkillsFromFolder(folder, tag, allSkills, allIssues);
            }

            // Abort if a newer scan has already started; its results will supersede ours.
            if (ct.IsCancellationRequested)
                return;

            lock (s_PackageSkillFoldersLock)
            {
                s_KnownPackageSkillFolders.Clear();
                s_KnownPackageSkillFolders.UnionWith(foundFolders);
            }

            SkillsRegistry.ReplaceSkillsByTag(tag, allSkills);
            LoadResults.StoreIssues(tag, allIssues);

            stopwatch.Stop();
            InternalLog.Log($"[SkillsScanner] 'Package' scan: {stopwatch.ElapsedMilliseconds}ms " +
                            $"({allSkills.Count} skills, {allIssues.Count} issues, {packagesWithSkills} package(s) with skills)");

            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        static void LoadProjectSkillsBackground(List<string> skillFiles, Stopwatch stopwatch, CancellationToken ct)
        {
            var tag = SkillRegistryTags.Project;
            var allSkills = new List<SkillDefinition>();
            var allIssues = new List<SkillFileIssue>();

            SkillUtils.LoadSkillFiles(skillFiles, tag, allSkills, allIssues);

            // Abort if a newer scan has already started; its results will supersede ours.
            if (ct.IsCancellationRequested)
                return;

            SkillsRegistry.ReplaceSkillsByTag(tag, allSkills);
            LoadResults.StoreIssues(tag, allIssues);

            stopwatch.Stop();
            InternalLog.Log($"[SkillsScanner] 'Project' scan: {stopwatch.ElapsedMilliseconds}ms " +
                            $"({allSkills.Count} skills, {allIssues.Count} issues)");

            // UI notification via main thread
            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        static void ScanUserAppDataFolderBackground(Stopwatch stopwatch, CancellationToken ct)
        {
            var tag = SkillRegistryTags.User;

            if (!Directory.Exists(UserAppDataFolder))
            {
                if (ct.IsCancellationRequested)
                    return;

                stopwatch.Stop();
                SkillsRegistry.RemoveByTag(tag);
                LoadResults.ClearIssues(tag);
                InternalLog.Log($"[SkillsScanner] 'User' scan: {stopwatch.ElapsedMilliseconds}ms (folder not found)");
                EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
                return;
            }

            var allSkills = new List<SkillDefinition>();
            var allIssues = new List<SkillFileIssue>();
            SkillUtils.LoadSkillsFromFolder(UserAppDataFolder, tag, allSkills, allIssues);

            // Abort if a newer scan has already started; its results will supersede ours.
            if (ct.IsCancellationRequested)
                return;

            SkillsRegistry.ReplaceSkillsByTag(tag, allSkills);
            LoadResults.StoreIssues(tag, allIssues);

            stopwatch.Stop();
            InternalLog.Log($"[SkillsScanner] 'User' scan: {stopwatch.ElapsedMilliseconds}ms " +
                            $"({allSkills.Count} skills, {allIssues.Count} issues)");

            EditorApplication.delayCall += () => OnSkillsRescanned?.Invoke();
        }

        /// <summary>
        /// Triggers a rescan of all skills.
        /// </summary>
        internal static void ForceRescan()
        {
            RescanAll();
            SkillsRegistryInitializer.StartUserFolderWatcher(UserAppDataFolder);
            OnSkillsRescanned?.Invoke();
        }

        internal static void CreateUserFolder()
        {
            try
            {
                Directory.CreateDirectory(UserAppDataFolder);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[SkillsScanner] CreateUserFolder: failed to create folder at {UserAppDataFolder}: {ex.Message}");
                return;
            }

            RescanUser();
            SkillsRegistryInitializer.StartUserFolderWatcher(UserAppDataFolder);
        }

        internal static void ShowInternalSkills(bool show, List<SkillFileIssue> issues = null)
        {
            InternalSkillsEnabled = show;
            if (show)
                LoadResults.StoreIssues(SkillRegistryTags.Internal, issues);
            else
                LoadResults.ClearIssues(SkillRegistryTags.Internal);
            OnSkillsRescanned?.Invoke();
        }
    }
}
