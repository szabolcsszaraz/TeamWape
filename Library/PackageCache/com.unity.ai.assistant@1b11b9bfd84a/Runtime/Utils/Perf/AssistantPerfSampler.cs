#if UNITY_EDITOR
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Utils.Perf
{
    // Temporary diagnostic instrumentation for UUM-146077.
    static class AssistantPerfSampler
    {
        const double k_FlushIntervalSeconds = 0.25;
        const double k_SpikeThresholdMs = 50.0;

        static readonly StringBuilder k_Builder = new(4096);

        static bool s_Running;
        static double s_LastTickTime;
        static double s_LastFlushTime;
        static double s_IntervalMaxTickMs;
        static double s_IntervalSumTickMs;
        static int s_IntervalTicks;

        internal static int LastFrame { get; private set; }

        internal static bool LastPlaying { get; private set; }

        internal static void Start()
        {
            if (s_Running)
                return;

            s_Running = true;
            s_LastTickTime = EditorApplication.timeSinceStartup;
            s_LastFlushTime = s_LastTickTime;

            EditorApplication.update += OnEditorUpdate;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting += Stop;
        }

        internal static void Stop()
        {
            if (!s_Running)
                return;

            s_Running = false;

            EditorApplication.update -= OnEditorUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            EditorApplication.quitting -= Stop;

            AssistantPerfRecorder.Flush();
        }

        static void OnBeforeAssemblyReload()
        {
            AssistantPerf.MarkEvent("editor.assembly_reload");
            Stop();
            AssistantPerfRecorder.Close();
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            AssistantPerf.MarkEvent("editor.play_mode", change.ToString());
            AssistantPerfRecorder.Flush();
        }

        static void OnEditorUpdate()
        {
            var now = EditorApplication.timeSinceStartup;
            var tickMs = (now - s_LastTickTime) * 1000.0;
            s_LastTickTime = now;

            LastFrame = Time.frameCount;
            LastPlaying = EditorApplication.isPlaying;

            s_IntervalTicks++;
            s_IntervalSumTickMs += tickMs;
            if (tickMs > s_IntervalMaxTickMs)
                s_IntervalMaxTickMs = tickMs;

            var isSpike = tickMs >= k_SpikeThresholdMs;
            if (!isSpike && now - s_LastFlushTime < k_FlushIntervalSeconds)
                return;

            s_LastFlushTime = now;
            WriteSample(now, tickMs, isSpike);
        }

        static void WriteSample(double now, double tickMs, bool isSpike)
        {
            var selfStart = AssistantPerf.Now;

            k_Builder.Clear();
            k_Builder.Append("{\"kind\":\"sample\",\"ts\":\"").Append(AssistantPerfRecorder.Timestamp()).Append('"');
            AppendNumber("tSec", now, "F3");
            AppendRaw("frame", LastFrame.ToString(CultureInfo.InvariantCulture));
            AppendRaw("playing", LastPlaying ? "true" : "false");
            AppendRaw("spike", isSpike ? "true" : "false");
            AppendNumber("tickMs", tickMs, "F2");
            AppendNumber("tickMaxMs", s_IntervalMaxTickMs, "F2");
            AppendNumber("tickAvgMs", s_IntervalTicks > 0 ? s_IntervalSumTickMs / s_IntervalTicks : 0.0, "F2");
            AppendRaw("ticks", s_IntervalTicks.ToString(CultureInfo.InvariantCulture));
            AppendNumber("unityDtMs", Time.unscaledDeltaTime * 1000.0, "F2");
            AppendNumber("heapMb", System.GC.GetTotalMemory(false) / (1024.0 * 1024.0), "F2");
            AppendRaw("gc0", System.GC.CollectionCount(0).ToString(CultureInfo.InvariantCulture));
            AppendRaw("gc1", System.GC.CollectionCount(1).ToString(CultureInfo.InvariantCulture));
            AppendRaw("gc2", System.GC.CollectionCount(2).ToString(CultureInfo.InvariantCulture));

            s_IntervalMaxTickMs = 0;
            s_IntervalSumTickMs = 0;
            s_IntervalTicks = 0;

            for (var i = 0; i < AssistantPerf.ProbeCount; i++)
            {
                var count = AssistantPerf.DrainCount(i);
                var ticks = AssistantPerf.DrainTicks(i);
                if (count == 0 && ticks == 0)
                    continue;

                AppendRaw("p." + AssistantPerf.ProbeNames[i] + ".n", count.ToString(CultureInfo.InvariantCulture));
                AppendNumber("p." + AssistantPerf.ProbeNames[i] + ".ms", AssistantPerf.TicksToMs(ticks), "F3");
            }

            for (var i = 0; i < AssistantPerf.GaugeCount; i++)
            {
                AppendRaw("g." + AssistantPerf.GaugeNames[i], AssistantPerf.ReadGauge(i).ToString(CultureInfo.InvariantCulture));
            }

            AppendNumber("selfMs", AssistantPerf.TicksToMs(AssistantPerf.Now - selfStart), "F4");
            k_Builder.Append('}');

            AssistantPerfRecorder.WriteLine(k_Builder.ToString());

            // A spike is the sample an investigator kills the Editor to read; it must survive that kill.
            if (isSpike)
                AssistantPerfRecorder.Flush();
        }

        static void AppendRaw(string key, string rawValue)
        {
            k_Builder.Append(",\"").Append(key).Append("\":").Append(rawValue);
        }

        static void AppendNumber(string key, double value, string format)
        {
            k_Builder.Append(",\"").Append(key).Append("\":").Append(value.ToString(format, CultureInfo.InvariantCulture));
        }
    }
}
#endif
