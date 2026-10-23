using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class CounterTools
    {
        // Hard cap on the number of frames GetCounterTable will emit, to keep the table readable.
        internal const int k_MaxTableFrames = 20;

        [AgentTool("Return a frames x counters table of raw counter values over a range of frames (max 20 frames). Use this to inspect per-frame values; for larger ranges use GetCounterSummary instead.", "Unity.Profiler.GetCounterTable")]
        [AgentToolSettings(assistantMode: AssistantMode.Agent | AssistantMode.Ask, mcp: McpAvailability.Available)]
        public static string GetCounterTable(
            ToolExecutionContext context,
            [ToolParameter("The names of the counters to read, exactly as listed in the seeded counter set")]
            string[] counterNames,
            [ToolParameter("The index of the first frame to include")]
            int firstFrame,
            [ToolParameter("The index of the last frame to include (inclusive)")]
            int lastFrame
        )
        {
            if (counterNames == null || counterNames.Length == 0)
                return "No counter names were requested.";

            if (lastFrame < firstFrame)
                return $"lastFrame ({lastFrame}) must be greater than or equal to firstFrame ({firstFrame}).";

            var requestedFrames = lastFrame - firstFrame + 1;
            if (requestedFrames > k_MaxTableFrames)
                return $"The requested range covers {requestedFrames} frames, which exceeds the {k_MaxTableFrames}-frame limit for GetCounterTable. Narrow the range to {k_MaxTableFrames} frames or fewer, or use GetCounterSummary for min/max/median over a wider range.";

            var sb = new StringBuilder();
            var counters = ResolveCounters(counterNames, sb, out var unknownNames);
            if (counters.Count == 0)
                return $"None of the requested counter names were recognized: {string.Join(", ", unknownNames)}.";

            var frameDataCache = context.Conversation.GetFrameDataCache();
            firstFrame = Math.Max(firstFrame, frameDataCache.FirstFrameIndex);
            lastFrame = Math.Min(lastFrame, frameDataCache.LastFrameIndex);
            if (lastFrame < firstFrame)
                return "No frames in the requested range are available in the current capture.";

            // Header: Frame | Counter (unit) | ...
            sb.Append("| Frame |");
            foreach (var counter in counters)
                sb.Append($" {counter.Name} ({UnitLabel(counter.Unit)}) |");
            sb.AppendLine();
            sb.Append("|---|");
            foreach (var _ in counters)
                sb.Append("---|");
            sb.AppendLine();

            for (var frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
            {
                using var mainThreadData = frameDataCache.GetRawFrameDataView(frameIndex, FrameDataViewUtils.MainThreadIndex);
                sb.Append($"| {FrameDataViewUtils.GetDisplayFrameNumber(frameIndex)} |");
                foreach (var counter in counters)
                {
                    var raw = mainThreadData.valid ? mainThreadData.GetCounterValueAsUInt64(counter.Name) : null;
                    sb.Append(raw.HasValue ? $" {FormatValue(raw.Value, counter.Unit)} |" : " n/a |");
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        [AgentTool("Return per-counter min / max / median values over a range of frames (any range size). Values are labelled with their units.", "Unity.Profiler.GetCounterSummary")]
        [AgentToolSettings(assistantMode: AssistantMode.Agent | AssistantMode.Ask, mcp: McpAvailability.Available)]
        public static string GetCounterSummary(
            ToolExecutionContext context,
            [ToolParameter("The names of the counters to summarize, exactly as listed in the seeded counter set")]
            string[] counterNames,
            [ToolParameter("The index of the first frame to include")]
            int firstFrame,
            [ToolParameter("The index of the last frame to include (inclusive)")]
            int lastFrame
        )
        {
            if (counterNames == null || counterNames.Length == 0)
                return "No counter names were requested.";

            if (lastFrame < firstFrame)
                return $"lastFrame ({lastFrame}) must be greater than or equal to firstFrame ({firstFrame}).";

            var sb = new StringBuilder();
            var counters = ResolveCounters(counterNames, sb, out var unknownNames);
            if (counters.Count == 0)
                return $"None of the requested counter names were recognized: {string.Join(", ", unknownNames)}.";

            var frameDataCache = context.Conversation.GetFrameDataCache();
            firstFrame = Math.Max(firstFrame, frameDataCache.FirstFrameIndex);
            lastFrame = Math.Min(lastFrame, frameDataCache.LastFrameIndex);
            if (lastFrame < firstFrame)
                return "No frames in the requested range are available in the current capture.";

            sb.AppendLine($"Counter summary over frames {FrameDataViewUtils.GetDisplayFrameNumber(firstFrame)}–{FrameDataViewUtils.GetDisplayFrameNumber(lastFrame)}:");

            // Open each frame's native view exactly once and read every counter from it, rather than
            // reopening the full frame range per counter. This keeps the cost at O(frames) native calls.
            var valuesByCounter = new List<double>[counters.Count];
            for (var i = 0; i < counters.Count; i++)
                valuesByCounter[i] = new List<double>();

            for (var frameIndex = firstFrame; frameIndex <= lastFrame; frameIndex++)
            {
                using var mainThreadData = frameDataCache.GetRawFrameDataView(frameIndex, FrameDataViewUtils.MainThreadIndex);
                if (!mainThreadData.valid)
                    continue;
                for (var i = 0; i < counters.Count; i++)
                {
                    var raw = mainThreadData.GetCounterValueAsUInt64(counters[i].Name);
                    if (raw.HasValue)
                        valuesByCounter[i].Add(raw.Value);
                }
            }

            for (var i = 0; i < counters.Count; i++)
            {
                var counter = counters[i];
                var values = valuesByCounter[i];
                if (values.Count == 0)
                {
                    sb.AppendLine($"- {counter.Name}: no data in range.");
                    continue;
                }

                var stats = ComputeStats(values);
                var unit = UnitLabel(counter.Unit);
                sb.AppendLine($"- {counter.Name}: min {FormatValue(stats.Min, counter.Unit)} {unit}, max {FormatValue(stats.Max, counter.Unit)} {unit}, median {FormatValue(stats.Median, counter.Unit)} {unit}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Resolves the requested names against <see cref="CuratedCounters"/>, appending a note for any
        /// unrecognized names to <paramref name="sb"/> rather than throwing.
        /// </summary>
        static List<CuratedCounter> ResolveCounters(string[] counterNames, StringBuilder sb, out List<string> unknownNames)
        {
            var resolved = new List<CuratedCounter>();
            unknownNames = new List<string>();
            foreach (var name in counterNames)
            {
                if (CuratedCounters.TryResolve(name, out var counter))
                    resolved.Add(counter);
                else
                    unknownNames.Add(name);
            }

            if (unknownNames.Count > 0)
                sb.AppendLine($"Note: skipped unrecognized counters: {string.Join(", ", unknownNames)}.");

            return resolved;
        }

        internal struct CounterStats
        {
            public double Min { get; set; }
            public double Max { get; set; }
            public double Median { get; set; }
        }

        /// <summary>
        /// Computes min / max / median over a non-empty series of raw counter values. Median uses the
        /// average of the two middle values for even-length series.
        /// </summary>
        internal static CounterStats ComputeStats(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
                throw new ArgumentException("values must be non-empty", nameof(values));

            var sorted = new List<double>(values);
            sorted.Sort();

            double median;
            var count = sorted.Count;
            if (count % 2 == 1)
                median = sorted[count / 2];
            else
                median = (sorted[count / 2 - 1] + sorted[count / 2]) / 2.0;

            return new CounterStats { Min = sorted[0], Max = sorted[count - 1], Median = median };
        }

        internal static string UnitLabel(CounterUnit unit) => unit switch
        {
            CounterUnit.TimeNanoseconds => "ms",
            CounterUnit.Bytes => "bytes",
            CounterUnit.Count => "count",
            _ => string.Empty
        };

        /// <summary>
        /// Formats a raw counter value for display in its unit. Time counters are reported in
        /// milliseconds (raw value is nanoseconds); bytes and counts are reported as-is.
        /// </summary>
        internal static string FormatValue(double rawValue, CounterUnit unit) => unit switch
        {
            CounterUnit.TimeNanoseconds => (rawValue / 1_000_000.0).ToString("F3", CultureInfo.InvariantCulture),
            _ => rawValue.ToString("N0", CultureInfo.InvariantCulture)
        };
    }
}
