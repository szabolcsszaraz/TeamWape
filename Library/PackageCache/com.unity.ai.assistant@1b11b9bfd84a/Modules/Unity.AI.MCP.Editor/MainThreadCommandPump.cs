using System;
using System.Threading;

namespace Unity.AI.MCP.Editor
{
    /// <summary>
    /// Focus-independent pump that drives the MCP command-queue drain off the
    /// throttled <c>EditorApplication.update</c> tick. A background timer ticks at
    /// a steady cadence regardless of editor focus; when work is pending it calls
    /// <c>requestDrain</c>, which (see Bridge) marshals the drain to the main thread
    /// and wakes the editor loop. Execution of the commands themselves stays on the
    /// main thread — this only changes what *drives* the drain.
    /// </summary>
    sealed class MainThreadCommandPump : IDisposable
    {
        readonly Func<bool> m_HasPendingWork;
        readonly Action m_RequestDrain;
        readonly int m_IntervalMs;
        Timer m_Timer;
        int m_Ticking;   // reentrancy guard (0/1)
        volatile bool m_Disposed;

        public MainThreadCommandPump(Func<bool> hasPendingWork, Action requestDrain, int intervalMs)
        {
            m_HasPendingWork = hasPendingWork ?? throw new ArgumentNullException(nameof(hasPendingWork));
            m_RequestDrain = requestDrain ?? throw new ArgumentNullException(nameof(requestDrain));
            m_IntervalMs = intervalMs;
            if (intervalMs <= 0) throw new ArgumentOutOfRangeException(nameof(intervalMs), "Must be positive.");
        }

        public void Start()
        {
            if (m_Disposed) return;
            m_Timer ??= new Timer(_ => Tick(), null, m_IntervalMs, m_IntervalMs);
        }

        // internal for unit tests; normally invoked by the timer.
        internal void Tick()
        {
            if (m_Disposed) return;
            if (Interlocked.Exchange(ref m_Ticking, 1) == 1) return;
            try
            {
                if (m_HasPendingWork())
                    m_RequestDrain();
            }
            catch
            {
                // Never let a pump tick throw onto the timer thread.
            }
            finally
            {
                Interlocked.Exchange(ref m_Ticking, 0);
            }
        }

        public void Dispose()
        {
            // A tick already in flight may call requestDrain once more after Dispose() returns; callers must tolerate this (Timer.Dispose does not wait for in-flight callbacks).
            m_Disposed = true;
            m_Timer?.Dispose();
            m_Timer = null;
        }
    }
}
