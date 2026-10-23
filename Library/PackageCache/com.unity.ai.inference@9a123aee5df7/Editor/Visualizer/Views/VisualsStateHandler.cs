using System;
using System.Collections.Generic;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    /// <summary>
    ///     Manages the visual state of nodes and edges in the graph visualization.
    ///     This class optimizes performance by handling state updates directly for visual elements
    ///     rather than relying on individual store subscriptions for each element. It centralizes
    ///     hover and selection state management, dispatching updates only to affected elements
    ///     when state changes occur.
    /// </summary>
    class VisualsStateHandler : Manipulator
    {
        readonly GraphStoreManager m_GraphStoreManager;
        readonly Dictionary<NodeData, NodeView> m_NodeViews;
        readonly Dictionary<int, List<EdgeView>> m_EdgeViews = new();

        List<object> m_PreviousHoveredObjects;
        object m_PreviousSelectedObject;

        IDisposableSubscription m_HoveredObjectsSubscription;
        IDisposableSubscription m_SelectedObjectSubscription;

        public VisualsStateHandler(GraphStoreManager storeManager, Dictionary<NodeData, NodeView> nodeViews, Dictionary<EdgeData, EdgeView> edgeViews)
        {
            m_GraphStoreManager = storeManager;
            m_NodeViews = nodeViews;

            foreach (var edges in edgeViews)
            {
                var tensorIndex = edges.Key.TensorIndex;
                if (!m_EdgeViews.TryGetValue(tensorIndex, out var edgeViewList))
                {
                    edgeViewList = new List<EdgeView>();
                    m_EdgeViews.Add(tensorIndex, edgeViewList);
                }

                edgeViewList.Add(edges.Value);
            }
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_HoveredObjectsSubscription = m_GraphStoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) => state.HoveredObjects, hovered =>
            {
                var state = m_GraphStoreManager.Store.GetState<GraphState>(GraphSlice.Name);
                foreach (var obj in hovered)
                {
                    m_PreviousHoveredObjects?.Remove(obj);
                    NotifyView(state, obj);
                }

                if (m_PreviousHoveredObjects != null)
                {
                    foreach (var obj in m_PreviousHoveredObjects)
                    {
                        NotifyView(state, obj);
                    }
                }

                m_PreviousHoveredObjects = new List<object>(hovered);
            });

            m_SelectedObjectSubscription = m_GraphStoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) => state.SelectedObject, selected =>
            {
                var state = m_GraphStoreManager.Store.GetState<GraphState>(GraphSlice.Name);

                if (m_PreviousSelectedObject != null)
                {
                    NotifyView(state, m_PreviousSelectedObject);
                }

                if (selected != null)
                {
                    NotifyView(state, selected);
                }

                m_PreviousSelectedObject = selected;
            });
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            m_HoveredObjectsSubscription?.Dispose();
            m_SelectedObjectSubscription?.Dispose();
            m_HoveredObjectsSubscription = null;
            m_SelectedObjectSubscription = null;
        }

        void NotifyView(GraphState state, object obj)
        {
            switch (obj)
            {
                case NodeData node:
                {
                    if (m_NodeViews.TryGetValue(node, out var nodeView))
                    {
                        nodeView.UpdateState(state);
                    }

                    break;
                }

                case int tensorIndex:
                {
                    if (m_EdgeViews.TryGetValue(tensorIndex, out var edgeViews))
                    {
                        foreach (var edgeView in edgeViews)
                        {
                            edgeView.UpdateState();
                        }
                    }

                    break;
                }

                default:
                {
                    throw new NotImplementedException();
                }
            }
        }
    }
}
