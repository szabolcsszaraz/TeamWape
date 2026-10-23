using System;
using Unity.AppUI.UI;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class InspectorProperty : InputLabel
    {
        public InspectorProperty(string label, string value)
            : base(label)
        {
            direction = Direction.Horizontal;
            tooltip = label;
            Add(new TextField(value)
            {
                isReadOnly = true
            });
        }
    }
}
