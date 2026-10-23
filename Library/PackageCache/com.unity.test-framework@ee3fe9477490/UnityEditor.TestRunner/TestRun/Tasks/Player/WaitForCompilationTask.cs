using System.Collections;
using UnityEditorInternal;

namespace UnityEditor.TestTools.TestRunner.TestRun.Tasks.Player
{
    /// <summary>
    /// Waits until script compilation and any pending domain reload have finished.
    /// Placed between <see cref="ApplyPlayerScriptingBackendTask"/> and
    /// <see cref="LegacyPlayerRunTask"/> in the Player task list so that
    /// BuildPlayer never starts while assemblies are still being recompiled
    /// (e.g. after a scripting-backend switch triggers a domain reload).
    /// </summary>
    internal class WaitForCompilationTask : TestTaskBase
    {
        public WaitForCompilationTask()
        {
            RerunAfterResume = true;
        }

        public override IEnumerator Execute(TestJobData testJobData)
        {
            while (EditorApplication.isCompiling || InternalEditorUtility.IsScriptReloadRequested())
            {
                yield return null;
            }
        }
    }
}
