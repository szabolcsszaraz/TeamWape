using System;
using Unity.Burst;
using Unity.Jobs;
using UnityEngine;

namespace Unity.InferenceEngine
{
    partial class CPUBackend
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        internal unsafe struct SetShapeJob : IJob, IJobResourceDeclarationO
        {
            public ReadWriteMemResource O { get; set; } int* Optr => (int*)O.ptr;
            public int rank;
            public fixed int dims[TensorShape.maxRank];

            public void Execute()
            {
                for (var i = 0; i < rank; i++)
                    Optr[i] = dims[i];
            }
        }
    }
}
