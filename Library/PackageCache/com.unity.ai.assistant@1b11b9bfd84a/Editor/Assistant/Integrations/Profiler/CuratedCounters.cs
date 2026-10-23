using System.Collections.Generic;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    /// <summary>
    /// A small, hand-authored set of high-signal profiler counters. This is the only way the
    /// prototype assistant learns which counters exist; it also doubles as the name → category/unit
    /// resolver for the counter tools (no dynamic catalog).
    /// </summary>
    static class CuratedCounters
    {
        public static readonly IReadOnlyList<CuratedCounter> All = new[]
        {
            new CuratedCounter("CPU", FrameDataViewUtils.MainThreadActiveTimeCounterName, CounterUnit.TimeNanoseconds,
                "Time the CPU main thread spent doing work this frame (excludes idle/wait)."),
            new CuratedCounter("CPU", FrameDataViewUtils.RenderThreadActiveTimeCounterName, CounterUnit.TimeNanoseconds,
                "Time the CPU render thread spent doing work this frame (excludes idle/wait)."),
            new CuratedCounter("GPU", FrameDataViewUtils.GpuFrameTimeCounterName, CounterUnit.TimeNanoseconds,
                "Time the GPU spent rendering the frame, as reported by the Frame Timing Manager."),
            new CuratedCounter("Memory", FrameDataViewUtils.GcAllocationsInFrameCounterName, CounterUnit.Bytes,
                "Managed heap memory allocated during the frame; high values drive GC spikes."),
            new CuratedCounter("Rendering", "Draw Calls Count", CounterUnit.Count,
                "Number of draw calls issued this frame."),
            new CuratedCounter("Rendering", "Batches Count", CounterUnit.Count,
                "Number of draw batches this frame; lower is better thanks to batching."),
            new CuratedCounter("Rendering", "SetPass Calls Count", CounterUnit.Count,
                "Number of shader pass switches this frame; a proxy for material/state changes."),
            new CuratedCounter("Rendering", "Triangles Count", CounterUnit.Count,
                "Number of triangles rendered this frame.")
        };

        /// <summary>
        /// Case-insensitive lookup of a curated counter by its name.
        /// </summary>
        public static bool TryResolve(string name, out CuratedCounter counter)
        {
            foreach (var candidate in All)
            {
                if (string.Equals(candidate.Name, name, System.StringComparison.OrdinalIgnoreCase))
                {
                    counter = candidate;
                    return true;
                }
            }

            counter = default;
            return false;
        }
    }
}
