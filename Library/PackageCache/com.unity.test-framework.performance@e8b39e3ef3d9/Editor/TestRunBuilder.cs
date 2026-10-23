using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.PerformanceTesting.Data;
using Unity.PerformanceTesting.Editor;
using Unity.PerformanceTesting.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

[assembly: PrebuildSetup(typeof(TestRunBuilder))]
[assembly: PostBuildCleanup(typeof(TestRunBuilder))]

namespace Unity.PerformanceTesting.Editor
{
    internal class TestRunBuilder : IPrebuildSetup, IPostBuildCleanup, IPreprocessBuildWithReport, IPostprocessBuildWithReport
    {
        private const string cleanResources = "PT_ResourcesCleanup";
        private const bool EnableDetailedDiffLogging = true;

        // UUM-132359: Library/ cache for the Resources/PerformanceTestRun*.json .meta files, so their GUID survives across builds.
        private const string MetaCacheDir = "Library/PerformanceTesting/MetaCache";

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!ShouldWriteBuildFiles(report.summary.options))
            {
                // Sweep artifacts a previously aborted test build may have left
                // behind, so they don't get baked into a non-test player.
                Cleanup();
                return;
            }

            CreateResourcesFolder();

            // UUM-132359: restore the cached .meta before writing, so the asset keeps its GUID instead of getting a new one.
            RestoreCachedMeta(Utils.TestRunPath);
            RestoreCachedMeta(Utils.RunSettingsPath);

            var run = CreateBuildInfo();
            SaveToStorage(run, Utils.TestRunPath);

            var settings = new RunSettings(Environment.GetCommandLineArgs());
            SaveToStorage(settings, Utils.RunSettingsPath);
        }

        public void OnPostprocessBuild(BuildReport report)
        {
            if (ShouldWriteBuildFiles(report.summary.options))
            {
                // UUM-132359: stash the .meta before Cleanup() deletes it, so the next test build can reuse the same GUID.
                StashMeta(Utils.TestRunPath);
                StashMeta(Utils.RunSettingsPath);
            }

            // Runs unconditionally so a non-test build also sweeps up files
            // left behind by a previously aborted test build.
            Cleanup();
        }

        // The run info and settings files are only ever read by performance tests
        // executing inside the player, so builds without test assemblies never need them.
        internal static bool ShouldWriteBuildFiles(BuildOptions options)
        {
            return (options & BuildOptions.IncludeTestAssemblies) != 0;
        }

        public void Setup()
        {
            SessionState.SetBool(cleanResources, false);

            var run = CreateRunInfo();
            SaveToPrefs(run, Utils.PlayerPrefKeyRunJSON);

            var settings = new RunSettings(Environment.GetCommandLineArgs());
            SaveToPrefs(settings, Utils.PlayerPrefKeySettingsJSON);
        }

#if !UNITY_2021_1_OR_NEWER
        private static List<string> cachedDependencies;
#endif
        static List<string> GetPackageDependencies()
        {
#if !UNITY_2021_1_OR_NEWER
            if (cachedDependencies != null)
                return cachedDependencies;
#endif

            IEnumerable<UnityEditor.PackageManager.PackageInfo> packages;
#if !UNITY_2021_1_OR_NEWER
            var listRequest = UnityEditor.PackageManager.Client.List(true);
            while (!listRequest.IsCompleted)
                System.Threading.Thread.Sleep(10);
            if (listRequest.Status == UnityEditor.PackageManager.StatusCode.Failure)
                Debug.LogError("Failed to list local packages");
            packages = new List<UnityEditor.PackageManager.PackageInfo>(listRequest.Result);
#else
            packages = UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages();
#endif
            var reformated = packages.Select(p => $"{p.name}@{p.version}").OrderBy(p => p).ToList();
#if !UNITY_2021_1_OR_NEWER
            cachedDependencies = reformated;
#endif
            return reformated;
        }

        public void Cleanup()
        {
            bool modifiedAssets = false;

            if (DeleteFileAndMeta(Utils.TestRunPath))
            {
                modifiedAssets = true;
            }

            if (DeleteFileAndMeta(Utils.RunSettingsPath))
            {
                modifiedAssets = true;
            }

            // Only delete the Resources folder if we created it and it is empty by
            // now - anything still inside was put there by the user, e.g. while a
            // stale cleanup flag was left behind by an aborted build.
            if (SessionState.GetBool(cleanResources, false) && Directory.Exists(Utils.ResourcesPath)
                && Directory.GetFileSystemEntries(Utils.ResourcesPath).Length == 0)
            {
                try
                {
                    // Non-recursive on purpose: if an external process created a file
                    // in the window since the emptiness check, fail instead of
                    // deleting it. Cleanup also runs during build preprocessing,
                    // where an unhandled exception would fail the build, so
                    // deletion failures only warn - a leftover empty folder is acceptable.
                    Directory.Delete(Utils.ResourcesPath, false);
                    modifiedAssets = true;
                }
                catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[TestRunBuilder] Could not delete '{Utils.ResourcesPath}': {e.Message}");
                }

                // Only remove the folder's meta once the folder is really gone -
                // deleting the meta of an existing folder would make Unity
                // regenerate it with a new GUID.
                if (!Directory.Exists(Utils.ResourcesPath))
                {
                    modifiedAssets |= TryDeleteFile(Utils.ResourcesPath + ".meta");
                }
            }

            // The flag describes at most one build cycle; reset it so it cannot
            // leak into a later, unrelated build.
            SessionState.SetBool(cleanResources, false);
            // Scrub the legacy EditorPrefs flag older package versions may have
            // left behind; it is no longer read.
            EditorPrefs.DeleteKey(cleanResources);

            // Only refresh the AssetDatabase if we actually deleted performance test files
            if (modifiedAssets)
            {
                AssetDatabase.Refresh();
            }
        }

        private bool DeleteFileAndMeta(string path)
        {
            // Non-short-circuiting so the meta file is attempted even if the
            // main file could not be deleted.
            return TryDeleteFile(path) | TryDeleteFile(path + ".meta");
        }

        private static bool TryDeleteFile(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            try
            {
                File.Delete(path);
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Cleanup runs during build preprocessing, where an unhandled
                // exception (e.g. from a transiently locked file) would fail the
                // build; the next cleanup pass will retry.
                Debug.LogWarning($"[TestRunBuilder] Could not delete '{path}': {e.Message}");
                return false;
            }
        }

        // UUM-132359: copy the asset's .meta into MetaCacheDir before we delete it, preserving its GUID.
        private static void StashMeta(string assetPath)
        {
            var srcMeta = assetPath + ".meta";
            if (!File.Exists(srcMeta)) return;

            try
            {
                Directory.CreateDirectory(MetaCacheDir);
                var destMeta = Path.Combine(MetaCacheDir, Path.GetFileName(srcMeta));
                File.Copy(srcMeta, destMeta, overwrite: true);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[TestRunBuilder] Could not cache '{srcMeta}': {e.Message}");
            }
        }

        // UUM-132359: counterpart to StashMeta - restores the cached .meta before the asset is rewritten. No-op if the
        // cache is empty or a .meta already exists at the destination.
        private static void RestoreCachedMeta(string assetPath)
        {
            var destMeta = assetPath + ".meta";
            if (File.Exists(destMeta)) return;

            var cachedMeta = Path.Combine(MetaCacheDir, Path.GetFileName(destMeta));
            if (!File.Exists(cachedMeta)) return;

            try
            {
                var destDir = Path.GetDirectoryName(destMeta);
                if (!string.IsNullOrEmpty(destDir) && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }
                File.Copy(cachedMeta, destMeta, overwrite: false);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[TestRunBuilder] Could not restore cached meta for '{assetPath}': {e.Message}");
            }
        }

        private static Data.Editor GetEditorInfo()
        {
            var fullVersion = UnityEditorInternal.InternalEditorUtility.GetFullUnityVersion();
            const string pattern = @"(.+\.+.+\.\w+)|((?<=\().+(?=\)))";
            var matches = Regex.Matches(fullVersion, pattern);

            return new Data.Editor
            {
                Branch = GetEditorBranch(),
                Version = matches[0].Value,
                Changeset = matches[1].Value,
                Date = UnityEditorInternal.InternalEditorUtility.GetUnityVersionDate(),
            };
        }

        private static string GetEditorBranch()
        {
            foreach (var method in typeof(UnityEditorInternal.InternalEditorUtility).GetMethods())
            {
                if (method.Name.Contains("GetUnityBuildBranch"))
                {
                    return (string) method.Invoke(null, null);
                }
            }

            return "null";
        }

        private static void SetBuildSettings(Run run)
        {
            if (run.Player == null) run.Player = new Player();

            run.Player.GpuSkinning = PlayerSettings.gpuSkinning;
            #if UNITY_2021_2_OR_NEWER
            run.Player.ScriptingBackend = PlayerSettings
                .GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(EditorUserBuildSettings.selectedBuildTargetGroup)).ToString();
            #else
            run.Player.ScriptingBackend = PlayerSettings
                .GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString();
            #endif
            run.Player.RenderThreadingMode = PlayerSettings.graphicsJobs ? PlayerSettings.graphicsJobMode.ToString() :
                PlayerSettings.MTRendering ? "MultiThreaded" : "SingleThreaded";
            run.Player.AndroidTargetSdkVersion = PlayerSettings.Android.targetSdkVersion.ToString();
            run.Player.AndroidBuildSystem = EditorUserBuildSettings.androidBuildSystem.ToString();
            run.Player.BuildTarget = EditorUserBuildSettings.activeBuildTarget.ToString();
            run.Player.StereoRenderingPath = PlayerSettings.stereoRenderingPath.ToString();
        }

        public Run CreateRunInfo()
        {
            var run = new Run();
            run.Editor = GetEditorInfo();
            run.Dependencies = GetPackageDependencies();
            SetBuildSettings(run);
            run.Date = Utils.ConvertToUnixTimestamp(DateTime.Now);

            return run;
        }
        public Run CreateBuildInfo()
        {
            var run = new Run();
            run.Editor = GetEditorInfo();
            run.Dependencies = GetPackageDependencies();
            SetBuildSettings(run);

            return run;
        }

        public Run GetPerformanceTestRun()
        {
            var run = CreateRunInfo();
            Metadata.SetRuntimeSettings(run);

            return run;
        }


        private void CreateResourcesFolder()
        {
            if (Directory.Exists(Utils.ResourcesPath))
            {
                SessionState.SetBool(cleanResources, false);
                return;
            }

            SessionState.SetBool(cleanResources, true);
            AssetDatabase.CreateFolder("Assets", "Resources");
        }

        private string SaveToStorage(object obj, string path)
        {
            var json = JsonUtility.ToJson(obj);
            if (File.Exists(path))
            {
                var existing = File.ReadAllText(path);
                if (existing == json)
                {
                    return json;
                }

                Debug.LogWarning($"[TestRunBuilder] Content changed for '{path}' - rewriting file\nOld length: {existing.Length} bytes\nNew length: {json.Length} bytes");

                if (EnableDetailedDiffLogging)
                {
                    LogContentDifferences(existing, json);
                }
            }
            else
            {
                Debug.Log($"[TestRunBuilder] Creating new file '{path}' ({json.Length} bytes)");
            }

            File.WriteAllText(path, json);
            return json;
        }

        private void LogContentDifferences(string existing, string json)
        {
            Debug.Log($"[TestRunBuilder] Old content:\n{existing}");
            Debug.Log($"[TestRunBuilder] New content:\n{json}");
        }

        private string SaveToPrefs(object obj, string key)
        {
            var json = JsonUtility.ToJson(obj, true);
            PlayerPrefs.SetString(key, json);
            return json;
        }
    }
}
