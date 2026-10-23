#if !UNITY_WEBGL_RENDERER_ONLY
using System;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Jobs;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Profiling;
using Unity.Profiling.LowLevel;

namespace UnityEngine.Rendering
{
    internal class CPUDrawInstanceData
    {
        /// <summary>
        /// Sorts and removes the destroyed draw instance indices from the CPU draw instance, batch, and
        /// range data structures using binary search. Cost scales with total live draw instance count, not just the number removed.
        /// </summary>
        static readonly ProfilerMarker k_DestroyDrawInstanceIndices =
            new ProfilerMarker(ProfilerCategory.Render, "DestroyDrawInstanceIndices.RemoveDrawInstanceIndices", MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Removes a batch of GPU instances from all CPU draw data structures. Sorts instance
        /// handles, maps them to draw instance indices via a parallel job, then removes those
        /// indices. Cost scales with total live draw instance count, not just the number removed.
        /// </summary>
        static readonly ProfilerMarker k_DestroyDrawInstances =
            new ProfilerMarker(ProfilerCategory.Render, "DestroyDrawInstances", MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Sorts the destroyed instance handle array in parallel. A prerequisite for the
        /// binary-search index-lookup job in DestroyDrawInstances.
        /// </summary>
        static readonly ProfilerMarker k_DestroyDrawInstancesParallelSort =
            new ProfilerMarker(ProfilerCategory.Render, "DestroyDrawInstances.ParallelSort", MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Sorts the destroyed batch material ID array in parallel. A prerequisite for the
        /// binary-search index-lookup job in DestroyMaterialDrawInstances.
        /// </summary>
        static readonly ProfilerMarker k_DestroyedBatchMaterialsParallelSort =
            new ProfilerMarker(ProfilerCategory.Render, "DestroyedBatchMaterials.ParallelSort", MarkerFlags.VerbosityAdvanced);

        public NativeList<DrawInstance> drawInstances => m_DrawInstances;
        public NativeParallelHashMap<DrawKey, int> batchHash => m_BatchHash;
        public NativeList<DrawBatch> drawBatches => m_DrawBatches;
        public NativeParallelHashMap<RangeKey, int> rangeHash => m_RangeHash;
        public NativeList<DrawRange> drawRanges => m_DrawRanges;
        public NativeArray<int> drawBatchIndices => m_DrawBatchIndices.AsArray();
        public NativeArray<int> drawInstanceIndices => m_DrawInstanceIndices.AsArray();

        private NativeParallelHashMap<RangeKey, int> m_RangeHash;       // index in m_DrawRanges, hashes by range state
        private NativeList<DrawRange> m_DrawRanges;
        private NativeParallelHashMap<DrawKey, int> m_BatchHash;        // index in m_DrawBatches, hashed by draw state
        private NativeList<DrawBatch> m_DrawBatches;
        private NativeList<DrawInstance> m_DrawInstances;
        private NativeList<int> m_DrawInstanceIndices;          // DOTS instance index, arranged in contiguous blocks in m_DrawBatches order (see DrawBatch.instanceOffset, DrawBatch.instanceCount)
        private NativeList<int> m_DrawBatchIndices;             // index in m_DrawBatches, arranged in contiguous blocks in m_DrawRanges order (see DrawRange.drawOffset, DrawRange.drawCount)

        private bool m_NeedsRebuild;

        public bool valid => m_DrawInstances.IsCreated;

        public void Initialize()
        {
            Assert.IsTrue(!valid);
            m_RangeHash = new NativeParallelHashMap<RangeKey, int>(1024, Allocator.Persistent);
            m_DrawRanges = new NativeList<DrawRange>(Allocator.Persistent);
            m_BatchHash = new NativeParallelHashMap<DrawKey, int>(1024, Allocator.Persistent);
            m_DrawBatches = new NativeList<DrawBatch>(Allocator.Persistent);
            m_DrawInstances = new NativeList<DrawInstance>(1024, Allocator.Persistent);
            m_DrawInstanceIndices = new NativeList<int>(1024, Allocator.Persistent);
            m_DrawBatchIndices = new NativeList<int>(1024, Allocator.Persistent);
        }

        public void Dispose()
        {
            if (m_DrawBatchIndices.IsCreated)
                m_DrawBatchIndices.Dispose();

            if (m_DrawInstanceIndices.IsCreated)
                m_DrawInstanceIndices.Dispose();

            if (m_DrawInstances.IsCreated)
                m_DrawInstances.Dispose();

            if (m_DrawBatches.IsCreated)
                m_DrawBatches.Dispose();

            if (m_BatchHash.IsCreated)
                m_BatchHash.Dispose();

            if (m_DrawRanges.IsCreated)
                m_DrawRanges.Dispose();

            if (m_RangeHash.IsCreated)
                m_RangeHash.Dispose();
        }

        public void RebuildDrawListsIfNeeded()
        {
            if (!m_NeedsRebuild)
                return;

            m_NeedsRebuild = false;

            Assert.IsTrue(m_RangeHash.Count() == m_DrawRanges.Length);
            Assert.IsTrue(m_BatchHash.Count() == m_DrawBatches.Length);

            m_DrawInstanceIndices.ResizeUninitialized(m_DrawInstances.Length);
            m_DrawBatchIndices.ResizeUninitialized(m_DrawBatches.Length);

            var internalDrawIndex = new NativeArray<int>(drawBatches.Length * BuildDrawListsJob.k_IntsPerCacheLine, Allocator.TempJob, NativeArrayOptions.ClearMemory);

            var prefixSumDrawInstancesJob = new PrefixSumDrawInstancesJob()
            {
                rangeHash = m_RangeHash,
                drawRanges = m_DrawRanges,
                drawBatches = m_DrawBatches,
                drawBatchIndices = m_DrawBatchIndices.AsArray()
            };

            var prefixSumJobHandle = prefixSumDrawInstancesJob.Schedule();

            new BuildDrawListsJob()
            {
                drawInstances = m_DrawInstances,
                batchHash = m_BatchHash,
                drawBatches = m_DrawBatches,
                internalDrawIndex = internalDrawIndex,
                drawInstanceIndices = m_DrawInstanceIndices.AsArray(),
            }
            .RunParallel(m_DrawInstances.Length, 128, prefixSumJobHandle);

            internalDrawIndex.Dispose();
        }

        public unsafe void DestroyDrawInstanceIndices(NativeArray<int> drawInstanceIndicesToDestroy)
        {
            using var _ = k_DestroyDrawInstanceIndices.Auto();

            drawInstanceIndicesToDestroy.ParallelSort().Complete();

            CPUDrawInstanceDataBurst.RemoveDrawInstanceIndices(drawInstanceIndicesToDestroy,
                ref m_DrawInstances,
                ref m_RangeHash,
                ref m_BatchHash,
                ref m_DrawRanges,
                ref m_DrawBatches);
        }

        public unsafe void DestroyDrawInstances(NativeArray<InstanceHandle> destroyedInstances)
        {
            if (m_DrawInstances.IsEmpty || destroyedInstances.Length == 0)
                return;

            using var _ = k_DestroyDrawInstances.Auto();

            NeedsRebuild();

            var destroyedInstancesSorted = new NativeArray<InstanceHandle>(destroyedInstances, Allocator.TempJob);
            Assert.AreEqual(UnsafeUtility.SizeOf<InstanceHandle>(), UnsafeUtility.SizeOf<int>());

            using (k_DestroyDrawInstancesParallelSort.Auto())
            {
                destroyedInstancesSorted.Reinterpret<int>().ParallelSort().Complete();
            }

            var drawInstanceIndicesToDestroy = new NativeList<int>(m_DrawInstances.Length, Allocator.TempJob);

            new FindDrawInstancesJob()
            {
                instancesSorted = destroyedInstancesSorted,
                drawInstances = m_DrawInstances,
                outDrawInstanceIndicesWriter = drawInstanceIndicesToDestroy.AsParallelWriter()
            }
            .RunBatchParallel(m_DrawInstances.Length, FindDrawInstancesJob.k_MaxBatchSize);

            DestroyDrawInstanceIndices(drawInstanceIndicesToDestroy.AsArray());

            destroyedInstancesSorted.Dispose();
            drawInstanceIndicesToDestroy.Dispose();
        }

        public unsafe void DestroyMaterialDrawInstances(NativeArray<uint> destroyedBatchMaterials)
        {
            if (m_DrawInstances.IsEmpty || destroyedBatchMaterials.Length == 0)
                return;

            NeedsRebuild();

            var destroyedBatchMaterialsSorted = new NativeArray<uint>(destroyedBatchMaterials, Allocator.TempJob);

            using (k_DestroyedBatchMaterialsParallelSort.Auto())
            {
                destroyedBatchMaterialsSorted.Reinterpret<int>().ParallelSort().Complete();
            }

            var drawInstanceIndicesToDestroy = new NativeList<int>(m_DrawInstances.Length, Allocator.TempJob);

            new FindMaterialDrawInstancesJob()
            {
                materialsSorted = destroyedBatchMaterialsSorted,
                drawInstances = m_DrawInstances,
                outDrawInstanceIndicesWriter = drawInstanceIndicesToDestroy.AsParallelWriter()
            }
            .RunBatchParallel(m_DrawInstances.Length, FindMaterialDrawInstancesJob.k_MaxBatchSize);

            DestroyDrawInstanceIndices(drawInstanceIndicesToDestroy.AsArray());

            destroyedBatchMaterialsSorted.Dispose();
            drawInstanceIndicesToDestroy.Dispose();
        }

        public void NeedsRebuild()
        {
            m_NeedsRebuild = true;
        }
    }
}

#endif // !UNITY_WEBGL_RENDERER_ONLY
