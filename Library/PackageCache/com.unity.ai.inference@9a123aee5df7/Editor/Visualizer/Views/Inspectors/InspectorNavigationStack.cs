using System;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class InspectorNavigationStack : VisualElement
    {
        readonly GraphStoreManager m_StoreManager;
        IconButton m_LeftNavigationBtn, m_RightNavigationBtn;

        public InspectorNavigationStack(GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
            m_StoreManager.Store.Subscribe(state => state.Get<GraphState>(GraphSlice.Name), state =>
            {
                if (state.CurrentSelectionIndex != -1)
                {
                    UpdateVisuals(state);
                }
            });

            InitializeVisuals();
        }

        void InitializeVisuals()
        {
            style.flexDirection = FlexDirection.Row;
            style.paddingBottom = 4;

            m_LeftNavigationBtn = new IconButton
            {
                icon = "caret-left",
                quiet = true
            };

            m_LeftNavigationBtn.clickable.clicked += MoveStackIndexDown;

            m_RightNavigationBtn = new IconButton
            {
                icon = "caret-right",
                quiet = true
            };

            m_RightNavigationBtn.clickable.clicked += MoveStackIndexUp;

            Add(m_LeftNavigationBtn);
            Add(m_RightNavigationBtn);
        }

        void MoveStackIndexUp()
        {
            m_StoreManager.Store.Dispatch(GraphStoreManager.MoveStackIndexUp.Invoke());
        }

        void MoveStackIndexDown()
        {
            m_StoreManager.Store.Dispatch(GraphStoreManager.MoveStackIndexDown.Invoke());
        }

        void UpdateVisuals(GraphState state)
        {
            m_RightNavigationBtn.SetEnabled(state.CurrentSelectionIndex < state.SelectionHistory.Count - 1);
            m_LeftNavigationBtn.SetEnabled(state.CurrentSelectionIndex > 0);
        }
    }
}
