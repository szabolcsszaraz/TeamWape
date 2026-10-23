using System.Collections.Generic;
using Unity.InferenceEngine.Compiler.Passes.Optimization;
using Unity.InferenceEngine.Graph;
using Unity.InferenceEngine.Layers;
using Unity.Jobs;
using UnityEngine;

namespace Unity.InferenceEngine
{
    class QuantizeConstantsPass
    {
        QuantizationType m_QuantizationType;

        public QuantizeConstantsPass(QuantizationType quantizationType)
        {
            m_QuantizationType = quantizationType;
        }

        static readonly HashSet<string> layersToQuantize = new() { "Conv", "ConvTranspose", "Gather", "Dense", "MatMul", "MatMul2D" };

        public void Run(ref Model model)
        {
            var gm = GraphConverter.ModelToGraphModule(model);

            var constantsToQuantize = new HashSet<Node>();

            foreach (var node in gm.graph.Nodes())
            {
                if (node.op != Node.kOpCallFunction || !layersToQuantize.Contains(node.target))
                    continue;

                foreach (var inputNode in node.inputNodes)
                {
                    if (inputNode.op != Node.kOpGetAttr || inputNode.partialTensor.dataType != DataType.Float)
                        continue;
                    constantsToQuantize.Add(inputNode);
                }
            }

            using var backend = new CPUBackend();
            using var min = new Tensor<float>(new TensorShape());
            using var max = new Tensor<float>(new TensorShape());

            foreach (var constantNode in constantsToQuantize)
            {
                var constantTensor = gm.attributes[constantNode.name];
                if (m_QuantizationType == QuantizationType.Float16)
                {
                    var quantizedTensor = new Tensor<short>(constantTensor.shape, data: null);
                    var data = new byte[sizeof(int) * quantizedTensor.count];
                    unsafe
                    {
                        fixed (void* dataPtr = &data[0])
                        fixed (byte* basePtr = constantTensor.array.Array)
                        {
                            var job = new BurstJobsQuantizeTensor.CastFloatToHalfJob
                            {
                                src = (float*)(basePtr + constantTensor.array.Offset),
                                dst = (ushort*)(dataPtr)
                            };
                            var jobHandle = job.Schedule(constantTensor.shape.length, 32);
                            jobHandle.Complete();
                        }
                    }

                    var quantizedConstantTensor = new ConstantTensor(constantTensor.shape, DataType.Short, data);
                    var quantizedConstantNode = GraphPassUtil.AddConstant(gm, constantNode, quantizedConstantTensor);
                    GraphPassUtil.ReplaceNode(constantNode, "Cast", new Argument[] { quantizedConstantNode, DataType.Float });
                }
                else if (m_QuantizationType == QuantizationType.Uint8)
                {
                    using var X = constantTensor.ToTensor() as Tensor<float>;
                    backend.ReduceMin(X, min, null);
                    backend.ReduceMax(X, max, null);
                    min.CompleteAllPendingOperations();
                    max.CompleteAllPendingOperations();
                    var minValue = min.GetItem<float>(0);
                    var maxValue = max.GetItem<float>(0);
                    float scale = (Mathf.Max(0, maxValue) - Mathf.Min(0, minValue)) / 255f;
                    byte zeroPoint = (byte)Mathf.RoundToInt(Mathf.Clamp(-minValue / scale, 0, 255));

                    var quantizedTensor = new Tensor<byte>(constantTensor.shape, null);
                    var data = new byte[sizeof(int) * quantizedTensor.count];
                    unsafe
                    {
                        fixed (void* dataPtr = &data[0])
                        fixed (byte* basePtr = constantTensor.array.Array)
                        {
                            var job = new BurstJobsQuantizeTensor.QuantizeUint8Job
                            {
                                src = (float*)(basePtr + constantTensor.array.Offset),
                                dst = (byte*)(dataPtr),
                                scale = scale,
                                zeroPoint = zeroPoint
                            };
                            var jobHandle = job.Schedule(constantTensor.shape.length, 32);
                            jobHandle.Complete();
                        }
                    }

                    var quantizedConstantTensor = new ConstantTensor(constantTensor.shape, DataType.Byte, data);
                    var quantizedConstantNode = GraphPassUtil.AddConstant(gm, constantNode, quantizedConstantTensor);
                    GraphPassUtil.ReplaceNode(constantNode, "DequantizeUint8", new Argument[] { quantizedConstantNode, scale, zeroPoint });
                }
            }

            var newModel = GraphConverter.GraphToModel(gm);
            newModel.ProducerName = model.ProducerName;
            newModel.symbolicDimNames = model.symbolicDimNames;
            model = newModel;
        }
    }
}
