using System.Collections;
using UnityEditor.TestTools.TestRunner.CommandLineTest;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace UnityEditor.TestTools.TestRunner.TestRun.Tasks
{
    /// <summary>
    /// Applies the scripting backend given in the test settings file (-testSettingsFile) to the player
    /// settings for the duration of an EditMode run, so that players built by the tests themselves use the
    /// requested backend. Player runs get this through <see cref="LegacyPlayerRunTask"/>, which applies the
    /// full set of test settings before building the test player.
    ///
    /// Only the scripting backend is applied here. The remaining test settings (provisioning profiles,
    /// graphics APIs, splash screen, ...) configure how the test player is built and launched, which an
    /// EditMode run never does, so applying them would change the editor state of existing runs for no gain.
    /// </summary>
    internal class ApplyPlayerScriptingBackendTask : TestTaskBase
    {
        public override IEnumerator Execute(TestJobData testJobData)
        {
            var testSettings = (testJobData.executionSettings?.overloadTestRunSettings as RunSettings)?.TestSettings;
            var requestedBackend = testSettings?.scriptingBackend;
            if (requestedBackend == null)
            {
                yield break;
            }

            var buildTargetGroup = EditorUserBuildSettings.activeBuildTargetGroup;
#if UNITY_2021_2_OR_NEWER
            var namedBuildTarget = NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            var currentBackend = PlayerSettings.GetScriptingBackend(namedBuildTarget);
#else
            var currentBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup);
#endif
            if (currentBackend == requestedBackend.Value)
            {
                yield break;
            }

            testJobData.HasOriginalPlayerScriptingBackend = true;
            testJobData.OriginalPlayerScriptingBackendGroup = buildTargetGroup;
            testJobData.OriginalPlayerScriptingBackend = currentBackend;

#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(namedBuildTarget, requestedBackend.Value);
#else
            PlayerSettings.SetScriptingBackend(buildTargetGroup, requestedBackend.Value);
#endif

            // Flush the change to ProjectSettings.asset now, while no test is running. Changing the backend
            // dirties the player settings, and the first BuildPlayer/BuildAssetBundles of the run would
            // otherwise write them itself (BuildPlayer.cpp calls AssetDatabase::SaveAssets before building).
            // That write then happens inside a fixture's OneTimeSetUp, so anything it logs is attributed to
            // that fixture and fails every test in it. Doing it here keeps the write outside test scope.
            AssetDatabase.SaveAssets();
        }
    }
}
