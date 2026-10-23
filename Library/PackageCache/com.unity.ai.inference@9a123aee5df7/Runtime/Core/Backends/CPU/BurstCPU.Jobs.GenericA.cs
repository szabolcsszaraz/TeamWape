using System;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;
using static Unity.Mathematics.math;

namespace Unity.InferenceEngine
{
    partial class CPUBackend
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal unsafe struct InstanceNormalizationTailJob : IJobParallelFor, IJobResourceDeclarationXSBWO
        {
            public float epsilon;
            public int channels;
            public int spatialDims;
            public ReadOnlyMemResource X { get; set; } float* Xptr => (float*)X.ptr;
            public ReadOnlyMemResource S { get; set; } float* Sptr => (float*)S.ptr;
            public ReadOnlyMemResource B { get; set; } float* Bptr => (float*)B.ptr;
            public ReadOnlyMemResource W { get; set; } float* Wptr => (float*)W.ptr;
            public ReadWriteMemResource O { get; set; } float* Optr => (float*)O.ptr;

            public void Execute(int threadIdx)
            {
                int bc = threadIdx / (spatialDims);
                int c = bc % channels;

                float mean = Wptr[bc * 2 + 0];
                float variance = Wptr[bc * 2 + 1];

                float scale = Sptr[c];
                float bias = Bptr[c];

                // normalization factor
                float invNormFactor = 1 / sqrt(variance + epsilon);

                float v = Xptr[threadIdx];
                v = v * invNormFactor - mean * invNormFactor;
                v = v * scale + bias;

                Optr[threadIdx] = v;
            }
        }
    }
}
