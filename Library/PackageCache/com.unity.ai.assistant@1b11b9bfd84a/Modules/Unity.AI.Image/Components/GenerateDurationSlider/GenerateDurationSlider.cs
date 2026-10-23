using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class GenerateDurationSlider : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Image/Components/GenerateDurationSlider/GenerateDurationSlider.uxml";

        readonly Slider m_Slider;
        float[] m_AllowedValues;

        public GenerateDurationSlider()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Slider = this.Q<Slider>();
            m_Slider.RegisterValueChangedCallback(evt =>
            {
                var value = m_AllowedValues != null ? SchemaPropertiesExtensions.SnapToNearest(evt.newValue, m_AllowedValues) : evt.newValue;
                if (m_AllowedValues != null)
                    m_Slider.SetValueWithoutNotify(value);
                this.Dispatch(GenerationSettingsActions.setDuration, value);
            });
            this.Use(state => state.SelectDurationUnrounded(this), duration => m_Slider.SetValueWithoutNotify(Mathf.Round(duration * 100f) / 100f));
            this.Use(state => state.SelectRefinementMode(this), mode => this.SetShown(mode == RefinementMode.Spritesheet));
            this.Use(state => state.SelectSelectedModel(this), model =>
            {
                var supportsDuration = model?.SupportsParam(ModelConstants.SchemaKeys.Duration) ?? false;
                if (supportsDuration && model?.paramsSchema?.Properties != null &&
                    model.paramsSchema.Properties.TryGetValue(ModelConstants.SchemaKeys.Duration, out var durationProp))
                {
                    m_AllowedValues = durationProp.GetAllowedFloatValues();
                    if (m_AllowedValues != null)
                    {
                        m_Slider.lowValue = m_AllowedValues[0];
                        m_Slider.highValue = m_AllowedValues[m_AllowedValues.Length - 1];
                    }
                    else
                    {
                        m_Slider.lowValue = (float)(durationProp.Minimum ?? 2);
                        m_Slider.highValue = (float)(durationProp.Maximum ?? 12);
                    }
                }
                else
                {
                    m_AllowedValues = null;
                    m_Slider.lowValue = 2;
                    m_Slider.highValue = 12;
                }
            });
        }

    }
}
