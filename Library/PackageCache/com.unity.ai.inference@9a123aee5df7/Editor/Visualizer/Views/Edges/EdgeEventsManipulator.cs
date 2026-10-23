using System;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Edges
{
    class EdgeEventsManipulator : Manipulator
    {
        readonly GraphStoreManager m_GraphStoreManager;
        GraphState m_GraphState;

        int m_LastHoveredIndex = -1;
        IDisposableSubscription m_StoreUnsub;

        public EdgeEventsManipulator(GraphStoreManager storeManager)
        {
            m_GraphStoreManager = storeManager;

            var latestState = m_GraphStoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            OnStateChanged(latestState);
        }

        void OnStateChanged(GraphState state)
        {
            m_GraphState = state;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            target.RegisterCallback<PointerDownEvent>(OnPointerDown);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);

            m_StoreUnsub = m_GraphStoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) =>
            {
                OnStateChanged(state);
            });
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
            target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);

            m_StoreUnsub?.Dispose();
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            var edge = GetClosestEdge(evt);

            if (edge != null && edge.TensorIndex != m_LastHoveredIndex)
            {
                m_LastHoveredIndex = edge.TensorIndex;
                m_GraphStoreManager.Store.Dispatch(GraphStoreManager.AddHoveredObject.Invoke(edge.TensorIndex));
            }
            else if (edge == null && m_LastHoveredIndex != -1)
            {
                m_GraphStoreManager.Store.Dispatch(GraphStoreManager.RemoveHoveredObject.Invoke(m_LastHoveredIndex));
                m_LastHoveredIndex = -1;
            }
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            var edge = GetClosestEdge(evt);

            if (edge != null)
            {
                evt.StopImmediatePropagation();
                evt.StopPropagation();

                m_GraphStoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(edge.TensorIndex));
            }
        }

        EdgeData GetClosestEdge(IPointerEvent evt)
        {
            var point = ((GraphView)target).contentContainer.WorldToLocal(evt.position);
            return EdgeEventsUtils.GetTouchingEdge(m_GraphState, point);
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (m_LastHoveredIndex != -1)
            {
                m_GraphStoreManager.Store.Dispatch(GraphStoreManager.RemoveHoveredObject.Invoke(m_LastHoveredIndex));
                m_LastHoveredIndex = -1;
            }
        }
    }
}
