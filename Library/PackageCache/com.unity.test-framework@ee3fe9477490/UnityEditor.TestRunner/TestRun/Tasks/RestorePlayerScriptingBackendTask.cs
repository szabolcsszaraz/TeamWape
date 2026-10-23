using System.Collections;
#if UNITY_2021_2_OR_NEWER
using UnityEditor.Build;
#endif

namespace UnityEditor.TestTools.TestRunner.TestRun.Tasks
{
    /// <summary>
    /// Restores the scripting backend that <see cref="ApplyPlayerScriptingBackendTask"/> replaced. The saved
    /// value lives on <see cref="TestJobData"/> rather than on the test settings, because
    /// <see cref="Api.ExecutionSettings.overloadTestRunSettings"/> is not serialized and is therefore gone
    /// after a domain reload.
    /// </summary>
    internal class RestorePlayerScriptingBackendTask : TestTaskBase
    {
        public RestorePlayerScriptingBackendTask()
        {
            RunOnError = ErrorRunMode.RunAlways;
            RunOnCancel = true;
        }

        public override IEnumerator Execute(TestJobData testJobData)
        {
            if (!testJobData.HasOriginalPlayerScriptingBackend)
            {
                yield break;
            }

            testJobData.HasOriginalPlayerScriptingBackend = false;
            var buildTargetGroup = testJobData.OriginalPlayerScriptingBackendGroup;
            var backend = testJobData.OriginalPlayerScriptingBackend;
#if UNITY_2021_2_OR_NEWER
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup), backend);
#else
            PlayerSettings.SetScriptingBackend(buildTargetGroup, backend);
#endif
        }
    }
}
