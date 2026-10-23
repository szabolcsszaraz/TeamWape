using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Burst;

namespace Unity.InferenceEngine
{
    static class GPUPixelBurstJobs
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct IntBytesAsFloatJob : IJobParallelFor
        {
            [NativeDisableUnsafePtrRestriction]
            public int* src;
            public NativeArray<float> destLower;
            public NativeArray<float> destUpper;

            public void Execute(int index)
            {
                var n = math.asuint(src[index]);
                destLower[index] = math.asfloat(0x3f800000 | (n & 0x0000ffff));
                destUpper[index] = math.asfloat(0x3f800000 | (n >> 16));
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct FloatBytesAsIntJob : IJobParallelFor
        {
            public NativeArray<float> srcLower;
            public NativeArray<float> srcUpper;
            [NativeDisableUnsafePtrRestriction]
            public int* dest;

            public void Execute(int index)
            {
                var nLower = math.asuint(srcLower[index]);
                var nUpper = math.asuint(srcUpper[index]);
                dest[index] = math.asint((nUpper << 16) | (nLower & 0x0000ffff));
            }
        }
    }
}
