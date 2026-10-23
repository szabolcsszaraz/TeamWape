using System;
using System.Linq;
using Unity.AppUI.Redux;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.Extensions;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class TensorInspector : VisualElement, IInspector
    {
        ScrollView m_ScrollView;
        readonly GraphStoreManager m_StoreManager;
        const string k_VisualTreePath = "Packages/com.unity.ai.inference/Editor/Visualizer/VisualTrees/Inspector.uxml";
        public object target { get; private set; }
        public VisualElement visualElement => this;

        public TensorInspector(GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
        }

        public void SetIndex(int tensorIndex)
        {
            target = tensorIndex;

            Clear();

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            var model = state.Model;

            LoadVisualTree();
            ConfigureHeading(model, tensorIndex);

            ConfigureFrameButton(tensorIndex, model.IsConstant(tensorIndex));

            m_ScrollView = this.Q<ScrollView>("InspectorScrollView");
            m_ScrollView.Clear();

            AddBasicProperties(tensorIndex, model);

            AddSourceNode(state, tensorIndex);

            AddUses(state, tensorIndex);

            AddPartialTensorValue(model, tensorIndex);
        }

        void LoadVisualTree()
        {
            var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_VisualTreePath);
            visualTree.CloneTree(this);
        }

        void ConfigureHeading(Model model, int tensorIndex)
        {
            var heading = this.Q<Heading>("InspectorHeading");
            heading.style.unityFontStyleAndWeight = FontStyle.Bold;
            heading.text = model.IsConstant(tensorIndex) ? "Constant Properties" : "Tensor Properties";
        }

        void ConfigureFrameButton(int tensorIndex, bool isConstant)
        {
            var frameButton = this.Q<IconButton>();
            frameButton.enabledSelf = !isConstant;
            frameButton.clickable.clicked += () =>
            {
                m_StoreManager.Store.Dispatch(GraphStoreManager.SetFocusedObject.Invoke(
                    new FocusData(tensorIndex, Vector2.zero, GraphView.ZoomLevel.MinZoom, false)));
            };
        }

        void AddBasicProperties(int tensorIndex, Model model)
        {
            var indexLabel = new InspectorProperty("index", tensorIndex.ToString());
            m_ScrollView.Add(indexLabel);

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            var partialTensor = state.PartialInferenceContext.GetPartialTensor(tensorIndex);

            var dataTypeLabel = new InspectorProperty("dataType", partialTensor.dataType.ToString());
            m_ScrollView.Add(dataTypeLabel);

            var shapeStr = model.DynamicShapeToString(partialTensor.shape);
            var shapeLabel = new InspectorProperty("shape", shapeStr);
            m_ScrollView.Add(shapeLabel);
        }

        void AddSourceNode(GraphState state, int tensorIndex)
        {
            var sourceNode = state.Nodes.Find(x => x.SentisOutputs.Contains(tensorIndex));
            if (sourceNode == null) return;

            m_ScrollView.Add(new Divider { direction = Direction.Horizontal });
            m_ScrollView.Add(new Heading("Source")
            {
                size = HeadingSize.XS,
                style = { unityFontStyleAndWeight = FontStyle.Bold }
            });

            var sourceLabel = CreateNodeElement(sourceNode, tensorIndex, true);
            m_ScrollView.Add(sourceLabel);
        }

        void AddUses(GraphState state, int tensorIndex)
        {
            var uses = state.Nodes.Where(x => x.SentisInputs.Contains(tensorIndex)).ToList();
            if (uses.Count == 0) return;

            m_ScrollView.Add(new Divider { direction = Direction.Horizontal });
            m_ScrollView.Add(new Heading("Uses")
            {
                size = HeadingSize.XS,
                style = { unityFontStyleAndWeight = FontStyle.Bold }
            });

            foreach (var source in uses)
            {
                var usesLabel = CreateNodeElement(source, tensorIndex, false);
                m_ScrollView.Add(usesLabel);
            }
        }

        NodeElement CreateNodeElement(NodeData node, int tensorIndex, bool isOutput)
        {
            if (node is LayerNodeData layer)
            {
                var index = 0;
                var connections = isOutput ? node.SentisOutputs : node.SentisInputs;
                for (var i = 0; i < connections.Length; ++i)
                {
                    if (connections[i] == tensorIndex)
                    {
                        index = i;
                        break;
                    }
                }

                var connectionData = isOutput ? layer.SentisOutputsData[index] : layer.SentisInputsData[index];
                return new NodeElement(m_StoreManager, node, connectionData);
            }

            return new NodeElement(m_StoreManager, node, null);
        }

        void AddPartialTensorValue(Model model, int tensorIndex)
        {
            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            var partialTensor = state.PartialInferenceContext.GetPartialTensor(tensorIndex);

            if (!partialTensor.isPartiallyKnown) return;

            m_ScrollView.Add(new Divider { direction = Direction.Horizontal });

            var valueStr = ExtractFromBracket(model.PartialTensorToString(partialTensor));
            var valueLabel = new InputLabel
            {
                direction = Direction.Vertical,
                label = "value",
                tooltip = "Value of the tensor"
            };
            valueLabel.Add(new TextArea(valueStr)
            {
                isReadOnly = true,
                style = { flexGrow = 1 }
            });

            m_ScrollView.Add(valueLabel);
        }

        static string ExtractFromBracket(string input)
        {
            // Check for null or empty input
            if (string.IsNullOrEmpty(input))
                return string.Empty;

            // Find the position of the first '['
            var bracketIndex = input.IndexOf('[');

            // If '[' is found, return everything from that position to the end
            return bracketIndex >= 0
                ? input.Substring(bracketIndex)
                :

                // If no '[' is found, return empty string
                string.Empty;
        }
    }
}
