using Unity.AI.Generators.UI.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class GenerateDurationSlider : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Sound/Components/GenerateDurationSlider/GenerateDurationSlider.uxml";

        readonly Slider m_Slider;
        float[] m_AllowedValues;
        bool m_IntegerOnly;

        public GenerateDurationSlider()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Slider = this.Q<Slider>();
            m_Slider.RegisterValueChangedCallback(evt =>
            {
                var value = m_AllowedValues != null ? SchemaPropertiesExtensions.SnapToNearest(evt.newValue, m_AllowedValues) : evt.newValue;
                if (m_IntegerOnly)
                    value = Mathf.Round(value);
                if (m_AllowedValues != null || m_IntegerOnly)
                    m_Slider.SetValueWithoutNotify(value);
                this.Dispatch(GenerationSettingsActions.setDuration, value);
            });
            this.Use(state => state.SelectDuration(this), duration => m_Slider.SetValueWithoutNotify(Mathf.Round(duration * 100f) / 100f));
            this.Use(state => state.SelectSoundReferenceIsValid(this), valid => m_Slider.SetEnabled(!valid));
            this.Use(state => state.SelectSelectedModel(this), model =>
            {
                var supportsDuration = model?.SupportsParam(ModelConstants.SchemaKeys.Duration) ?? false;
                this.SetShown(supportsDuration);

                if (supportsDuration && model?.paramsSchema?.Properties != null &&
                    model.paramsSchema.Properties.TryGetValue(ModelConstants.SchemaKeys.Duration, out var durationProp))
                {
                    m_AllowedValues = durationProp.GetAllowedFloatValues();
                    m_IntegerOnly = durationProp.IsIntegerType();
                    if (m_AllowedValues != null)
                    {
                        m_Slider.lowValue = m_AllowedValues[0];
                        m_Slider.highValue = m_AllowedValues[m_AllowedValues.Length - 1];
                    }
                    else
                    {
                        m_Slider.lowValue = (float)(durationProp.Minimum ?? 1);
                        m_Slider.highValue = (float)(durationProp.Maximum ?? 30);
                    }
                }
                else
                {
                    m_AllowedValues = null;
                    m_IntegerOnly = false;
                }
            });
        }

    }
}
