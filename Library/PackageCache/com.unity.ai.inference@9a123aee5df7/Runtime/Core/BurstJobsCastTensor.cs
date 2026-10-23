using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.InferenceEngine
{
    static class BurstJobsCastTensor
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Float64BytesAsFloatJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public long* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public float* dst;

            public void Execute(int index)
            {
                double v = math.asdouble(src[index]);
                dst[index] = v < int.MinValue ? (float)int.MinValue : v > int.MaxValue ? (float)int.MaxValue : (float)v;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Float16BytesAsFloatJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public ushort* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public float* dst;

            public void Execute(int index)
            {
                dst[index] = Mathf.HalfToFloat(src[index]);
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct BoolBytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public bool* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                dst[index] = src[index] ? 1 : 0;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Uint8BytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public byte* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                dst[index] = src[index];
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Int8BytesAsIntJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public sbyte* src;
            [NoAlias] [NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                dst[index] = src[index];
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Uint16BytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public ushort* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                dst[index] = src[index];
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Int16BytesAsIntJob : IJobParallelFor
        {
            [NoAlias] [NativeDisableUnsafePtrRestriction] [ReadOnly] public short* src;
            [NoAlias] [NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                dst[index] = src[index];
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Uint32BytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public uint* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                uint v = src[index];
                dst[index] = v > int.MaxValue ? int.MaxValue : (int)v;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Uint64BytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public ulong* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                ulong v = src[index];
                dst[index] = v > int.MaxValue ? int.MaxValue : (int)v;
            }
        }

        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        public unsafe struct Int64BytesAsIntJob : IJobParallelFor
        {
            [NoAlias][NativeDisableUnsafePtrRestriction] [ReadOnly] public long* src;
            [NoAlias][NativeDisableUnsafePtrRestriction]            public int* dst;

            public void Execute(int index)
            {
                long v = src[index];
                dst[index] = v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v;
            }
        }
    }
}
