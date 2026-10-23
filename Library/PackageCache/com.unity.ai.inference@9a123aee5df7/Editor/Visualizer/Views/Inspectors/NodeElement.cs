using System;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Manipulators;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class NodeElement : VisualElement
    {
        readonly GraphStoreManager m_storeManager;
        readonly NodeData m_NodeData;

        public NodeElement(GraphStoreManager storeManager, NodeData nodeData, LayerConnectionsHandler.LayerConnectionData layerData)
        {
            m_storeManager = storeManager;
            m_NodeData = nodeData;

            var text = $"{nodeData.Name}: ";
            if (layerData != null)
            {
                text += $"{layerData.name}";
            }

            var label = new InputLabel(text)
            {
                direction = Direction.Horizontal,
                contentContainer =
                {
                    style =
                    {
                        flexDirection = FlexDirection.Row,
                        justifyContent = Justify.FlexStart
                    }
                },
                tooltip = layerData != null ? layerData.name : nodeData.Name
            };

            var labelContainer = label.Q(className: "appui-inputlabel__label-container");
            labelContainer.style.flexGrow = 1;

            var iconButton = new IconButton
            {
                icon = "squares-four",
                tooltip = "Inspect Node",
                quiet = true
            };

            iconButton.clickable.clicked += () =>
            {
                m_storeManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(m_NodeData));
            };
            label.Add(iconButton);

            Add(label);

            this.AddManipulator(new HoverManipulator(m_storeManager, m_NodeData));
        }
    }
}
