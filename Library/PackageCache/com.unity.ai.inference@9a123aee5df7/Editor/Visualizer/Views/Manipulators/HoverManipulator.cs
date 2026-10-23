using System;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Manipulators
{
    class HoverManipulator : Manipulator
    {
        object m_HoverTarget;
        GraphStoreManager m_GraphStoreManager;
        bool IsInitialized => m_HoverTarget != null && m_GraphStoreManager != null;

        public HoverManipulator(GraphStoreManager storeManager, object hoverTarget)
        {
            Initialize(storeManager, hoverTarget);
        }

        public HoverManipulator() {}

        public void Initialize(GraphStoreManager storeManager, object hoverTarget)
        {
            m_HoverTarget = hoverTarget;
            m_GraphStoreManager = storeManager;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.RegisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.UnregisterCallback<PointerEnterEvent>(OnPointerEnter);
            target.UnregisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        void OnPointerEnter(PointerEnterEvent evt)
        {
            if (!IsInitialized)
                return;

            var state = m_GraphStoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            if (state.HoveredObjects.Contains(m_HoverTarget))
                return;

            m_GraphStoreManager.Store.Dispatch(GraphStoreManager.AddHoveredObject.Invoke(m_HoverTarget));
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (!IsInitialized)
                return;

            m_GraphStoreManager.Store.Dispatch(GraphStoreManager.RemoveHoveredObject.Invoke(m_HoverTarget));
        }
    }
}
