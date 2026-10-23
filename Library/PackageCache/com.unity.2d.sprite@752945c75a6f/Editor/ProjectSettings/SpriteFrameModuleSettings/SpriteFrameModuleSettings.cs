using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace UnityEditor.U2D.Sprites.ProjectSettings
{
    class SpriteFrameModuleSettings
    {
        const string k_Uxml = "Packages/com.unity.2d.sprite/Editor/ProjectSettings/SpriteFrameModuleSettings/SpriteFrameModuleSettings.uxml";
        [SpriteEditorWindowProjectSettings("Sprite Frame Module", new[] { "Sprite Frame Module, lock, unlock, capability, override"})]
        public static VisualElement CreateSettingsProvider()
        {
            var styleSheet = EditorGUIUtility.Load("UIPackageResources/Settings/UIToolkitSettingsView.uss") as StyleSheet;
            var ve = new VisualElement();
            var visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            if(visualTreeAsset == null)
            {
                ve.Add(new Label("Unable to load settings UI."));
                Debug.LogError($"Failed to load UI {k_Uxml}.");
                return ve;
            }
            visualTreeAsset.CloneTree(ve);
            if(styleSheet == null)
                Debug.LogError("Failed to load stylesheet for Sprite Frame Module Settings.");
            else
                ve.styleSheets.Add(styleSheet);
            var spriteFrameEditCapabilityOverride = ve.Q<VisualElement>("spriteFrameEditCapabilityOverrideWarning");
            var spriteFrameEditCapabilityToggle = ve.Q<Toggle>("spriteFrameEditCapabilityOverride");
            Action<bool> updateOverrideUI = allowOverride =>
            {
                spriteFrameEditCapabilityToggle.SetValueWithoutNotify(allowOverride);
                spriteFrameEditCapabilityOverride.visible = allowOverride;
                spriteFrameEditCapabilityOverride.style.display = allowOverride ? DisplayStyle.Flex : DisplayStyle.None;
            };
            updateOverrideUI(SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride);
            Action onSettingsChanged = () =>
            {
                updateOverrideUI(SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride);
            };
            ve.RegisterCallback<AttachToPanelEvent>(evt =>
            {
                SpriteFrameModuleSettingsAsset.SettingsChanged += onSettingsChanged;
            });
            ve.RegisterCallback<DetachFromPanelEvent>(evt =>
            {
                SpriteFrameModuleSettingsAsset.SettingsChanged -= onSettingsChanged;
            });
            spriteFrameEditCapabilityToggle.RegisterValueChangedCallback(evt =>
            {
                if (SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride != evt.newValue)
                {
                    SpriteFrameModuleSettingsAsset.allowSpriteFrameEditCapabilityOverride = evt.newValue;
                    SpriteFrameModuleSettingsAsset.Save();
                }
                updateOverrideUI(evt.newValue);
            });
            return ve;
        }
    }
}
