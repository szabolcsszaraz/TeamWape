using System.Collections.Generic;
using System.Linq;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Utilities;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class DimensionsDropDown : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Image/Components/DimensionsDropDown/DimensionsDropDown.uxml";

        readonly DropdownField m_DimensionsDropdown;
        readonly Toggle m_CustomResolutionToggle;
        readonly VisualElement m_CustomResolutionFields;
        readonly IntegerField m_CustomWidthField;
        readonly IntegerField m_CustomHeightField;

        public DimensionsDropDown()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_DimensionsDropdown = this.Q<DropdownField>("dimensions-dropdown");
            m_CustomResolutionToggle = this.Q<Toggle>("custom-resolution-toggle");
            m_CustomResolutionFields = this.Q<VisualElement>("custom-resolution-fields");
            m_CustomWidthField = this.Q<IntegerField>("custom-width-field");
            m_CustomHeightField = this.Q<IntegerField>("custom-height-field");

            m_DimensionsDropdown.RegisterValueChangedCallback(evt =>
            {
                this.Dispatch(GenerationSettingsActions.setImageDimensions, evt.newValue);
            });

            m_CustomResolutionToggle.RegisterValueChangedCallback(OnCustomResolutionToggled);

            m_CustomWidthField.RegisterValueChangedCallback(evt => OnCustomDimensionChanged(evt.newValue, m_CustomHeightField.value));
            m_CustomHeightField.RegisterValueChangedCallback(evt => OnCustomDimensionChanged(m_CustomWidthField.value, evt.newValue));

            this.Use(state => state.SelectModelSettingsSupportsCustomResolutions(this), OnSupportsCustomResolutionChanged);
            this.UseArray(state => state.SelectModelSettingsResolutions(this).ToList(), OnPartnerResolutionsChanged);
            this.Use(state => state.SelectUseCustomResolution(this), OnUseCustomResolutionChanged);
            this.Use(state => state.SelectImageDimensions(this), OnImageDimensionsChanged);
            this.UseAsset(asset => this.SetShown(!asset.IsCubemap()));
        }

        void OnCustomDimensionChanged(int width, int height)
        {
            this.Dispatch(GenerationSettingsActions.setImageDimensions, $"{width} x {height}");
        }

        void OnCustomResolutionToggled(ChangeEvent<bool> evt)
        {
            this.Dispatch(GenerationSettingsActions.setUseCustomResolution, evt.newValue);

            if (evt.newValue)
            {
                var setting = this.GetState().SelectGenerationSetting(this);
                var dimensions = setting.SelectImageDimensionsVector2();
                m_CustomWidthField.value = dimensions.x;
                m_CustomHeightField.value = dimensions.y;
                OnCustomDimensionChanged(dimensions.x, dimensions.y);
            }
            else
            {
                // Switching back to preset dimensions: the custom width/height may have left an
                // out-of-range value in imageDimensions. Reset it to the dropdown's (always valid)
                // value so validation/quoting re-runs and the Generate button updates.
                var presetDimensions = m_DimensionsDropdown.choices != null && m_DimensionsDropdown.choices.Contains(m_DimensionsDropdown.value)
                    ? m_DimensionsDropdown.value
                    : m_DimensionsDropdown.choices?.FirstOrDefault();
                if (presetDimensions != null)
                    this.Dispatch(GenerationSettingsActions.setImageDimensions, presetDimensions);
            }
        }

        void OnUseCustomResolutionChanged(bool useCustom)
        {
            m_CustomResolutionToggle.SetValueWithoutNotify(useCustom);
            m_DimensionsDropdown.SetShown(!useCustom);
            m_CustomResolutionFields.SetShown(useCustom);

            if (useCustom)
            {
                var customDims = this.GetState().SelectGenerationSetting(this).SelectImageDimensionsVector2();
                m_CustomWidthField.SetValueWithoutNotify(customDims.x);
                m_CustomHeightField.SetValueWithoutNotify(customDims.y);
            }
        }

        void OnSupportsCustomResolutionChanged(bool supports)
        {
            m_CustomResolutionToggle.SetShown(supports);
            if (!supports && this.GetState().SelectUseCustomResolution(this))
                this.Dispatch(GenerationSettingsActions.setUseCustomResolution, false);
        }

        void OnImageDimensionsChanged(string dimensions)
        {
            if (this.GetState().SelectUseCustomResolution(this))
            {
                var customDims = this.GetState().SelectGenerationSetting(this).SelectImageDimensionsVector2();
                m_CustomWidthField.SetValueWithoutNotify(customDims.x);
                m_CustomHeightField.SetValueWithoutNotify(customDims.y);
            }

            m_DimensionsDropdown.SetValueWithoutNotify(dimensions);
        }

        void OnPartnerResolutionsChanged(List<string> resolutions)
        {
            var currentDimensions = m_DimensionsDropdown.value;
            m_DimensionsDropdown.choices = resolutions ?? new List<string>();

            var supportsCustom = this.GetState().SelectModelSettingsSupportsCustomResolutions(this);
            if (!supportsCustom && !m_DimensionsDropdown.choices.Contains(currentDimensions))
            {
                var firstChoice = m_DimensionsDropdown.choices.FirstOrDefault();
                if (firstChoice != null)
                {
                    this.Dispatch(GenerationSettingsActions.setImageDimensions, firstChoice);
                    return;
                }
            }

            if (!string.IsNullOrEmpty(currentDimensions))
            {
                OnImageDimensionsChanged(currentDimensions);
            }
        }
    }
}
