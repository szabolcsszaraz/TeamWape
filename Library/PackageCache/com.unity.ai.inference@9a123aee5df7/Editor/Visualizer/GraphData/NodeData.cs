using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    abstract class NodeData
    {
        public abstract string Name { get; }

        public Vector2 CanvasPosition { get; set; }
        public Vector2 CanvasSize { get; set; }
        public List<NodeData> Inputs { get; set; } = new();
        public List<NodeData> Outputs { get; set; } = new();
        public int Index { get; set; }

        public abstract int[] SentisInputs { get; }
        public abstract int[] SentisOutputs { get; }
        public abstract int SentisIndex { get; }

        public abstract string ToString(Model model);

        protected static string FormatShapeAndDataType(Model model, int index, string fallbackShape = "?", string fallbackDataType = "?")
        {
            var shape = model.GetShape(index);
            var dataType = model.GetDataType(index);

            return $"shape: {(shape.HasValue ? model.DynamicShapeToString(shape.Value) : fallbackShape)}, " +
                $"dataType: {(dataType.HasValue ? dataType.Value : fallbackDataType)}";
        }
    }
}
