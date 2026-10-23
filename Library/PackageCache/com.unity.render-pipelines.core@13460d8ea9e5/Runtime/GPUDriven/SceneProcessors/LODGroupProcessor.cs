#if !UNITY_WEBGL_RENDERER_ONLY
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Profiling.LowLevel;
using UnityEngine.Assertions;

namespace UnityEngine.Rendering
{
    internal class LODGroupProcessor
    {
        /// <summary>
        /// Frees GPU and CPU state for a set of destroyed LOD group instances by delegating to
        /// <c>LODGroupDataSystem.FreeLODGroups</c>.
        /// </summary>
        static readonly ProfilerMarker k_DestroyLODGroupInstances =
            new ProfilerMarker(ProfilerCategory.Render, "DestroyLODGroupInstances", MarkerFlags.VerbosityAdvanced);

        /// <summary>
        /// Processes a LOD group update batch that allocates GPU instances for new groups and writes
        /// group settings, reference point, world-space size, and LOD buffer data. Transform-only
        /// batches skip allocation and update position and size only.
        /// </summary>
        static readonly ProfilerMarker k_ProcessLODGroupUpdateBatch =
            new ProfilerMarker(ProfilerCategory.Render, "ProcessLODGroupUpdateBatch", MarkerFlags.VerbosityAdvanced);

        private GPUDrivenProcessor m_GPUDrivenProcessor;
        private LODGroupDataSystem m_LODGroupDataSystem;

        public LODGroupProcessor(GPUDrivenProcessor gpuDrivenProcessor, GPUResidentContext context)
        {
            m_GPUDrivenProcessor = gpuDrivenProcessor;
            m_LODGroupDataSystem = context.lodGroupDataSystem;
        }

        public void DestroyInstances(NativeArray<EntityId> destroyedIDs)
        {
            using (k_DestroyLODGroupInstances.Auto())
            {
                m_LODGroupDataSystem.FreeLODGroups(destroyedIDs);
            }
        }

        public void ProcessGameObjectChanges(NativeArray<EntityId> changedLODGroups, bool transformOnly)
        {
            m_GPUDrivenProcessor.DispatchLODGroupData(changedLODGroups, transformOnly, ProcessGameObjectUpdateBatch);
        }

        public void ProcessUpdateBatch(in LODGroupUpdateBatch updateBatch)
        {
            if (updateBatch.TotalLength == 0)
                return;

            using var _ = k_ProcessLODGroupUpdateBatch.Auto();

            if (updateBatch.updateMode == LODGroupUpdateBatchMode.MightIncludeNewInstances)
            {
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.GroupSettings));
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.WorldSpaceReferencePoint));
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.WorldSpaceSize));
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.LODBuffer));

                NativeArray<GPUInstanceIndex> instances = m_LODGroupDataSystem.GetOrAllocateInstances(updateBatch, Allocator.TempJob);
                m_LODGroupDataSystem.UpdateLODGroupData(updateBatch, instances);
                instances.Dispose();
            }
            else if (updateBatch.updateMode == LODGroupUpdateBatchMode.OnlyKnownInstances)
            {
                // This mode only support transform-only updates for now.
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.WorldSpaceSize));
                Assert.IsTrue(updateBatch.HasAnyComponent(LODGroupComponentMask.WorldSpaceReferencePoint));

                m_LODGroupDataSystem.UpdateLODGroupTransforms(updateBatch);
            }
        }

        void ProcessGameObjectUpdateBatch(in GPUDrivenLODGroupData inputData)
        {
            if (inputData.invalidLODGroup.Length > 0)
                DestroyInstances(inputData.invalidLODGroup);

            if (inputData.lodGroup.Length == 0)
                return;

            var updateMode = inputData.transformOnly ? LODGroupUpdateBatchMode.OnlyKnownInstances : LODGroupUpdateBatchMode.MightIncludeNewInstances;

            var updateBatch = new LODGroupUpdateBatch(new LODGroupUpdateSection
            {
                instanceIDs = inputData.lodGroup,
                worldSpaceReferencePoints = inputData.worldSpaceReferencePoint.Reinterpret<float3>(),
                worldSpaceSizes = inputData.worldSpaceSize,
                lodGroupSettings = inputData.groupSettings,
                forceLODMask = inputData.forceLODMask,
                lodBuffers = inputData.lodBuffer
            },
            updateMode,
            Allocator.TempJob);

            updateBatch.Validate();
            ProcessUpdateBatch(updateBatch);

            updateBatch.Dispose();
        }
    }
}

#endif // !UNITY_WEBGL_RENDERER_ONLY
