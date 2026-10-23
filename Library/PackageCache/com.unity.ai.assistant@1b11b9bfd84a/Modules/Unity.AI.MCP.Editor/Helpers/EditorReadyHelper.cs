using System;
using System.Threading.Tasks;
using UnityEditor;

namespace Unity.AI.MCP.Editor.Helpers
{
    /// <summary>
    /// Ensures Unity's asset database is up-to-date before tool execution.
    /// Calls AssetDatabase.Refresh() and waits for any resulting compilation
    /// or asset import to finish. If domain reload occurs during the wait,
    /// the caller is killed and the MCP server retries automatically.
    /// </summary>
    static class EditorReadyHelper
    {
        static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(120);

        /// <summary>
        /// Refreshes the asset database and waits for the editor to become idle.
        /// No-op when there are no pending changes and the editor is already idle.
        /// </summary>
        public static async Task RefreshAndWaitForReady()
        {
            AssetDatabase.Refresh();

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                await WaitForEditorReadyAsync(DefaultTimeout);
            }
        }

        static Task WaitForEditorReadyAsync(TimeSpan timeout)
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            var start = DateTime.UtcNow;

            void Tick()
            {
                if (tcs.Task.IsCompleted)
                {
                    EditorApplication.update -= Tick;
                    return;
                }

                if ((DateTime.UtcNow - start) > timeout)
                {
                    EditorApplication.update -= Tick;
                    tcs.TrySetException(new TimeoutException());
                    return;
                }

                if (!EditorApplication.isCompiling && !EditorApplication.isUpdating)
                {
                    EditorApplication.update -= Tick;
                    tcs.TrySetResult(true);
                }
            }

            EditorApplication.update += Tick;
            EditorApplication.QueuePlayerLoopUpdate();
            return tcs.Task;
        }
    }
}
