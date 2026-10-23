using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;

namespace UnityEngine.Rendering.RenderGraphModule.NativeRenderPassCompiler
{
    internal partial class NativePassCompiler
    {
        /// <summary>
        /// Marks the initialization of per-compilation context data by allocating pass and resource
        /// arrays sized to the pass count. Cost scales with pass count and resource count in the
        /// render graph.
        /// </summary>
        internal static readonly ProfilerMarker k_SetupContextData =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_SetupContextData");

        /// <summary>
        /// Marks construction of the pass dependency graph from the recorded render graph pass
        /// list. Iterates every pass to resolve resource read/write relationships. Cost scales
        /// with total pass count and the number of resource attachments for each pass.
        /// </summary>
        internal static readonly ProfilerMarker k_BuildGraph =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_BuildGraph");

        /// <summary>
        /// Marks dead-code elimination of render passes. Removes passes whose outputs are never
        /// consumed and have no side effects. Cost scales with total pass count and the depth of
        /// resource dependency chains.
        /// </summary>
        internal static readonly ProfilerMarker k_CullNodes =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_CullNodes");

        /// <summary>
        /// Marks the pass-merging phase that combines compatible raster passes into a single
        /// native render pass. Reduces load/store operations on tile-based hardware. Cost
        /// scales with pass count and render target compatibility checks.
        /// </summary>
        internal static readonly ProfilerMarker k_TryMergeNativePasses =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_TryMergeNativePasses");

        /// <summary>
        /// Marks the forward and backward traversal that computes the first and last use of
        /// each resource. Results drive resource lifetime, async-compute fence placement, and
        /// memoryless eligibility. Cost scales with pass count and total resource reference count.
        /// </summary>
        internal static readonly ProfilerMarker k_FindResourceUsageRanges =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_FindResourceUsageRanges");

        /// <summary>
        /// Marks the backward traversal that propagates texture UV origin (top-left or
        /// bottom-left) through the native pass graph. Ensures correct blit and resolve
        /// operations on platforms with flipped coordinate spaces. Cost scales with native
        /// pass count and attachment count.
        /// </summary>
        internal static readonly ProfilerMarker k_PropagateTextureUVOrigin =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_PropagateTextureUVOrigin");

        /// <summary>
        /// Marks the scan that identifies transient resources eligible for memoryless
        /// (tile-local) allocation. Only runs on hardware that supports memoryless textures.
        /// Cost scales with native pass count and the number of transient attachments.
        /// </summary>
        internal static readonly ProfilerMarker k_DetectMemorylessResources =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_DetectMemorylessResources");

        /// <summary>
        /// Marks the per-pass resource initialization step that allocates and clears transient
        /// render targets before their first use. Cost depends on the number of resources
        /// created per pass and whether a clear blit is required.
        /// </summary>
        internal static readonly ProfilerMarker k_ExecuteInitializeResources =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_ExecuteInitializeResources");

        /// <summary>
        /// Marks determination of load and store actions for each attachment in a native render
        /// pass. Evaluates clear, load, discard, and store policies per attachment. Cost scales
        /// with attachment count and the complexity of merged subpass configurations.
        /// </summary>
        internal static readonly ProfilerMarker k_PrepareNativePass =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_PrepareNativePass");

        /// <summary>
        /// Marks emission of the BeginRenderPass command into the command buffer. Configures
        /// attachment descriptors, MSAA, dimensions, and load/store actions for each attachment.
        /// Cost scales with attachment count and MSAA resolve complexity.
        /// </summary>
        internal static readonly ProfilerMarker k_ExecuteBeginRenderPassCommand =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_ExecuteBeginRenderpassCommand");

        /// <summary>
        /// Marks per-pass resource destruction after the last pass that uses each transient
        /// resource. Releases render targets, temporary allocations, and pooled objects. Cost
        /// scales with the number of resources destroyed per pass.
        /// </summary>
        internal static readonly ProfilerMarker k_ExecuteDestroyResources =
            new ProfilerMarker(ProfilerCategory.Render, "Inl_NRPRGComp_ExecuteDestroyResources");
    }
}
