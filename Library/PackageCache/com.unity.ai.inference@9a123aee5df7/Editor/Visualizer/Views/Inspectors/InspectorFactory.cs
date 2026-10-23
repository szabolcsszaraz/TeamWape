using System;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    static class InspectorFactory
    {
        public static IInspector GetInspectorForObject(object obj, GraphStoreManager storeManager)
        {
            if (obj == null)
                return null;
            switch (obj)
            {
                case NodeData node:
                {
                    var inspector = new NodeInspector(storeManager);
                    inspector.SetNode(node);
                    return inspector;
                }

                case int index:
                {
                    var inspector = new TensorInspector(storeManager);
                    inspector.SetIndex(index);
                    return inspector;
                }

                default:
                    throw new ArgumentOutOfRangeException($"{obj.GetType()} is not a valid inspector target");
            }
        }
    }
}
