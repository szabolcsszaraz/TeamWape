using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.PlayMode.Editor;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEditor.SceneManagement;
using UnityEngine;
using Debug = UnityEngine.Debug;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Unity.Multiplayer.Center.Editor
{
    [InitializeOnLoad]
    class QuickStartSamplesManager
    {
        static QuickStartSamplesManager()
        {
            //Register delay call to check if we queued an init step before domain re-load
            EditorApplication.delayCall += ExecuteInitStepsForSample;
        }

        const string k_LogTag = "[" + nameof(QuickStartSamplesManager) + "]";
        const string k_DefaultSampleScene = "Example.unity";
        const string k_QuickStartPackageID = "com.unity.multiplayer.center.quickstart";
        const string k_InitializationStepID = "QuickStart-Initialization";
        const string k_ExecutionStepID = "QuickStart-ExecuteStepsFor";
        static readonly TimeSpan k_TimeoutInterval = TimeSpan.FromSeconds(10);

        readonly Dictionary<string, Sample> m_ImportedSamples = new();

        public QuickStartSamplesManager()
        {
            if (!IsQuickStartsInstalled(out PackageInfo quickstart)) { return; }

            foreach (var sample in GetSamplesFrom(quickstart))
            {
                var sampleName = System.IO.Path.GetFileName(sample.resolvedPath);
                var package = PackageInfo.FindForPackageName(sampleName);
                if (package != null && package.source == PackageSource.Embedded)
                {
                    m_ImportedSamples[sampleName] = sample;
                }
            }
        }

        ILogger Logger => Debug.unityLogger;

        public void Install(string sampleId)
        {
            Events.registeringPackages -= OnRegisteringPackages;
            AssetDatabase.importPackageCompleted -= OnReImportPackageCompleted;

            if (!VerifyQuickStartsAreInstalled(sampleId, out var quickstart)) { return; }

            Events.registeringPackages += OnRegisteringPackages;
            AssetDatabase.importPackageCompleted += OnReImportPackageCompleted;

            foreach (var sample in GetSamplesFrom(quickstart))
            {
                if (System.IO.Path.GetFileName(sample.resolvedPath) != sampleId) { continue; }

                EditorPrefs.SetString(k_InitializationStepID, sampleId);
                EditorApplication.delayCall += () =>
                {
                    sample.Import(Sample.ImportOptions.HideImportWindow |
                                  Sample.ImportOptions.OverridePreviousImports);
                };
                if (sample.Import(Sample.ImportOptions.HideImportWindow |
                                  Sample.ImportOptions.OverridePreviousImports))
                {
                    m_ImportedSamples[sampleId] = sample;
                    return;
                }

                Logger.LogError(k_LogTag, L10n.Tr($"Installing the sample {sampleId} has failed." +
                                                  " Make sure that the sample exists in the quick starts package!"));
            }

            Logger.LogWarning(k_LogTag, L10n.Tr("Sample with given id could not be located") + sampleId);
        }

        void OnReImportPackageCompleted(string a)
        {
            OnPackageImport(PackageInfo.GetAllRegisteredPackages());
        }

        void OnRegisteringPackages(PackageRegistrationEventArgs reg)
        {
            OnPackageImport(reg.added);
        }

        void OnPackageImport(IEnumerable<PackageInfo>  packages)
        {
            var importedPackage = EditorPrefs.GetString(k_InitializationStepID, null);
            if (packages != null)
            {
                foreach (var packageInfo in packages)
                {
                    if (packageInfo.name != importedPackage)
                    {
                        continue;
                    }

                    EditorPrefs.SetString(k_InitializationStepID, null);
                    EditorPrefs.SetString(k_ExecutionStepID, importedPackage);
                }
            }

            // A bunch of things do not work properly from the importPackageCompleted callback,
            // let's register to the next editor update instead.
            EditorApplication.delayCall += ExecuteInitStepsForSample;
        }

        public bool IsInstalled(string sampleId)
        {
            return m_ImportedSamples.ContainsKey(sampleId);
        }

        static IEnumerable<Sample> GetSamplesFrom(PackageInfo package)
        {
            return Sample.FindByPackage(package.name, package.version);
        }

        internal static bool IsQuickStartsInstalled(out PackageInfo quickstart)
        {
            quickstart = PackageInfo.FindForPackageName(k_QuickStartPackageID);
            return quickstart != null;
        }

        bool VerifyQuickStartsAreInstalled(string sampleId, out PackageInfo quickstart)
        {
            if (IsQuickStartsInstalled(out quickstart)) { return true; }

            var message =
                $"Installing the sample {sampleId} will require the installation of {k_QuickStartPackageID} too.";
            // not installed
            // prompt user to notify that the quickstarts package will be installed with this action too
            Logger.LogWarning(k_LogTag, message);

            var result = EditorDialog.DisplayDecisionDialog(
                "Missing required package.",
                message, "Install", "Cancel");

            // if canceled
            if (!result) { return false; }

            var addRequest = AwaitQuickstartsAddRequest();

            // if not a successful installation
            if (addRequest.Status != StatusCode.Success) { return false; }

            quickstart = addRequest.Result;
            return true;
        }

        public static AddRequest StartQuickstartsAddRequest() => Client.Add(k_QuickStartPackageID);

        public static AddRequest AwaitQuickstartsAddRequest()
        {
            // create the add request
            var addRequest = Client.Add(k_QuickStartPackageID);

            // wait for request to time out or complete
            var timeout = k_TimeoutInterval;

            var stopwatch = new Stopwatch();
            stopwatch.Start();

            while (timeout > TimeSpan.Zero && addRequest.Status == StatusCode.InProgress)
            {
                timeout -= stopwatch.Elapsed;
                stopwatch.Restart();
            }

            stopwatch.Stop();
            return addRequest;
        }

        static void ExecuteInitStepsForSample()
        {
            var sampleName = EditorPrefs.GetString(k_ExecutionStepID, null);
            EditorPrefs.SetString(k_ExecutionStepID, null);

            if (string.IsNullOrEmpty(sampleName))
                return;

            // Prompt to save the current scene before replacing it (skip in batch mode — no GUI)
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            // Load default scene
            EditorSceneManager.OpenScene($"Packages/{sampleName}/{k_DefaultSampleScene}", OpenSceneMode.Single);

            // ping readme
            var readme = AssetDatabase.LoadMainAssetAtPath($"Packages/{sampleName}/Readme.md");
            Selection.activeObject = readme;

            // load the playmode scenario associated with the sample
            var scenarios =
                AssetDatabase.FindAssets($"t:{nameof(PlayModeScenario)}", new[] { $"Packages/{sampleName}" });
            if (scenarios.Length > 0)
            {
                PlayModeScenarioManager.ActiveScenario =
                    AssetDatabase.LoadAssetAtPath<PlayModeScenario>(AssetDatabase.GUIDToAssetPath(scenarios[0]));
            }
            else
            {
                Debug.unityLogger.LogError(k_LogTag,
                    "No multiplayer scenarios were found in the sample! " +
                    "Ensure that each sample has a default scenario!");
            }

            // Looks like we need a second delayCall for the asset ping to work properly.
            EditorApplication.delayCall += () =>
            {
                ProjectWindowUtil.ShowCreatedAsset(readme);
                EditorGUIUtility.PingObject(readme);
            };
        }

        public void RemoveSample(string sampleId)
        {
            Logger.LogWarning(k_LogTag, $"Removing sample {sampleId}");

            var sample = PackageInfo.FindForPackageName(sampleId);

            // adapted from UpmClient.cs;

            try
            {
                foreach (var file in Directory.GetFiles(sample.resolvedPath, "*",
                             System.IO.SearchOption.AllDirectories))
                {
                    var fileInfo = new FileInfo(file);
                    if ((fileInfo.Attributes & FileAttributes.ReadOnly) != 0)
                    {
                        fileInfo.Attributes &= ~FileAttributes.ReadOnly;
                    }
                }

                Directory.Delete(sample.resolvedPath, true);

                //resolve packages once locally embedded package has been removed
                EditorApplication.delayCall += Client.Resolve;
            }
            catch (IOException e)
            {
                Logger.Log(k_LogTag, $"Cannot remove embedded sample {sampleId}: {e.Message}");
            }
        }
    }
}
