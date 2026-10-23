using System;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Manipulators;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Finder
{
    class FinderElement : Label
    {
        readonly HoverManipulator m_Manipulator;

        public FinderElement()
        {
            m_Manipulator = new HoverManipulator();
            this.AddManipulator(m_Manipulator);
        }

        public void BindItem(GraphStoreManager storeManager, NodeData nodeData)
        {
            m_Manipulator.Initialize(storeManager, nodeData);
            var state = storeManager.Store.GetState<GraphState>(GraphSlice.Name);
            text = nodeData.ToString(state.Model);
        }
    }
}
