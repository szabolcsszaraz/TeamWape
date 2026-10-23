using System;
using Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    class LayerNodeData : NodeData
    {
        public override string Name => LayerData?.opName;
        public Layer LayerData { get; private set; }
        public LayerConnectionsHandler.LayerConnectionData[] SentisInputsData { get; set; }
        public LayerConnectionsHandler.LayerConnectionData[] SentisOutputsData { get; set; }

        public override int[] SentisInputs => LayerData.inputs;
        public override int[] SentisOutputs => LayerData.outputs;
        public override int SentisIndex => LayerData.outputs[0];
        public int Category { get; private set; }

        public LayerNodeData(Layer layerData)
        {
            LayerData = layerData;
            SentisInputsData = LayerConnectionsHandler.GetInputsForLayer(layerData).ToArray();
            SentisOutputsData = LayerConnectionsHandler.GetOutputsForLayer(layerData).ToArray();
            Category = LayerCategorizer.GetCategoryForLayer(layerData);
        }

        public override string ToString(Model model)
        {
            var ls = LayerData.ToString();
            var layerType = LayerData.GetType().Name;

            // Extract information from the LayerData string
            return $"<b>{layerType}</b> {ls.Substring(ls.IndexOf('-') + 2)}";
        }
    }
}
