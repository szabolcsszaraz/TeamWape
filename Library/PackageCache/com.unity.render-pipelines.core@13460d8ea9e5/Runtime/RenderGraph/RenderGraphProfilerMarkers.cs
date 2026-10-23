using Unity.Profiling;
using Unity.Profiling.LowLevel;

namespace UnityEngine.Rendering.RenderGraphModule
{
    /// <summary>
    /// Static profiler marker declarations for the Render Graph system.
    /// </summary>
    internal static class RenderGraphProfilerMarkers
    {
        /// <summary>
        /// Compiles the native render graph from the recorded pass list, resolving resource
        /// lifetimes, merging compatible passes, and allocating transient render targets.
        /// </summary>
        public static readonly ProfilerMarker CompileRenderGraph = new ProfilerMarker(ProfilerCategory.Render, "Inl_CompileRenderGraph");

        /// <summary>
        /// Computes an FNV-1a hash of all recorded render graph passes and their resource
        /// dependencies, used to detect unchanged graphs and skip recompilation.
        /// CPU-only marker — no GPU sampling.
        /// </summary>
        public static readonly ProfilerMarker ComputeHashRenderGraph = new ProfilerMarker(ProfilerCategory.Render, "Inl_ComputeHashRenderGraph", MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Executes the compiled native render graph, dispatching each pass's render commands
        /// through the command buffer with automatic resource barriers and load/store actions.
        /// </summary>
        public static readonly ProfilingSampler ExecuteRenderGraph = ProfilingSampler.Create(nameof(ExecuteRenderGraph), MarkerFlags.Default);
    }
}
