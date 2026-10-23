using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Microsoft.Msagl.Layout.Layered;
using Microsoft.Msagl.Miscellaneous;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine;
using Layout = Microsoft.Msagl.Core.Layout;

namespace Unity.InferenceEngine.Editor.Visualizer.GraphData
{
    sealed class GraphLayoutHandler : IDisposable
    {
        GraphState m_State;
        Layout.GeometryGraph m_GeometryGraph;

        public bool LayoutComputed { get; private set; }

        public GraphLayoutHandler(GraphState state)
        {
            m_State = state;
        }

        public void InitializeNodes()
        {
            AddInputNodes();
            AddLayerNodes();
            AddOutputNodes();
            ConnectNodes();
        }

        void AddInputNodes()
        {
            for (var i = 0; i < m_State.Model.inputs.Count; ++i)
            {
                var input = m_State.Model.inputs[i];
                var inputNode = new InputNodeData(input) { Index = m_State.Nodes.Count };
                m_State.Nodes.Add(inputNode);
            }
        }

        void AddLayerNodes()
        {
            for (var i = 0; i < m_State.Model.layers.Count; ++i)
            {
                var layer = m_State.Model.layers[i];
                var layerNode = new LayerNodeData(layer) { Index = m_State.Nodes.Count };
                m_State.Nodes.Add(layerNode);
            }
        }

        void AddOutputNodes()
        {
            for (var i = 0; i < m_State.Model.outputs.Count; ++i)
            {
                var output = m_State.Model.outputs[i];
                var outputNode = new OutputNodeData(output) { Index = m_State.Nodes.Count };
                m_State.Nodes.Add(outputNode);
            }
        }

        void ConnectNodes()
        {
            foreach (var node in m_State.Nodes)
            {
                var outputsSet = new HashSet<int>(node.SentisOutputs);
                var inputsSet = new HashSet<int>(node.SentisInputs);

                foreach (var otherNode in m_State.Nodes)
                {
                    if (node == otherNode)
                        continue;

                    ConnectOutputsToInputs(node, otherNode, outputsSet);
                    ConnectInputsToOutputs(node, otherNode, inputsSet);
                }
            }
        }

        static void ConnectOutputsToInputs(NodeData node, NodeData otherNode, HashSet<int> outputsSet)
        {
            foreach (var input in otherNode.SentisInputs)
            {
                if (outputsSet.Contains(input))
                {
                    node.Outputs.Add(otherNode);
                    break;
                }
            }
        }

        static void ConnectInputsToOutputs(NodeData node, NodeData otherNode, HashSet<int> inputsSet)
        {
            foreach (var output in otherNode.SentisOutputs)
            {
                if (inputsSet.Contains(output))
                {
                    node.Inputs.Add(otherNode);
                    break;
                }
            }
        }

        public void ComputeLayout()
        {
            LayoutComputed = false;

            // Clean up resources before creating new ones
            Clean();

            m_GeometryGraph = new Layout.GeometryGraph();
            var nodes = new List<Layout.Node>();

            for (var i = 0; i < m_State.Nodes.Count; i++)
            {
                var node = m_State.Nodes[i];
                var nodeLayout = new Layout.Node(CurveFactory.CreateRectangle(node.CanvasSize.x, node.CanvasSize.y, new Point(0f, 0f)))
                {
                    UserData = node
                };

                nodes.Add(nodeLayout);
                m_GeometryGraph.Nodes.Add(nodeLayout);
            }

            foreach (var node in nodes)
            {
                var visualizerNode = (NodeData)node.UserData;
                var remainingOutputs = new List<NodeData>(visualizerNode.Outputs);

                // Local helper function to create and add edges
                void CreateAndAddEdge(NodeData targetNodeData, int tensorIndex)
                {
                    var edge = new Layout.Edge(node, nodes[targetNodeData.Index]) { UserData = visualizerNode };
                    edge.TargetPort = new Layout.RelativeFloatingPort(
                        () => CurveFactory.CreateRectangle(10, 10, new Point()),
                        () => edge.Target.Center,
                        new Point(0, edge.Target.Height / 2f));
                    edge.SourcePort = new Layout.RelativeFloatingPort(
                        () => CurveFactory.CreateRectangle(10, 10, new Point()),
                        () => edge.Source.Center,
                        new Point(0, -edge.Source.Height / 2f));
                    m_State.Edges.Add(new EdgeData(edge, visualizerNode, targetNodeData, tensorIndex));
                    m_GeometryGraph.Edges.Add(edge);
                }

                // Process specific Sentis outputs first
                for (var i = 0; i < visualizerNode.SentisOutputs.Length; ++i)
                {
                    var sentisOutput = visualizerNode.SentisOutputs[i];
                    var matchingOutput = remainingOutputs.FirstOrDefault(x => x.SentisInputs.Contains(sentisOutput));

                    if (matchingOutput != null)
                    {
                        CreateAndAddEdge(matchingOutput, sentisOutput);
                        remainingOutputs.Remove(matchingOutput);
                    }
                }

                // Process any remaining outputs
                foreach (var output in remainingOutputs)
                {
                    CreateAndAddEdge(output, visualizerNode.SentisIndex);
                }
            }

            var setting = new SugiyamaLayoutSettings();
            LayoutHelpers.CalculateLayout(m_GeometryGraph, setting, null);

            for (var i = 0; i < m_State.Nodes.Count; i++)
            {
                var node = nodes[i];
                var inode = (NodeData)node.UserData;
                inode.CanvasPosition = new Vector2((float)node.Center.X, -(float)node.Center.Y - inode.CanvasSize.y / 2f); //We invert y, so it's top -> bottom
            }

            LayoutComputed = true;
        }

        void Clean()
        {
            // Dispose of the previous geometry graph if it exists
            if (m_GeometryGraph != null)
            {
                // Clear references to allow for garbage collection
                m_GeometryGraph.Nodes.Clear();
                m_GeometryGraph.Edges.Clear();

                m_GeometryGraph = null;
            }
        }

        public void Dispose()
        {
            Clean();
        }
    }
}
