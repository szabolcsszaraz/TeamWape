using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Burst;
using Unity.InferenceEngine.Graph;

namespace Unity.InferenceEngine.Compiler.Passes.Optimization
{
    class RoundDenormalWeightsPass : GraphPass
    {
        [BurstCompile(OptimizeFor = OptimizeFor.Performance, FloatMode = FloatMode.Default, FloatPrecision = FloatPrecision.Standard, CompileSynchronously = true)]
        unsafe struct RoundDenormalJob : IJobParallelFor
        {
            [NoAlias]
            [NativeDisableUnsafePtrRestriction]
            public uint* ptr;

            public void Execute(int index)
            {
                if (float.IsSubnormal(ptr[index]))
                    ptr[index] = 0;
            }
        }

        /// <summary>
        /// Makes subnormal float values in constant tensors equal to 0.
        /// </summary>
        public override void Run(GraphModule gm)
        {
            foreach (var constantTensor in gm.attributes.Values)
            {
                if (constantTensor.shape.length == 0)
                    continue;

                unsafe
                {
                    fixed (byte* basePtr = constantTensor.array.Array)
                    {
                        byte* segmentPtr = basePtr + constantTensor.array.Offset;
                        var job = new RoundDenormalJob
                        {
                            ptr = (uint*)segmentPtr
                        };
                        var jobHandle = job.Schedule(constantTensor.shape.length, 32);
                        jobHandle.Complete();
                    }
                }
            }
        }
    }
}
