#if !UNITY_WEBGL_RENDERER_ONLY
using System;

namespace UnityEngine.Rendering
{
    [GenerateHLSL]
    internal static class SpeedTreeWindShaderDef
    {
        public const int kMaxWindParamsCount = InstanceDataSystem.k_STMaxWindParamsCount;
    };
}

#endif // !UNITY_WEBGL_RENDERER_ONLY
