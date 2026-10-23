using System;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    interface IInspector
    {
        public object target { get; }
        public VisualElement visualElement { get; }
    }
}
