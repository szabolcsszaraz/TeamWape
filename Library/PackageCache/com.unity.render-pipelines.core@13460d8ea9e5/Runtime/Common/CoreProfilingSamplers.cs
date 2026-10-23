using Unity.Profiling.LowLevel;

namespace UnityEngine.Rendering
{
    /// <summary>
    /// Static profiler marker declarations for the Core Render Pipeline.
    /// Each field is a pre-allocated <see cref="ProfilingSampler"/>.
    /// </summary>
    internal static class CoreProfilingSamplers
    {
        /// <summary>
        /// Blits a source texture into a power-of-two atlas slot, executing mip-level copies
        /// with padding to prevent texture bleeding at atlas boundaries.
        /// </summary>
        public static readonly ProfilingSampler BlitTextureInPotAtlas = ProfilingSampler.Create(nameof(BlitTextureInPotAtlas), MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Streams Adaptive Probe Volume cells in and out based on camera position, which loads
        /// brick data for nearby cells and frees up distant cells to stay within the memory budget.
        /// </summary>
        public static readonly ProfilingSampler APVCellStreamingUpdate = ProfilingSampler.Create(nameof(APVCellStreamingUpdate), MarkerFlags.Default);

        /// <summary>
        /// Blends between two Adaptive Probe Volume lighting scenarios (for example, day and night) by
        /// interpolating SH coefficients in the brick pool based on the current blend factor.
        /// </summary>
        public static readonly ProfilingSampler APVScenarioBlendingUpdate = ProfilingSampler.Create(nameof(APVScenarioBlendingUpdate), MarkerFlags.Default);

        /// <summary>
        /// Defragments the Adaptive Probe Volume index buffer by compacting sparse entries
        /// left by evicted cells, which empties contiguous index space for new allocations.
        /// </summary>
        public static readonly ProfilingSampler APVIndexDefragUpdate = ProfilingSampler.Create(nameof(APVIndexDefragUpdate), MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Reads Adaptive Probe Volume brick data from disk into the streaming scratch buffer,
        /// issuing asynchronous file reads for cells queued by the cell streaming update.
        /// </summary>
        public static readonly ProfilingSampler APVDiskStreamingUpdate = ProfilingSampler.Create(nameof(APVDiskStreamingUpdate), MarkerFlags.Default);

        /// <summary>
        /// Uploads completed disk-read results into the Adaptive Probe Volume GPU brick pool,
        /// copying SH data from the scratch buffer into the 3D pool texture at assigned locations.
        /// </summary>
        public static readonly ProfilingSampler APVDiskStreamingUpdatePool = ProfilingSampler.Create(nameof(APVDiskStreamingUpdatePool), MarkerFlags.VerbosityAdvanced);
    }
}
