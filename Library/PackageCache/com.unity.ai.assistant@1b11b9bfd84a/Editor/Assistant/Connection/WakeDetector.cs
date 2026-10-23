using System;

namespace Unity.AI.Assistant.Editor.Connection
{
    /// <summary>
    /// Detects that the process was suspended (e.g. system sleep) by spotting a large wall-clock
    /// gap between successive ticks. Pure and Unity-free for testability — the caller feeds it a
    /// monotonic clock (e.g. EditorApplication.timeSinceStartup) on each editor update.
    /// </summary>
    sealed class WakeDetector
    {
        readonly double m_GapThresholdSeconds;
        double m_LastTick;
        bool m_Primed;

        public WakeDetector(double gapThresholdSeconds)
        {
            if (gapThresholdSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(gapThresholdSeconds), "Must be positive.");
            m_GapThresholdSeconds = gapThresholdSeconds;
        }

        /// <summary>
        /// Feed the current time. Returns true exactly when the gap since the previous tick is at
        /// least the threshold (inclusive) (i.e. the process was likely suspended). The first call primes the
        /// clock and never fires.
        /// </summary>
        public bool Tick(double nowSeconds)
        {
            if (!m_Primed)
            {
                m_Primed = true;
                m_LastTick = nowSeconds;
                return false;
            }

            var gap = nowSeconds - m_LastTick;
            m_LastTick = nowSeconds;
            return gap >= m_GapThresholdSeconds;
        }
    }
}
