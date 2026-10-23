using System;
using UnityEngine.Rendering;

namespace UnityEngine.Rendering.RenderGraphModule
{
    [Obsolete("Use RenderGraphProfilerMarkers static fields instead.")]
    internal enum RenderGraphProfileId
    {
        CompileRenderGraph,
        ExecuteRenderGraph,
        ComputeHashRenderGraph,
    }
}
