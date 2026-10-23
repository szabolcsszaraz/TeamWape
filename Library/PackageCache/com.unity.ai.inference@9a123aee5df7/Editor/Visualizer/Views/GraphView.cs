using System;
using System.Collections.Generic;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Edges;
using Unity.InferenceEngine.Editor.Visualizer.Views.Graph;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Canvas = Unity.AppUI.UI.Canvas;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class GraphView : Canvas
    {
        const string k_StylePath = "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/GraphView.uss";

        GraphStoreManager m_StoreManager;
        GraphInspector m_GraphInspector;
        GraphFinder m_GraphFinder;
        EdgeEventsManipulator m_EdgeEventsManipulator;
        VisualsStateHandler m_VisualsStateHandler;
        ScrollOffsetManipulator m_ScrollOffsetManipulator;

        readonly Dictionary<NodeData, NodeView> m_NodeViews = new();
        readonly Dictionary<EdgeData, EdgeView> m_EdgeViews = new();

        NodeData m_HighestNodeData;
        IDisposableSubscription m_StoreSubscription;

        public Dictionary<NodeData, NodeView> NodeViews => m_NodeViews;
        public Dictionary<EdgeData, EdgeView> EdgeViews => m_EdgeViews;
        public GraphInspector GraphInspector => m_GraphInspector;

        public GraphView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylePath);
            styleSheets.Add(styleSheet);
            maxZoom = 2.5f;
        }

        public void Initialize(GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;

            Clear();

            this.AddManipulator(new FramingManipulator(m_StoreManager));

            m_ScrollOffsetManipulator = new ScrollOffsetManipulator();
            this.AddManipulator(m_ScrollOffsetManipulator);

            m_EdgeEventsManipulator = new EdgeEventsManipulator(m_StoreManager);
            this.AddManipulator(m_EdgeEventsManipulator);

            m_GraphInspector = new GraphInspector(m_StoreManager, this);

            // Setup finder and add it to the panel
            m_GraphFinder = new GraphFinder(this, m_StoreManager);
            var currentIndex = parent.IndexOf(this);

            // Insert after the GraphView in the hierarchy, making sure it's under elements over the canvas
            parent.Insert(currentIndex + 1, m_GraphFinder);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            MarkDirtyRepaint();

            // We use trickle down to intercept the event before base class can consume it
            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        public void InitializeNodeViews()
        {
            Clear();
            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            foreach (var node in state.Nodes)
            {
                var nodeView = new NodeView(m_StoreManager, node);
                m_NodeViews.Add(node, nodeView);
                Add(nodeView);
            }

            m_ScrollOffsetManipulator?.UpdateBoundsMarker();
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.F)
            {
                if (evt.commandKey || evt.ctrlKey) //Handle Ctrl+F/Cmd+F to focus the search bar
                {
                    m_GraphFinder.searchBar.Focus();
                }
                else //Handle view reset
                {
                    m_StoreManager.Store.Dispatch(GraphStoreManager.SetFocusedObject?.Invoke(
                        new FocusData(m_HighestNodeData, Vector2.up, ZoomLevel.MaxZoom, true)));
                }

                evt.StopPropagation();
                evt.StopImmediatePropagation();
            }
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject?.Invoke(null));
        }

        public void UpdateNodesCanvasPosition()
        {
            foreach (var nodeKvp in m_NodeViews)
            {
                var node = nodeKvp.Value;
                node.UpdateCanvasPosition();

                // Track the topmost node for initial framing
                if (m_HighestNodeData == null || nodeKvp.Key.CanvasPosition.y < m_HighestNodeData.CanvasPosition.y)
                {
                    m_HighestNodeData = nodeKvp.Key;
                }
            }

            if (m_VisualsStateHandler != null)
            {
                this.RemoveManipulator(m_VisualsStateHandler);
                m_VisualsStateHandler = null;
            }

            m_VisualsStateHandler = new VisualsStateHandler(m_StoreManager, m_NodeViews, m_EdgeViews);
            this.AddManipulator(m_VisualsStateHandler);

            m_ScrollOffsetManipulator?.UpdateBoundsMarker();

            m_StoreManager.Store.Dispatch(GraphStoreManager.SetFocusedObject?.Invoke(
                new FocusData(m_HighestNodeData, Vector2.up, ZoomLevel.MaxZoom, true)));
        }

        public void CreateEdges()
        {
            foreach (var edge in m_EdgeViews.Values)
            {
                Remove(edge);
            }

            m_EdgeViews.Clear();

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            foreach (var edge in state.Edges)
            {
                var edgeView = new EdgeView(m_StoreManager, edge);
                m_EdgeViews.Add(edge, edgeView);
                Insert(0, edgeView); // Insert at index 0 to render edges behind nodes
            }
        }

        public static class ZoomLevel
        {
            public const float MinZoom = 300f;
            public const float MaxZoom = 800f;
        }
    }
}
