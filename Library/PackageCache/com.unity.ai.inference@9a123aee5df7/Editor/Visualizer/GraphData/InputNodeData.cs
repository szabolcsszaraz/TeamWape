using System;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    class InputNodeData : NodeData
    {
        public override string Name => InputData.name;
        public Model.Input InputData { get; private set; }

        public override int[] SentisInputs => Array.Empty<int>();
        public override int[] SentisOutputs => new[] { InputData.index };
        public override int SentisIndex => InputData.index;

        public InputNodeData(Model.Input inputData)
        {
            InputData = inputData;
        }

        public override string ToString(Model model)
        {
            return $"<b>{InputData.name}</b> index: {InputData.index}, {FormatShapeAndDataType(model, InputData.index, model.DynamicShapeToString(InputData.shape), InputData.dataType.ToString())}";
        }
    }
}
