using System;
using System.Diagnostics;
using System.Threading;
using Unity.Profiling;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Unity.AI.Assistant.Utils.Perf
{
    // Temporary diagnostic instrumentation for UUM-146077 (Editor FPS collapse in long Assistant sessions).
    static class AssistantPerf
    {
        internal readonly struct Scope : IDisposable
        {
            readonly int m_Probe;
            readonly long m_StartTicks;

            internal Scope(int probe, long startTicks)
            {
                m_Probe = probe;
                m_StartTicks = startTicks;
            }

            public void Dispose()
            {
                s_Markers[m_Probe].End();

                // A negative start marks a marker-only scope: the Profiler span is emitted but the JSONL
                // counters stay untouched because logging was off when the scope opened.
                if (m_StartTicks < 0)
                    return;

                Interlocked.Add(ref s_ProbeTicks[m_Probe], Stopwatch.GetTimestamp() - m_StartTicks);
                Interlocked.Increment(ref s_ProbeCounts[m_Probe]);
            }
        }

        internal const string k_SettingKey = "Assistant.Perf.Instrumentation";

        internal static readonly int ProbeCount = Enum.GetValues(typeof(PerfProbe)).Length;
        internal static readonly int GaugeCount = Enum.GetValues(typeof(PerfGauge)).Length;
        internal static readonly string[] ProbeNames = Enum.GetNames(typeof(PerfProbe));
        internal static readonly string[] GaugeNames = Enum.GetNames(typeof(PerfGauge));

        static readonly long[] s_ProbeTicks = new long[ProbeCount];
        static readonly long[] s_ProbeCounts = new long[ProbeCount];
        static readonly long[] s_Gauges = new long[GaugeCount];
        static readonly ProfilerMarker[] s_Markers = CreateMarkers();

        static volatile bool s_Enabled;

        public static bool Enabled => s_Enabled;

        public static long Now => Stopwatch.GetTimestamp();

        public static double TicksToMs(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

        public static Scope Measure(PerfProbe probe)
        {
            var index = (int)probe;
            s_Markers[index].Begin();
            return new Scope(index, s_Enabled ? Stopwatch.GetTimestamp() : -1);
        }

        public static void Count(PerfProbe probe)
        {
            if (!s_Enabled)
                return;

            Interlocked.Increment(ref s_ProbeCounts[(int)probe]);
        }

        public static void SetGauge(PerfGauge gauge, long value)
        {
            if (!s_Enabled)
                return;

            Interlocked.Exchange(ref s_Gauges[(int)gauge], value);
        }

        public static void AddGauge(PerfGauge gauge, long delta)
        {
            if (!s_Enabled)
                return;

            Interlocked.Add(ref s_Gauges[(int)gauge], delta);
        }

        public static void MarkEvent(string name, string detail = null)
        {
            if (!s_Enabled)
                return;

            AssistantPerfRecorder.WriteEvent(name, detail, -1);
        }

        public static void MarkElapsed(string name, string detail, long startTicks)
        {
            if (!s_Enabled)
                return;

            AssistantPerfRecorder.WriteEvent(name, detail, TicksToMs(Stopwatch.GetTimestamp() - startTicks));
        }

        internal static long DrainTicks(int probe) => Interlocked.Exchange(ref s_ProbeTicks[probe], 0);

        internal static long DrainCount(int probe) => Interlocked.Exchange(ref s_ProbeCounts[probe], 0);

        internal static long ReadGauge(int gauge) => Interlocked.Read(ref s_Gauges[gauge]);

        static ProfilerMarker[] CreateMarkers()
        {
            var markers = new ProfilerMarker[ProbeNames.Length];
            for (var i = 0; i < ProbeNames.Length; i++)
            {
                markers[i] = new ProfilerMarker("AssistantPerf." + ProbeNames[i]);
            }

            return markers;
        }

#if UNITY_EDITOR
        internal static void Initialize()
        {
            s_Enabled = EditorUserSettings.GetConfigValue(k_SettingKey) == bool.TrueString;

            if (!s_Enabled)
                return;

            AssistantPerfRecorder.Open();
            AssistantPerfSampler.Start();
        }

        internal static void SetEnabled(bool enabled)
        {
            EditorUserSettings.SetConfigValue(k_SettingKey, enabled.ToString());
            s_Enabled = enabled;

            if (enabled)
            {
                ResetCounters();
                AssistantPerfRecorder.Open();
                AssistantPerfSampler.Start();
            }
            else
            {
                AssistantPerfSampler.Stop();
                AssistantPerfRecorder.Close();
            }
        }

        static void ResetCounters()
        {
            for (var i = 0; i < ProbeCount; i++)
            {
                Interlocked.Exchange(ref s_ProbeTicks[i], 0);
                Interlocked.Exchange(ref s_ProbeCounts[i], 0);
            }

            for (var i = 0; i < GaugeCount; i++)
                Interlocked.Exchange(ref s_Gauges[i], 0);
        }
#endif
    }
}
