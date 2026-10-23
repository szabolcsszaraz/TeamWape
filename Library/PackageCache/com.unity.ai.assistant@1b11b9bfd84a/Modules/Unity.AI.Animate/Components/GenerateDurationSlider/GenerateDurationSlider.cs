using System;
using Unity.AI.Animate.Services.Stores.Actions;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Animate.Components
{
    [UxmlElement]
    partial class GenerateDurationSlider : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Animate/Components/GenerateDurationSlider/GenerateDurationSlider.uxml";

        // Fallback range (seconds) used when the selected model's length param declares no bounds,
        // e.g. legacy model lists that predate x-unit. Matches the historical UXML defaults.
        const float k_FallbackMinSeconds = 1f;
        const float k_FallbackMaxSeconds = 10f;

        readonly Slider m_Slider;

        public GenerateDurationSlider()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Slider = this.Q<Slider>();
            m_Slider.RegisterValueChangedCallback(evt => this.Dispatch(GenerationSettingsActions.setDuration, evt.newValue));
            this.Use(state => state.SelectDuration(this), duration => m_Slider.SetValueWithoutNotify(Mathf.Round(duration * 100f) / 100f));
            this.Use(state => state.SelectSelectedModel(this), OnModelChanged);
        }

        // The slider is always presented in seconds. For models whose length is expressed in
        // seconds we honor the model's real bounds so the user can only pick a duration the model
        // accepts (e.g. Uthana's 4-10s). For frame-based (or legacy, unit-less) models the raw
        // frame bounds aren't meaningful UX limits, so we keep the familiar default window.
        void OnModelChanged(ModelSettings model)
        {
            var supportsLength = model?.SupportsParam(ModelConstants.SchemaKeys.Length) ?? false;
            this.SetShown(supportsLength);
            if (!supportsLength)
                return;

            model.paramsSchema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Length, out var lengthProp);

            var (min, max) = ModelUnitConversion.IsSecondsUnit(lengthProp)
                ? ModelUnitConversion.WireRangeToSeconds(lengthProp, k_FallbackMinSeconds, k_FallbackMaxSeconds)
                : (k_FallbackMinSeconds, k_FallbackMaxSeconds);

            m_Slider.lowValue = min;
            m_Slider.highValue = max;

            // Keep the stored duration inside the new range. Dispatch the clamped value (not just
            // repaint) so what Generate/Quote read from state matches what the slider shows —
            // SetValueWithoutNotify alone would leave the store holding a stale, out-of-range value.
            var current = m_Slider.value;
            var clamped = Mathf.Clamp(current, min, max);
            m_Slider.SetValueWithoutNotify(clamped);
            if (!Mathf.Approximately(clamped, current))
                this.Dispatch(GenerationSettingsActions.setDuration, clamped);
        }
    }
}
