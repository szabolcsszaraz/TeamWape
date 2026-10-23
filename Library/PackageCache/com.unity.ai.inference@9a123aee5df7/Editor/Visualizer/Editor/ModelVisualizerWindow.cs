using System;
using System.Collections.Generic;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
#if SENTIS_ANALYTICS_ENABLED
using Unity.InferenceEngine.Editor.Visualizer.Editor.Analytics;
#endif

namespace Unity.InferenceEngine.Editor.Visualizer.Editor
{
    class ModelVisualizerWindow : EditorWindow
    {
        const string k_WindowTitle = "Model Visualizer";
        static readonly string[] k_DarkStylePaths =
        {
            "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI - Editor Dark - Small.tss",
            "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/View_dark.uss",
            "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/View.uss"
        };

        static readonly string[] k_LightStylePaths =
        {
            "Packages/com.unity.dt.app-ui/PackageResources/Styles/Themes/App UI - Editor Light - Small.tss",
            "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/View_light.uss",
            "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/View.uss"
        };

        GraphStoreManager m_StoreManager;
        GraphView m_Canvas;
        LoadingView m_LoadingView;

        [SerializeField]
        ModelAsset m_ModelAsset;

        internal ModelAsset ModelAsset => m_ModelAsset;
        internal GraphView Canvas => m_Canvas;
        internal GraphStoreManager StoreManager => m_StoreManager;

        public void CreateGUI()
        {
            rootVisualElement.Clear();
            rootVisualElement.AddToClassList("unity-editor");
            titleContent = new GUIContent(k_WindowTitle);
            var panel = new Panel();
            rootVisualElement.Add(panel);
            panel.style.width = Length.Percent(100);
            panel.style.height = Length.Percent(100);

            m_Canvas = new GraphView
            {
                style =
                {
                    width = Length.Percent(100),
                    height = Length.Percent(100)
                }
            };

            m_Canvas.controlScheme = ModelVisualizerSettings.instance.CanvasControlScheme;
            ModelVisualizerSettings.instance.OnPropertiesModified += OnProjectSettingsSaved;

            panel.Add(m_Canvas);

            m_LoadingView = new LoadingView();
            panel.Add(m_LoadingView);

            var currentTheme = EditorGUIUtility.isProSkin switch
            {
                true => new List<string>(k_DarkStylePaths),
                false => new List<string>(k_LightStylePaths)
            };

            foreach (var theme in currentTheme)
            {
                var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(theme);
                if (styleSheet != null)
                {
                    rootVisualElement.styleSheets.Add(styleSheet);
                }
            }

            if (m_ModelAsset != null)
                Initialize(m_ModelAsset);
        }

        internal void Initialize(ModelAsset modelAsset)
        {
            m_ModelAsset = modelAsset;

            m_StoreManager = new GraphStoreManager(modelAsset, m_Canvas);
            m_Canvas.Initialize(m_StoreManager);
            m_LoadingView.Initialize(m_StoreManager);

            titleContent = new GUIContent(modelAsset == null ? k_WindowTitle : modelAsset.name);

#if SENTIS_ANALYTICS_ENABLED
            // Send analytics event when a model is opened
            if (modelAsset != null)
            {
                var assetPath = AssetDatabase.GetAssetPath(modelAsset);
                var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
                ModelVisualizerAnalytics.SendOpenEvent(assetGuid);
            }
#endif
        }

        internal static ModelVisualizerWindow VisualizeModel(ModelAsset modelAsset)
        {
            var desiredDockNextTo = new[]
            {
                Type.GetType("UnityEditor.GameView,UnityEditor"),
                typeof(ModelVisualizerWindow)
            };

            var wnd = CreateWindow<ModelVisualizerWindow>(desiredDockNextTo);
            wnd.Initialize(modelAsset);
            return wnd;
        }

        public void OnReimport()
        {
            CleanForReuse();
            CreateGUI();
        }

        internal void CleanForReuse()
        {
            ModelVisualizerSettings.instance.OnPropertiesModified -= OnProjectSettingsSaved;

            m_Canvas = null;
            rootVisualElement.Clear();
            m_StoreManager?.Dispose();
        }

        void OnProjectSettingsSaved()
        {
            m_Canvas.controlScheme = ModelVisualizerSettings.instance.CanvasControlScheme;
        }

        void OnDestroy()
        {
            if (m_StoreManager != null)
            {
                m_StoreManager.Dispose();
                m_StoreManager = null;
            }

            if (m_Canvas != null)
            {
                m_Canvas.RemoveFromHierarchy();
                m_Canvas = null;
            }

            m_ModelAsset = null;

            ModelVisualizerSettings.instance.OnPropertiesModified -= OnProjectSettingsSaved;
        }
    }
}
