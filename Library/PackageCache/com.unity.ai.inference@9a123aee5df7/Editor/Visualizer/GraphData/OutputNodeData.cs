using System;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    class OutputNodeData : NodeData
    {
        public override string Name => OutputData.name;
        public Model.Output OutputData { get; private set; }

        public override int[] SentisInputs => new[] { OutputData.index };
        public override int[] SentisOutputs => Array.Empty<int>();
        public override int SentisIndex => OutputData.index;

        public OutputNodeData(Model.Output outputData)
        {
            OutputData = outputData;
        }

        public override string ToString(Model model)
        {
            return $"<b>{OutputData.name}</b> index: {OutputData.index}, {FormatShapeAndDataType(model, OutputData.index)}";
        }
    }
}
