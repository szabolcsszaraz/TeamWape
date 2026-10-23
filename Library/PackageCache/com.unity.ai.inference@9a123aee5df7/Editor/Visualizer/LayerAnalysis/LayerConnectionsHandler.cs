using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis
{
    static class LayerConnectionsHandler
    {
        public record LayerConnectionData(int index, int sentisIndex, string name, string description);

        public static List<LayerConnectionData> GetInputsForLayer(Layer layer)
        {
            var inputData = new List<LayerConnectionData>();
            var inputNames = layer.GetInputNames();
            for (var i = 0; i < layer.inputs.Length; ++i)
            {
                inputData.Add(new LayerConnectionData(i, layer.inputs[i], inputNames[i], null));
            }

            return inputData;
        }

        public static List<LayerConnectionData> GetOutputsForLayer(Layer layer)
        {
            var outputData = new List<LayerConnectionData>();
            var outputNames = layer.GetOutputNames();
            for (var i = 0; i < layer.outputs.Length; ++i)
            {
                outputData.Add(new LayerConnectionData(i, layer.outputs[i], outputNames[i], null));
            }

            return outputData;
        }
    }
}
