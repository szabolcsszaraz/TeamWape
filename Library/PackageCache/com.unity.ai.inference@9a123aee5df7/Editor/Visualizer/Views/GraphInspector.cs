using System;
using Unity.AppUI.Redux;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class GraphInspector : VisualElement
    {
        const string k_StylePath = "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/GraphInspector.uss";
        readonly InspectorNavigationStack m_InspectorNavigationStack;

        IconButton m_CloseButton;
        ScrollView m_InspectorsParent;
        Tray m_Tray;
        readonly GraphStoreManager m_StoreManager;
        readonly VisualElement m_TrayReferenceView;
        IInspector m_CurrentInspector;
        public Tray Tray => m_Tray;

        public GraphInspector(GraphStoreManager storeManager, VisualElement trayReferenceView)
        {
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylePath);
            styleSheets.Add(sheet);

            m_TrayReferenceView = trayReferenceView;
            m_StoreManager = storeManager;
            m_StoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) =>
            {
                if (state.SelectedObject == null)
                {
                    m_Tray?.Dismiss();
                    m_Tray = null;
                    m_CurrentInspector = null;
                    Clear();
                    return;
                }

                if (!Equals(state.SelectedObject, m_CurrentInspector?.target))
                {
                    InitializeVisuals();
                    InitializeTray();
                }
            });

            m_InspectorNavigationStack = new InspectorNavigationStack(m_StoreManager);

            InitializeVisuals();
        }

        void InitializeVisuals()
        {
            Clear();

            style.flexGrow = 1;

            m_InspectorsParent = new ScrollView
            {
                style =
                {
                    flexGrow = 1,
                    minWidth = 300,
                    maxWidth = 300
                }
            };

            Add(m_InspectorsParent);

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            m_CurrentInspector = InspectorFactory.GetInspectorForObject(state.SelectedObject, m_StoreManager);

            if (m_CurrentInspector != null)
                m_InspectorsParent.Add(m_CurrentInspector.visualElement);

            m_CloseButton = new IconButton
            {
                icon = "x",
                quiet = true,
                style =
                {
                    position = Position.Absolute,
                    alignSelf = Align.FlexEnd,
                    marginTop = 5,
                    marginRight = 5
                }
            };
            m_CloseButton.clickable.clicked += OnClose;
            Add(m_CloseButton);

            Add(m_InspectorNavigationStack);
        }

        void OnClose()
        {
            m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(null));
        }

        void InitializeTray()
        {
            if (m_Tray == null)
            {
                m_Tray = Tray.Build(m_TrayReferenceView, this);
                m_Tray.SetPosition(TrayPosition.Right);
                m_Tray.SetHandleVisible(false);
                m_Tray.view.pickingMode = PickingMode.Ignore;
                m_Tray.view.style.backgroundColor = new Color(0.5f, 0.5f, 0.5f, 0.0f);
                m_Tray.dismissed += (_, _) =>
                {
                    m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(null));
                };

                var trayContent = m_Tray.view.Q<VisualElement>("appui-tray__tray");
                trayContent.style.paddingLeft = 14f;
                trayContent.style.borderTopLeftRadius = 0f;
                trayContent.style.borderBottomLeftRadius = 0f;

                m_Tray.Show();
            }
        }
    }
}
