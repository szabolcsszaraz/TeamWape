using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Utils
{
    static class TaskUtils
    {
        /// <summary>
        /// Task call wrapper that ensures exceptions from async functions are always logged rather than silently
        /// swallowed. Also ensures that in fire-and-forget circumstances, the initial section of async functions do not
        /// run synchronously.
        /// <para>
        /// Accepts a <see cref="Func{Task}"/> so that the synchronous portion of an
        /// async method (everything before its first <c>await</c>) is deferred via <see cref="Task.Yield"/>, keeping
        /// the calling thread unblocked. Note: this does not offload to a thread-pool thread — continuations resume on
        /// the current <see cref="System.Threading.SynchronizationContext"/> (e.g. Unity's main thread).
        /// </para>
        /// </summary>
        /// <param name="task">The async function to invoke fire-and-forget.</param>
        /// <param name="exceptionHandler">Optional callback invoked in addition to logging when an exception is caught.</param>
        internal static Task WithExceptionLogging(this Func<Task> task, Action<Exception> exceptionHandler = null)
        {
            // Capture synchronously on the calling thread before any yield, so the stack trace
            // reflects the actual originating call site rather than the async continuation.
            var stackTrace = System.Environment.StackTrace;
            return EnsureNonBlockingWrapper(stackTrace);

            async Task EnsureNonBlockingWrapper(string callerStack)
            {
                // Force a yield, so that the first part of an async task is not blocking
                await Task.Yield();
                Task taskCall = null;

                // Catches exceptions thrown synchronously before the first await in the async function
                // (e.g. argument validation or setup code that throws before yielding).
                try
                {
                    taskCall = task();
                }
                catch (Exception ex)
                {
                    InternalLog.LogException(ex);
                    exceptionHandler?.Invoke(ex);
                    throw;
                }

                if (taskCall != null && (!taskCall.IsCompleted || taskCall.IsFaulted))
                {
                    try
                    {
                        await taskCall;
                    }
                    catch (Exception ex)
                    {
                        var combinedException = new AggregateException(ex.Message, ex, new Exception($"Originating thread callstack: {callerStack}"));
                        InternalLog.LogException(combinedException);
                        exceptionHandler?.Invoke(combinedException);
                        throw combinedException;
                    }
                }
            }
        }
        
        /// <summary>
        /// Awaits a condition to be true, and returns after a timeout. Returns false if condition never returns true
        /// before cancellation. Otherwise, returns true.
        /// </summary>
        internal static async Task<bool> AwaitCondition(Func<bool> condition, float pollRateMillis, CancellationToken cancellationToken)
        {
            while (!condition())
            {
                if (cancellationToken.IsCancellationRequested)
                    return false;

                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(pollRateMillis), cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    return false;
                }
            }
            
            return true;
        }
    }
}
