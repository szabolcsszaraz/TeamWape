using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEditor.PackageManager;

namespace Unity.AI.Assistant.Editor
{

    /// <summary>
    /// Decides when to trigger skill rescans: on load, on user-folder file changes (via <see cref="System.IO.FileSystemWatcher"/>),
    /// and on project asset changes (via <see cref="SkillsProjectAssetPostprocessor"/>).
    /// Delegates all scan execution to <see cref="SkillsScanner"/>.
    /// </summary>
    [InitializeOnLoad]
    static class SkillsRegistryInitializer
    {
        static readonly long k_DebounceUserFolderScanDelayTicks = TimeSpan.FromMilliseconds(500).Ticks;
        
        static FileSystemWatcher s_UserFolderWatcher;
        static string s_WatchedUserFolder;
        static volatile bool s_UserFolderChangePending;
        static long s_UserFolderLastChangeTicks;

        static SkillsRegistryInitializer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += StopUserFolderWatcher;
            Events.registeredPackages -= OnPackagesChanged;
            Events.registeredPackages += OnPackagesChanged;
            SkillsScanner.RescanAll();
            StartUserFolderWatcher(SkillsScanner.UserAppDataFolder);
        }

        internal static void StartUserFolderWatcher(string folder)
        {
            if (s_WatchedUserFolder == folder && s_UserFolderWatcher != null)
                return;

            StopUserFolderWatcher();

            if (!Directory.Exists(folder))
                return;

            s_UserFolderWatcher = new FileSystemWatcher(folder)
            {
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite,
                EnableRaisingEvents = true,
            };
            s_UserFolderWatcher.Created += OnUserFolderChanged;
            s_UserFolderWatcher.Changed += OnUserFolderChanged;
            s_UserFolderWatcher.Deleted += OnUserFolderChanged;
            s_UserFolderWatcher.Renamed += OnUserFolderChanged;
            s_UserFolderWatcher.Error   += OnUserFolderWatcherError;

            s_WatchedUserFolder = folder;
            EditorApplication.update += OnEditorUpdate;
        }

        static void StopUserFolderWatcher()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (s_UserFolderWatcher == null)
                return;

            s_UserFolderWatcher.EnableRaisingEvents = false;
            s_UserFolderWatcher.Dispose();
            s_UserFolderWatcher = null;
            s_WatchedUserFolder = null;
        }

        // Called from background thread - only sets flags, no Unity API calls
        static void OnUserFolderChanged(object sender, FileSystemEventArgs e)
        {
            Interlocked.Exchange(ref s_UserFolderLastChangeTicks, DateTime.Now.Ticks);
            s_UserFolderChangePending = true;
        }

        // Called from background thread when the watcher encounters an error (e.g. a watched
        // subdirectory was deleted while being enumerated).
        static void OnUserFolderWatcherError(object sender, ErrorEventArgs e)
        {
            InternalLog.LogWarning($"[SkillsRegistryInitializer] FileSystemWatcher error: {e.GetException()?.Message}");
        }

        // Called on the main thread when packages are added, removed, or updated during the Editor session.
        static void OnPackagesChanged(PackageRegistrationEventArgs args)
        {
            foreach (var p in args.added)
            {
                if (!string.IsNullOrEmpty(p.resolvedPath) && Directory.Exists(Path.Combine(p.resolvedPath, SkillsScanner.PackageSkillsFolder)))
                {
                    SkillsScanner.RescanPackages();
                    return;
                }
            }
            foreach (var p in args.removed)
            {
                if (string.IsNullOrEmpty(p.resolvedPath) || SkillsScanner.PackageHadSkills(p.resolvedPath))
                {
                    SkillsScanner.RescanPackages();
                    return;
                }
            }
            // Can't inspect the old resolved path, so rescan unconditionally on updates.
            if (args.changedTo.Count > 0)
                SkillsScanner.RescanPackages();
        }

        // Called on the main thread via EditorApplication.update
        static void OnEditorUpdate()
        {
            if (!s_UserFolderChangePending)
                return;
            if (DateTime.Now.Ticks - Interlocked.Read(ref s_UserFolderLastChangeTicks) < k_DebounceUserFolderScanDelayTicks)
                return;

            s_UserFolderChangePending = false;
            SkillsScanner.RescanUser();
        }

        // Called from SkillsProjectAssetPostprocessor on the main thread after any asset import
        internal static void OnProjectAssetsChanged(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            if (!AreProjectSkillsAffected(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths, SkillsScanner.LastProjectSkillPaths))
                return;

            SkillsScanner.RescanProject();
        }

        internal static bool AreProjectSkillsAffected(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, HashSet<string> projectSkillsPaths)
        {
            if (ArePathsAffected(projectSkillsPaths, importedAssets))      return true;
            if (ArePathsAffected(projectSkillsPaths, deletedAssets))       return true;
            if (ArePathsAffected(projectSkillsPaths, movedAssets))         return true;
            if (ArePathsAffected(projectSkillsPaths, movedFromAssetPaths)) return true;
            return false;
        }

        static bool ArePathsAffected(HashSet<string> projectSkillsPaths, IEnumerable<string> allPaths)
        {
            foreach (var path in allPaths)
            {
                if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    continue;

                // Any SKILL.md appearing or disappearing anywhere in Assets triggers a rescan
                if (path.EndsWith("/SKILL.md", StringComparison.OrdinalIgnoreCase))
                    return true;

                // A known skill folder itself was moved or deleted
                if (projectSkillsPaths.Contains(path))
                    return true;

                // A file inside a known skill folder, at most one subfolder deep
                if (IsWithinSkillFolderScope(path, projectSkillsPaths))
                    return true;
            }

            return false;
        }

        // Returns true if assetPath is a direct child or one-level-deep subfolder child
        // of any folder that contained a SKILL.md in the last scan
        internal static bool IsWithinSkillFolderScope(string assetPath, HashSet<string> knownFolders)
        {
            var lastSlash = assetPath.LastIndexOf('/');
            if (lastSlash < 0)
                return false;

            var parent = assetPath.Substring(0, lastSlash);
            if (knownFolders.Contains(parent))
                return true;

            var secondLastSlash = parent.LastIndexOf('/');
            if (secondLastSlash < 0)
                return false;

            var grandparent = parent.Substring(0, secondLastSlash);
            return knownFolders.Contains(grandparent);
        }
    }

    class SkillsProjectAssetPostprocessor : AssetPostprocessor
    {
        static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            SkillsRegistryInitializer.OnProjectAssetsChanged(importedAssets, deletedAssets, movedAssets, movedFromAssetPaths);
        }
    }
}
