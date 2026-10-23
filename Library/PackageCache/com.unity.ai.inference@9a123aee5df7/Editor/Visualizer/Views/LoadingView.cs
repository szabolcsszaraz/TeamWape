using System;
using Unity.AppUI.Redux;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class LoadingView : VisualElement
    {
        const string k_StylePath = "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/LoadingView.uss";
        const string k_SentisImagePath = "Packages/com.unity.ai.inference/Editor/d_InferenceEngineModel.png";

        GraphStoreManager m_StoreManager;
        IDisposableSubscription m_StoreSub;
        readonly Text m_LoadingText;
        readonly CircularProgress m_Loader;
        readonly Image m_LoadingSpinnerImage;

        public LoadingView()
        {
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylePath);
            styleSheets.Add(styleSheet);

            m_Loader = new CircularProgress
            {
                innerRadius = 0.45f
            };

            m_LoadingSpinnerImage = m_Loader.Q<Image>("appui-progress__image");

            var image = new Image
            {
                image = AssetDatabase.LoadAssetAtPath<Texture2D>(k_SentisImagePath)
            };

            image.AddToClassList("loader-image");
            m_Loader.hierarchy.Add(image);
            Add(m_Loader);

            m_LoadingText = new Text
            {
                size = TextSize.L
            };
            m_LoadingText.AddToClassList("loader-text");
            Add(m_LoadingText);

            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            SubScribeToStore();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
          UnsubscribeFromStore();
        }

        void SubScribeToStore()
        {
            if(m_StoreManager == null || m_StoreSub != null)
                return;

            m_StoreSub = m_StoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) => state.LoadingStatus, OnLoadingStatusChanged);
        }

        void UnsubscribeFromStore()
        {
            m_StoreSub?.Dispose();
            m_StoreSub = null;
        }

        public void Initialize(GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
            SubScribeToStore();

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            OnLoadingStatusChanged(state.LoadingStatus);
        }

        void OnLoadingStatusChanged(GraphState.LoadingState loadingState)
        {
            style.display = loadingState != GraphState.LoadingState.Done ? DisplayStyle.Flex : DisplayStyle.None;
            m_LoadingText.text = loadingState switch
            {
                GraphState.LoadingState.Idle => "Loading ...",
                GraphState.LoadingState.LoadingModel => "Loading model ...",
                GraphState.LoadingState.LayoutComputation => "Computing layout ...",
                GraphState.LoadingState.Done => "Done.",
                GraphState.LoadingState.Error => "Error loading model.",
                _ => throw new ArgumentOutOfRangeException(nameof(loadingState), loadingState, null)
            };

            m_LoadingSpinnerImage.style.display = loadingState == GraphState.LoadingState.Error? DisplayStyle.None : DisplayStyle.Flex;
            m_LoadingText.EnableInClassList("error-state", loadingState == GraphState.LoadingState.Error);
        }
    }
}
