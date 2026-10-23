using System;
using System.Collections.Generic;
using Unity.AppUI.Redux;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class NodeInspector : VisualElement, IInspector
    {
        readonly GraphStoreManager m_StoreManager;
        ScrollView m_ScrollView;
        const string k_VisualTreePath = "Packages/com.unity.ai.inference/Editor/Visualizer/VisualTrees/Inspector.uxml";
        public object target { get; private set; }
        public VisualElement visualElement => this;
        static readonly Dictionary<string, string> k_PropertyLabelMappings = new()
        {
            { "opName", "operator" }
        };

        public NodeInspector(GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
        }

        public void SetNode(NodeData nodeData)
        {
            target = nodeData;

            Clear();

            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_VisualTreePath);

            visualTree.CloneTree(this);

            var heading = this.Q<Heading>("InspectorHeading");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;

            heading.text = nodeData switch
            {
                InputNodeData => "Input Properties",
                OutputNodeData => "Output Properties",
                LayerNodeData => "Layer Properties",
                _ => "Node Properties"
            };

            var frameButton = this.Q<IconButton>();
            frameButton.clickable.clicked += () =>
            {
                m_StoreManager.Store.Dispatch(GraphStoreManager.SetFocusedObject.Invoke(
                    new FocusData(nodeData, Vector2.zero, GraphView.ZoomLevel.MinZoom, false)));
            };

            m_ScrollView = this.Q<ScrollView>("InspectorScrollView");
            m_ScrollView.Clear();

            AddPropertiesSection(nodeData);
            AddInputs(nodeData);
            AddOutputs(nodeData);
        }

        void AddPropertiesSection(NodeData nodeData)
        {
            var excludedProperties = new[] { "inputs", "outputs" };

            switch (nodeData)
            {
                case InputNodeData inputNode:
                    AddProperties(inputNode.InputData, excludedProperties);
                    break;
                case OutputNodeData outputNode:
                    AddProperties(outputNode.OutputData, excludedProperties);
                    break;
                case LayerNodeData layerNode:
                    AddProperties(layerNode.LayerData, excludedProperties);
                    break;
            }
        }

        void AddInputs(NodeData nodeData)
        {
            if (nodeData.SentisInputs?.Length == 0)
                return;

            m_ScrollView.Add(new Divider
            {
                direction = Direction.Horizontal
            });

            var headingText = nodeData is OutputNodeData ? "Tensor" : "Inputs";

            m_ScrollView.Add(new Heading(headingText)
            {
                size = HeadingSize.XS,
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });

            if (nodeData is LayerNodeData layerNode)
            {
                for (var i = 0; i < layerNode.SentisInputsData.Length; ++i)
                {
                    var input = layerNode.SentisInputsData[i];
                    var tensorElement = new TensorElement(m_StoreManager, input);
                    m_ScrollView.Add(tensorElement);
                }
            }
            else
            {
                for (var i = 0; i < nodeData.SentisInputs.Length; i++)
                {
                    var input = nodeData.SentisInputs[i];
                    var tensorElement = new TensorElement(m_StoreManager, new LayerConnectionsHandler.LayerConnectionData(i, input, string.Empty, input.ToString()));
                    m_ScrollView.Add(tensorElement);
                }
            }
        }

        void AddOutputs(NodeData nodeData)
        {
            if (nodeData.SentisOutputs?.Length == 0)
                return;

            m_ScrollView.Add(new Divider
            {
                direction = Direction.Horizontal
            });

            var headingText = nodeData is InputNodeData ? "Tensor" : "Outputs";

            m_ScrollView.Add(new Heading(headingText)
            {
                size = HeadingSize.XS,
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold
                }
            });

            if (nodeData is LayerNodeData layerNode)
            {
                for (var i = 0; i < layerNode.SentisOutputsData.Length; ++i)
                {
                    var output = layerNode.SentisOutputsData[i];
                    var tensorElement = new TensorElement(m_StoreManager, output);
                    m_ScrollView.Add(tensorElement);
                }
            }
            else
            {
                for (var i = 0; i < nodeData.SentisOutputs.Length; i++)
                {
                    var output = nodeData.SentisOutputs[i];
                    var tensorElement = new TensorElement(m_StoreManager, new LayerConnectionsHandler.LayerConnectionData(i, output, string.Empty, output.ToString()));
                    m_ScrollView.Add(tensorElement);
                }
            }
        }

        void AddProperties(object instance, string[] excludedProperties)
        {
            var model = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name).Model;
            foreach (var prop in InspectorUtils.GetPublicProperties(instance, model, excludedProperties))
            {
                var propName = prop.name;
                if (k_PropertyLabelMappings.TryGetValue(prop.name, out var label))
                {
                    propName = label;
                }

                var labelElement = new InspectorProperty(propName, prop.value);
                m_ScrollView.Add(labelElement);
            }
        }
    }
}
