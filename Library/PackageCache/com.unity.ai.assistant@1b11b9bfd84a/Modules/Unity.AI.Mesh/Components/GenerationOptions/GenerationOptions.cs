using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Components
{
    [UxmlElement]
    partial class GenerationOptions : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Components/GenerationOptions/GenerationOptions.uxml";
        const int k_DefaultFaceLimitMax = 2_000_000;

        readonly Toggle m_RemoveBackground;
        readonly Toggle m_UseCustomSeed;
        readonly IntegerField m_CustomSeed;
        readonly Toggle m_UseFaceLimit;
        readonly SliderInt m_FaceLimit;
        readonly DropdownField m_TargetFormat;

        public GenerationOptions()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            AddToClassList("generation-options");

            m_UseCustomSeed = this.Q<Toggle>(className:"generation-options-use-custom-seed");
            m_CustomSeed = this.Q<IntegerField>(className:"generation-options-custom-seed");
            m_UseFaceLimit = this.Q<Toggle>(className:"generation-options-use-face-limit");
            m_FaceLimit = this.Q<SliderInt>(className:"generation-options-face-limit");
            m_TargetFormat = this.Q<DropdownField>(className:"generation-options-target-format");

            m_UseCustomSeed.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setUseCustomSeed, evt.newValue));
            m_CustomSeed.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setCustomSeed, evt.newValue));
            m_UseFaceLimit.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setUseFaceLimit, evt.newValue));
            m_FaceLimit.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setFaceLimit, evt.newValue));
            m_TargetFormat.RegisterValueChangedCallback(evt =>
                this.Dispatch(GenerationSettingsActions.setTargetFormat, evt.newValue));

            this.Use(state => state.SelectGenerationOptions(this), OnGenerationOptionsChanged);
            this.Use(state => (state.SelectFaceLimitOptions(this), state.SelectSelectedModel(this)), OnFaceLimitChanged);
            this.Use(state => (state.SelectTargetFormat(this), state.SelectSelectedModel(this)), OnTargetFormatChanged);
        }

        void OnGenerationOptionsChanged((bool useCustomSeed, int customSeed) arg)
        {
            var (useCustomSeed, customSeed) = arg;
            m_UseCustomSeed.value = useCustomSeed;
            m_CustomSeed.value = customSeed;
            m_CustomSeed.EnableInClassList("flex", useCustomSeed);
            m_CustomSeed.EnableInClassList("hide", !useCustomSeed);
        }

        void OnFaceLimitChanged(((bool useFaceLimit, int faceLimit) options, ModelSettings model) arg)
        {
            var (options, model) = arg;
            var properties = model?.paramsSchema?.Properties;
            var faceLimitKey = properties?.FindFirstSupportedParam(
                ModelConstants.SchemaKeys.FaceLimit, ModelConstants.SchemaKeys.FaceCount);
            var supportsFaceLimit = faceLimitKey != null;

            m_UseFaceLimit.EnableInClassList("flex", supportsFaceLimit);
            m_UseFaceLimit.EnableInClassList("hide", !supportsFaceLimit);
            m_UseFaceLimit.value = options.useFaceLimit;

            var showSlider = supportsFaceLimit && options.useFaceLimit;
            m_FaceLimit.EnableInClassList("flex", showSlider);
            m_FaceLimit.EnableInClassList("hide", !showSlider);

            int low = 1;
            int high = k_DefaultFaceLimitMax;
            if (supportsFaceLimit && properties.TryGetValue(faceLimitKey, out var prop))
            {
                low = (int)(prop.Minimum ?? 1);
                high = (int)(prop.Maximum ?? k_DefaultFaceLimitMax);
            }

            m_FaceLimit.highValue = high;
            m_FaceLimit.lowValue = low;
            m_FaceLimit.value = Math.Clamp(options.faceLimit, low, high);
        }

        void OnTargetFormatChanged((string targetFormat, ModelSettings model) arg)
        {
            var (targetFormat, model) = arg;
            var properties = model?.paramsSchema?.Properties;
            var key = properties?.FindFirstSupportedParam(
                ModelConstants.SchemaKeys.TargetFormat, ModelConstants.SchemaKeys.GeometryFileFormat);

            if (key != null && properties.TryGetValue(key, out var prop) && prop.Enum is { Count: > 0 })
            {
                var supportedFormats = new HashSet<string> { "glb", "fbx" };
                var choices = prop.Enum.Select(e => e?.ToString()).Where(e => !string.IsNullOrEmpty(e) && supportedFormats.Contains(e)).ToList();
                m_TargetFormat.choices = choices;

                if (string.IsNullOrEmpty(targetFormat) || !choices.Contains(targetFormat))
                {
                    var defaultValue = prop.Default?.ToString();
                    m_TargetFormat.value = !string.IsNullOrEmpty(defaultValue) && choices.Contains(defaultValue)
                        ? defaultValue
                        : choices[0];
                }
                else
                {
                    m_TargetFormat.value = targetFormat;
                }

                m_TargetFormat.EnableInClassList("flex", true);
                m_TargetFormat.EnableInClassList("hide", false);
            }
            else
            {
                m_TargetFormat.EnableInClassList("flex", false);
                m_TargetFormat.EnableInClassList("hide", true);
            }
        }
    }
}
