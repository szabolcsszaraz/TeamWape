using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine
{
    static class CPUFallbackCalculator
    {
        public static HashSet<int> Calculate(Model model, BackendType backendType, out HashSet<int> layerCPUFallbackShouldFlushGPU)
        {
            // Algorithm:
            // start to gather all CPU seeds:
            //  - all layers that needs a given input to be on the CPU (ie read-back)
            //  - they set their respective inputs to need to run on the CPU
            // foreach layers (starting from the bottom's-up)
            //  if a layer is flagged to need to run on the CPU, all inputs also should run on CPU
            //  exception is holes nodes that operate regardless of their input's data
            // Ex:
            //               c = add   d = concat
            //       ...         \    /
            //        |   s = div(c, d)
            //         \  |
            // t = tile(a, s)
            //      \
            //       mul ...
            // * s is set to need to run on cpu = cpu seed
            // * bottoms up:
            //      - mul -> no cpu skip
            //      - tile -> no cpu skip
            //      - a -> no cpu skip
            //      - s -> is cpu, all inputs (c, d) needs to run on cpu
            //   + continue propagating up to start of graph

            Dictionary<int, int> earliestGPUConsumer = new Dictionary<int, int>();
            layerCPUFallbackShouldFlushGPU = new HashSet<int>();
            // Important: We will identify layers consuming an input with their first output number,
            // ie layer.outputs[0]. This concept of earliest based on that id only works because we
            // assume the model graph has been topologically sorted and that is reflected in the numbering
            // of the inputs/outputs.

            var layerCPUFallback = new HashSet<int>();
            if (backendType == BackendType.CPU)
                return layerCPUFallback;

            for (var i = 0; i < model.layers.Count; i++)
            {
                var layer = model.layers[i];

                for (var j = 0; j < layer.inputs.Length; j++)
                {
                    var input = layer.inputs[j];
                    if (input == -1)
                        continue;

                    if (layer.IsInputCPURead(j))
                        layerCPUFallback.Add(input);
                }
            }

            for (var i = model.layers.Count - 1; i >= 0; i--)
            {
                var layer = model.layers[i];

                var isLayerCPU = false;

                foreach (var output in layer.outputs)
                {
                    isLayerCPU |= layerCPUFallback.Contains(output);
                }

                if (!isLayerCPU)
                    continue;

                // Make sure to add all its other outputs if they are not already there as these
                // are also considered generated on the CPU: this is important for multi-out layers like TopK
                foreach (var output in layer.outputs)
                    layerCPUFallback.Add(output);

                for (var j = 0; j < layer.inputs.Length; j++)
                {
                    var input = layer.inputs[j];
                    if (input == -1)
                        continue;

                    if (!layer.IsInputNoDataDependency(j))
                        layerCPUFallback.Add(input);
                }
            }

            // We repeat the forward pass now that we know for sure which layer will fallback to CPU:
            // This is because we need to identify not only the earliest consumer of an input,
            // but the earliest GPU consumer (see layerCPUFallbackShouldFlushGPU), as there could be early consumer
            // that is running on the CPU but a later GPU consumer before another later CPU fallback layer
            // consumes the same input again, and this earlier CPU consumer would hide the need to flush
            // otherwise:
            for (var i = 0; i < model.layers.Count; i++)
            {
                var layer = model.layers[i];

                for (var j = 0; j < layer.inputs.Length; j++)
                {
                    var input = layer.inputs[j];
                    if (input == -1)
                        continue;

                    bool isLayerRunningOnGPU = !layerCPUFallback.Contains(layer.outputs[0]);
                    if (isLayerRunningOnGPU && (!earliestGPUConsumer.TryGetValue(input, out int earliestUser) || (layer.outputs[0] < earliestUser)))
                        earliestGPUConsumer[input] = layer.outputs[0]; // we identify current layer by its first output
                }
            }

            // Use of layerCPUFallbackShouldFlushGPU:
            //
            // A layer earlier than another not flagged for CPU fallback - that can thus potentially run on GPUCompute -
            // can use the same input as one of the CPU fallback layer.
            // This layer will thus appear in a Schedule call before us and it can try to transfer the tensor to eg GPU.
            // Since scheduling doesn't kick-off the compute buffer, only queue commands,
            // we could reach scheduling of the present later CPU layer before the earliest consumer kernel runs,
            // pin() the input to move it back to CPU, and this would destroy the tensor data
            // (hence destroy the underlying compute buffer) that had already been bound in the GPU command queue!
            //
            // This hashset allows to detect that condition

            // Do one last forward pass - in the order the layers are going to be scheduled by the worker
            // - so that we can map exactly when a layer that falls back to CPU is also accessing
            // input tensor data from an earlier consumer that is NOT flagged to be ran on the CPU.
            // We can only be sure of the later after having ran the backward pass above.
            for (var i = 0; i < model.layers.Count; i++)
            {
                var l = model.layers[i];
                // Only check for flush when we fallback on CPU:
                if (layerCPUFallback.Contains(l.outputs[0]))
                {
                    for (int ii = 0; ii < l.inputs.Length; ii++)
                        if (earliestGPUConsumer.TryGetValue(l.inputs[ii], out int earliestGPUUser) && (l.outputs[0] > earliestGPUUser))
                        {
                            // Make sure to add all its other outputs if they are not already there as these
                            // are also considered generated on the CPU:
                            foreach (int outidx in l.outputs)
                                layerCPUFallbackShouldFlushGPU.Add(outidx);

                            // Remove the input in question in the dictionary when we found and flagged the earliest CPU consumer to flush the GPU queue
                            // so another later potential CPU consumer - even if later than the earliest GPU consumer (ie fitting the criteria to flush) -
                            // is found, as by that point, we already flushed the queue for that problematic input:
                            earliestGPUConsumer.Remove(l.inputs[ii]);
                        }
                }
            }

            return layerCPUFallback;
        }
    }
}
