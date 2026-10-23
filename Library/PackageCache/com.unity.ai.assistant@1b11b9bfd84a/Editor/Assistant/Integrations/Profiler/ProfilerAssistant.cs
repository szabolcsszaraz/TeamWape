using System;
using System.Collections.Generic;
using System.IO;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Api;
using Unity.AI.Assistant.Skills;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    class ProfilerAssistant : IProxyCpuProfilerAskAssistantService
    {
        internal const string k_SkillName = "unity-profiling-skill";
        internal const string k_SkillTag = "Skills.Profiler";

        [InitializeOnLoadMethod]
        static void InitializeAgent()
        {
            try
            {
                var skill = CreateProfilingSkill();
                SkillsRegistry.AddSkills(new List<SkillDefinition> { skill });
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to initialize Profiling Skill: {ex.Message}");
            }
        }

        public bool Initialize()
        {
            return SkillsRegistry.GetSkills().ContainsKey(k_SkillName);
        }

        public void Dispose()
        {
            // nothing to do
        }

        public void ShowAskAssistantPopup(Rect parentRect, IProxyAskAssistantService.Context context, string prompt)
        {
            if (string.IsNullOrEmpty(context.Payload))
                throw new ArgumentException("Payload cannot be null or empty", nameof(context.Payload));
            if (string.IsNullOrEmpty(prompt))
                throw new ArgumentException("Prompt cannot be null or empty", nameof(prompt));

            var attachment = new VirtualAttachment(context.Payload, context.Type, context.DisplayName, context.Metadata);
            try
            {
                var attachedContext = new AssistantApi.AttachedContext();
                attachedContext.Add(attachment);
                _ = AssistantApi.PromptThenRunInternal(parentRect, prompt, attachedContext, integrationName: IntegrationName.CpuProfiler);
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private class PromptGetter : LazyFileConfiguration<string>, ISkillResource
        {
            public PromptGetter(string defaultPrompt, string path) : base(defaultPrompt, path) { }
            protected override string Parse(FileStream stream)
            {
                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
            public override string ToString() => Data;
            public int Length => Data.Length;
            public string GetContent(int maxChars = int.MaxValue) => Data.Length > maxChars ? Data.Substring(0, maxChars + 1) : Data;
        }

        // Use path to a local text file for faster iteration during development.
        private static readonly PromptGetter k_ProfilerAssistantSystemPrompt = new PromptGetter(k_ProfilerAssistantDefaultSystemPrompt, null);
        private static readonly PromptGetter k_ProfilerWorkflowResource = new PromptGetter(k_ProfilerWorkflowDefaultContent, null);
        private static readonly PromptGetter k_ProfilerAssumptionsResource = new PromptGetter(k_ProfilerAssumptionsDefaultContent, null);
        private static readonly PromptGetter k_ProfilerLinkFormatResource = new PromptGetter(k_ProfilerLinkFormatDefaultContent, null);
        private static readonly PromptGetter k_ProfilerCountersResource = new PromptGetter(k_ProfilerCountersDefaultContent, null);

        private const string k_ProfilerAssistantDefaultSystemPrompt =
@"## Role
You are a Unity performance profiling expert. Give concise, developer-focused advice on performance spikes.
Do not ask the user for more information. For ambiguous cases, state assumptions and investigate.

## Workflow
Before starting: read `resources/profiler-workflow.md` for the optimized analysis procedure and decision rules.

1. Triage: find the spiked frame using `GetFrameRangeTopTimeSummary`
2. Identify bottleneck: `GetFrameSelfTimeSamplesSummary` + `GetFrameGcAllocationsSummary` (batch both)
3. Verify: `GetSampleTimeSummary` using the SampleId (never construct path strings)
4. Code: `GetMarkerCode` if the marker maps to a user script

## References
- `resources/profiler-workflow.md` — optimized Golden Path, decision rules, URP/RenderGraph patterns
- `resources/profiler-assumptions.md` — marker naming conventions, pass-through markers, special markers
- `resources/profiler-link-format.md` — required link format for frames and samples
- `resources/profiler-counters.md` — available counters (units + descriptions) and how to read their values";

        private const string k_ProfilerWorkflowDefaultContent =
@"## Golden Path: Spike Analysis in ~6 Tool Calls

### Turn 1 — Frame Triage
- Call `GetFrameRangeTopTimeSummary` over the full capture to identify the max-CPU frame.
- Skip Frame 0 (index 0) for steady-state analysis — see `resources/profiler-assumptions.md` for initialization frame guidance.

### Turn 2 — Bottleneck Identification (batch both calls)
For the spiked frame, call both in the same step:
- `GetFrameSelfTimeSamplesSummary(frameIndex)` — identifies the LEAF bottleneck directly (actual executing code, not wrappers)
- `GetFrameGcAllocationsSummary(frameIndex)` — always pair with self-time to catch GC-driven spikes

### Turn 3 — Verify and Understand
Using the SampleId from Turn 2 (never construct a path string — always use the integer SampleId):
- `GetSampleTimeSummary(frameIndex, threadName, sampleId)` to see parent context and confirm Editor vs Runtime overhead.
- If the tool output says ""Sample Self Time is significant on its own"", that IS the bottleneck — stop drilling.

### Turn 4 — Code Investigation (only if sample maps to user script)
- `GetMarkerCode(markerName)` for markers matching `TypeName.MethodName()` pattern.
- Use `GetFileContentLineCount` before `GetFileContent` to avoid loading large files unnecessarily.
- Focus on the function named by the profiler sample. After reading script code, cross-reference with child samples.
- Call out the specific child samples that support your analysis in the output.

## Decision Rules

**High Self Time (leaf found):** Self-time > 10% of frame in `GetFrameSelfTimeSamplesSummary` → this IS the bottleneck. Go directly to `GetMarkerCode`. Do NOT traverse children.

**Low Self Time (pass-through marker):** Total-time high but self-time near zero → call `GetSampleTimeSummary` with the SampleId to list all top children in one call. Pick the deepest child that accounts for >50% of the parent's time.

**URP / RenderGraph markers:**
- `UniversalRenderTotal`, `RenderCameraStack`, `RecordAndExecuteRenderGraph` → pass-through wrappers; skip them and jump to `Inl_` markers or specific pass names.
- `RecordRenderGraph` → CPU-side pass setup complexity; look at child counts and script passes.
- `Execute` → draw call overhead or GPU submission.
- `Inl_SomeName` → call `GetMarkerCode(""SomeName"")` (strip the `Inl_` prefix).

**Multi-thread:** If Main Thread self-time does not explain the frame budget overrun, call `GetBottomUpSampleTimeSummary` without a thread filter, or `GetRelatedSamplesTimeSummary` to check Render Thread overlap.

**Parallel investigation:** When multiple independent systems are each slow (>10% of frame), batch their `GetSampleTimeSummary` and `GetMarkerCode` calls in the same reasoning step.

**Fast frame selected:** If the requested frame is within budget, tell the user and suggest selecting a frame near a visible spike, adjusting the target frame time, or using the Profile Analyzer package.

## ID Types — Do Not Mix
Each tool returns a specific ID type; they are not interchangeable:
- `SampleId` — use with `GetSampleTimeSummary`, `GetSampleGcAllocationSummary`, `GetRelatedSamplesTimeSummary`
- `BottomUpId` — use only with `GetBottomUpSampleTimeSummary` (returned by `GetFrameSelfTimeSamplesSummary`)
- `RawIndex` — stable within a frame; use only in profiler:// links, not as a tool parameter
- `Marker Id Path` — `/`-delimited MarkerId integers; use only with `GetSampleTimeSummaryByMarkerPath` / `GetSampleGcAllocationSummaryByMarkerPath`

If `GetSampleTimeSummary` fails with a SampleId from `GetFrameSelfTimeSamplesSummary`, that tool returns a `BottomUpId`. Call `GetBottomUpSampleTimeSummary` instead, then use the regular `SampleId` from its output for subsequent calls.

## SampleId Reliability
Tool outputs include `SampleId: N` and `RawIndex: N` for every sample. Always use the integer SampleId directly for follow-up calls. Never construct a marker path string manually — this causes ""Input string format"" errors.

If a SampleId fails with ""not found in Frame X on thread Y"", retry on a different thread (Render Thread, Worker Thread) before reporting failure.

## Engine Marker Filter — Do Not Search Source for These
Do NOT call `FindScriptFile` or `GetMarkerCode` for engine-internal markers. Use `GetSampleTimeSummary` to find the first user-script child instead:
- Markers starting with: `PostLateUpdate.`, `PreLateUpdate.`, `FixedUpdate.`, `Update.`, `Gfx.`, `Render.`, `Physics.`
- `EditorLoop`, `PlayerLoop`, `BehaviourUpdate`, `LateBehaviourUpdate`, `Initialize Mono`, `Initialize Graphics`
- Any marker without a `.` separator is likely an engine system marker, not a user script.

## Investigation Tips (when no obvious leaf bottleneck is found)
- Add `Profiler.BeginSample`/`EndSample` custom markers to narrow cost within a large function.
- Check for string concatenation, LINQ, or `new` allocations inside `Update()` loops causing `GC.Alloc`.
- Use Unity's Profile Analyzer package for multi-frame statistical analysis.
- Use Profile Analyzer's ""Compare"" mode between good and bad frames.";

        private const string k_ProfilerAssumptionsDefaultContent =
@"## Profiler Marker Naming Conventions

1. **MonoBehaviour scripts** use the format `MonoBehaviourName.Method`
   - Example: `MyScript.Update`, `MyScript.Start`
   - The corresponding filename is `MyScript.cs`

2. **`Inl_` prefix**: Main Thread markers of the Universal Render Pipeline
   - These map to a ScriptableRenderPass implementation
   - Search WITHOUT the `Inl_` prefix: `Inl_Light2D Pass` → search for `Light2D Pass`
   - Use `GetMarkerCode` with the stripped name

3. **Pass-through / wrapper markers** — high total time, near-zero self time, safe to skip:
   - `UniversalRenderTotal`, `RenderCameraStack`, `RecordAndExecuteRenderGraph`
   - `EditorLoop` (Editor overhead — not present in player builds)
   - `PlayerLoop`, `BehaviourUpdate`, `LateBehaviourUpdate` (loop dispatchers)
   - Call `GetSampleTimeSummary` on these only to find their expensive children, then move to those children.

4. **RenderGraph markers**:
   - `RecordRenderGraph`: cost here = CPU-side pass setup complexity
   - `Execute`: cost here = draw call overhead, GPU submission
   - `Inl_` markers under these are the actual render passes to investigate

5. **Job system markers**:
   - `WaitForJobGroupID`: Main Thread stall waiting for a job. `GetSampleTimeSummary` shows callstack. Use `GetRelatedSamplesTimeSummary` to check actual worker thread cost.

6. **GC markers**:
   - `GC.Alloc`: Managed allocation. `GetSampleTimeSummary` shows callstack if capture was taken with ""Capture Callstacks for GC.Alloc"" enabled.
   - If callstack is missing, advise the user to re-capture with callstack collection enabled.

7. **Initialization frames (Frame 1 / index 0)**:
   - Frame 1 (index 0) is dominated by `Initialize Mono`, `Initialize Graphics`, JIT compilation, and asset loading. High allocations and long frame times here are expected and non-actionable.
   - Always skip Frame 1 for steady-state performance analysis. Focus on a representative frame from normal gameplay (typically after the first few seconds of runtime).";

        private const string k_ProfilerLinkFormatDefaultContent =
@"## Profiler Link Format

ALWAYS refer to profiler frames and samples as links in the following format:

1. **Frame**: `[Frame 18](profiler://frame/17)` where 17 is the frame index used in the URL and 18 is the frame number displayed in the Profiler UI (frame index + 1)
2. **Sample**: `[SampleName](profiler://frame/17/threadName/Main%20Thread/rawIndex/60/name/SampleName)`
   - Parameters: frame index, URL-encoded thread name, RawIndex, URL-encoded sample name
   - Full example: `[UnityEngine.Rendering.DebugUpdater.RuntimeInit() [Invoke]](profiler://frame/17/threadName/Main%20Thread/rawIndex/60/name/UnityEngine.Rendering.DebugUpdater.RuntimeInit()%20%5BInvoke%5D)`
3. **DO NOT** wrap links with quotes or backticks";

        private const string k_ProfilerCountersDefaultContent =
@"## Available Counters

These are the profiler counters you can read. Use the exact names below — unrecognized names are skipped with a note. Values are read per frame from the main thread.

### CPU
- **CPU Main Thread Active Time** (ms) — Time the CPU main thread spent doing work this frame (excludes idle/wait).
- **CPU Render Thread Active Time** (ms) — Time the CPU render thread spent doing work this frame (excludes idle/wait).

### GPU
- **GPU Frame Time** (ms) — Time the GPU spent rendering the frame, as reported by the Frame Timing Manager.

### Memory
- **GC Allocated In Frame** (bytes) — Managed heap memory allocated during the frame; high values drive GC spikes.

### Rendering
- **Draw Calls Count** (count) — Number of draw calls issued this frame.
- **Batches Count** (count) — Number of draw batches this frame; lower is better thanks to batching.
- **SetPass Calls Count** (count) — Number of shader pass switches this frame; a proxy for material/state changes.
- **Triangles Count** (count) — Number of triangles rendered this frame.

## Reading Counter Values

- `GetCounterSummary(counterNames, firstFrame, lastFrame)` — min / max / median per counter over any range. **Start here over the whole capture** to see how a counter behaves before drilling into specific frames.
- `GetCounterTable(counterNames, firstFrame, lastFrame)` — per-frame frames x counters table. **Hard cap of 20 frames**; for wider ranges, narrow the request or use `GetCounterSummary`.";

        static SkillDefinition CreateProfilingSkill()
        {
            return new SkillDefinition()
                .WithName(k_SkillName)
                .WithDescription("Specialized skill for spike analysis of Unity performance profiling data. Used when a profiling capture is provided as context or for performance investigation queries.")
                .WithTag(k_SkillTag)
                .WithTag(SkillRegistryTags.BuiltIn) // bypasses the user opt-in filter; never cleared by file scanners
                .WithContent(k_ProfilerAssistantSystemPrompt.Data)
                .WithResource("resources/profiler-workflow.md", k_ProfilerWorkflowResource)
                .WithResource("resources/profiler-assumptions.md", k_ProfilerAssumptionsResource)
                .WithResource("resources/profiler-link-format.md", k_ProfilerLinkFormatResource)
                .WithResource("resources/profiler-counters.md", k_ProfilerCountersResource)
                .WithToolsFrom<ProfilingSessionTools>()
                .WithToolsFrom<ProfilingSummaryTools>()
                .WithToolsFrom<FileProfilingTools>()
                .WithToolsFrom<CounterTools>();
        }
    }
}
