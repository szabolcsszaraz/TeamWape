using System;
using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.Utils
{
    /// <summary>
    /// Bounds an async operation with a deadline. Used to stop a stalled server-dependent
    /// asset-generation step (download / background-removal) from hanging a chat turn
    /// indefinitely (UUM-144682). On timeout the awaited task is abandoned (not cancelled) and a
    /// TimeoutException is thrown; cancellation of the underlying op is a separate follow-up.
    /// </summary>
    static class TaskTimeout
    {
        internal static async Task<T> WaitWithTimeout<T>(Task<T> task, TimeSpan timeout, string timeoutMessage)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            using var cts = new CancellationTokenSource();
            var completed = await Task.WhenAny(task, Task.Delay(timeout, cts.Token));
            if (completed != task)
            {
                // Observe any late-occurring exception on the abandoned task so it doesn't surface
                // as an UnobservedTaskException in the Unity console after we've moved on.
                _ = task.ContinueWith(t => { _ = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException(timeoutMessage);
            }

            cts.Cancel(); // stop the delay timer so it doesn't linger
            return await task; // observe the result, or re-throw the task's own exception
        }
    }
}
