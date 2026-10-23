using System;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Components
{
    class MeshSettings : VisualElement
    {
        public event Action OnSettingsApplied;

        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Mesh/Components/MeshSettings/MeshSettings.uxml";

        readonly EnumField m_PivotModeField;

        public MeshSettings()
        {
            RegisterCallback<AttachToPanelEvent>(OnAttachToPanel);
            RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_PivotModeField = this.Q<EnumField>("pivot-mode-field");
            m_PivotModeField.Init(MeshPivotMode.Center);
        }

        void OnAttachToPanel(AttachToPanelEvent _)
        {
            LoadCurrentSettings();

            RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        void OnDetachFromPanel(DetachFromPanelEvent _)
        {
            SaveCurrentSettings();

            UnregisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        }

        void LoadCurrentSettings()
        {
            var settings = this.GetState()?.SelectMeshSettings(this);
            if (settings == null)
                settings = new MeshSettingsState();

            m_PivotModeField.SetValueWithoutNotify(settings.pivotMode);
        }

        void SaveCurrentSettings()
        {
            var newSettings = new MeshSettingsState
            {
                pivotMode = (MeshPivotMode)m_PivotModeField.value,
            };

            this.Dispatch(GenerationSettingsActions.setMeshSettings, newSettings);
            OnSettingsApplied?.Invoke();
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    evt.StopPropagation();
                    SaveCurrentSettings();
                    break;
            }
        }
    }
}