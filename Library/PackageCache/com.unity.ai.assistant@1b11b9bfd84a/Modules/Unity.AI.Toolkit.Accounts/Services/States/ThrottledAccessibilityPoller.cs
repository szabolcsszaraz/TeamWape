using System;

namespace Unity.AI.Toolkit.Accounts.Services.States
{
    /// <summary>
    /// Pure, Unity-free helper used while waiting for the account API to become
    /// accessible. On each poll it proactively re-derives the gating account
    /// states (at most once per <c>intervalSeconds</c>) so accessibility can flip
    /// without the editor regaining focus, then reports the current result.
    /// No focus is forced anywhere — refresh only re-reads cached Connect state.
    /// </summary>
    sealed class ThrottledAccessibilityPoller
    {
        readonly Func<bool> m_IsAccessible;
        readonly Action m_Refresh;
        readonly double m_IntervalSeconds;
        double m_NextRefreshAt; // 0 => refresh on first poll

        public ThrottledAccessibilityPoller(Func<bool> isAccessible, Action refresh, double intervalSeconds)
        {
            m_IsAccessible = isAccessible ?? throw new ArgumentNullException(nameof(isAccessible));
            m_Refresh = refresh ?? throw new ArgumentNullException(nameof(refresh));
            m_IntervalSeconds = intervalSeconds;
            if (intervalSeconds <= 0) throw new ArgumentOutOfRangeException(nameof(intervalSeconds), "Must be positive.");
        }

        public bool Poll(double nowSeconds)
        {
            if (nowSeconds >= m_NextRefreshAt)
            {
                m_NextRefreshAt = nowSeconds + m_IntervalSeconds;
                m_Refresh();
            }
            return m_IsAccessible();
        }
    }
}
