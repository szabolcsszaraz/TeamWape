using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Sound.Services.Stores.Actions;
using Unity.AI.Sound.Services.Stores.Selectors;
using Unity.AI.Sound.Services.Utilities;
using Unity.AI.Toolkit.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Sound.Components
{
    [UxmlElement]
    partial class DynamicModelParams : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Sound/Components/DynamicModelParams/DynamicModelParams.uxml";
        const string k_VoiceSamplesPath = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Sound/Sounds/VoiceSamples";
        const string k_VoiceKey = "voice";

        static readonly HashSet<string> k_HandledKeys = new()
        {
            ModelConstants.SchemaKeys.Prompt,
            ModelConstants.SchemaKeys.NegativePrompt,
            ModelConstants.SchemaKeys.NegativePromptCamel,
            ModelConstants.SchemaKeys.Duration,
            ModelConstants.SchemaKeys.Seed,
            ModelConstants.SchemaKeys.Loop,
            ModelConstants.SchemaKeys.OutputFormat,
            ModelConstants.SchemaKeys.OutputFormatSnake,
            ModelConstants.SchemaKeys.InputAudio,
            ModelConstants.SchemaKeys.InputAudioSnake,
            "input_audio_strength",
            ModelConstants.SchemaKeys.LanguageCode,
            ModelConstants.SchemaKeys.LanguageCodeSnake,
        };

        readonly VisualElement m_Container;

        CancellationTokenSource m_PreviewCts;
        string m_CurrentModelId;

        public DynamicModelParams()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Container = this.Q<VisualElement>("dynamic-model-params-container");

            this.Use(state => state.SelectSelectedModel(this), OnModelChanged);
            this.Use(state => state.SelectDynamicParams(this), OnDynamicParamsChanged);
        }

        void OnModelChanged(ModelSettings model)
        {
            if (model?.paramsSchema?.Properties == null)
            {
                StopPreview();
                m_Container.Clear();
                m_CurrentModelId = null;
                this.SetShown(false);
                return;
            }

            // Skip rebuild if the model hasn't changed
            if (m_CurrentModelId == model.id && m_Container.childCount > 0)
                return;

            StopPreview();
            m_Container.Clear();

            var dynamicProperties = model.paramsSchema.Properties
                .Where(kvp => !k_HandledKeys.Contains(kvp.Key))
                .Where(kvp => !kvp.Key.StartsWith("$ref:"))
                .Where(kvp => !kvp.Key.StartsWith("ui:"))
                .Where(kvp => kvp.Value.SemanticType != ModelConstants.SemanticTypes.AssetId)
                .Where(kvp => kvp.Value.SemanticType != ModelConstants.SemanticTypes.AssetIdList)
                .Where(kvp => kvp.Value.Enum is { Count: > 0 }) // Only show enum dropdowns
                .ToList();

            if (dynamicProperties.Count == 0)
            {
                m_CurrentModelId = model.id;
                this.SetShown(false);
                return;
            }

            // Determine field ordering from uiSchema if available
            var orderedKeys = GetOrderedKeys(model.uiSchema, dynamicProperties.Select(p => p.Key).ToList());

            // Set the model ID before dispatching defaults to prevent re-entrant rebuilds
            var modelChanged = m_CurrentModelId != model.id;
            m_CurrentModelId = model.id;

            foreach (var key in orderedKeys)
            {
                var prop = model.paramsSchema.Properties[key];
                var field = CreateFieldForProperty(key, prop);
                if (field != null)
                    m_Container.Add(field);
            }

            this.SetShown(m_Container.childCount > 0);

            // Populate default values into the store after UI is built so the
            // re-entrant OnModelChanged call hits the early-exit guard above.
            // Preserve any existing user selections; only fill in missing keys.
            if (modelChanged)
            {
                var existing = this.GetState().SelectDynamicParams(this);
                var merged = new SerializableDictionary<string, string>();

                // Keep existing user selections
                if (existing != null)
                {
                    foreach (var kvp in existing)
                        merged[kvp.Key] = kvp.Value;
                }

                // Only add defaults for keys not already stored
                foreach (var key in orderedKeys)
                {
                    if (!merged.ContainsKey(key))
                    {
                        var prop = model.paramsSchema.Properties[key];
                        var defaultValue = GetDefaultValue(prop);
                        if (defaultValue != null)
                            merged[key] = defaultValue;
                    }
                }

                this.Dispatch(GenerationSettingsActions.setDynamicParams, merged);
            }
        }

        void OnDynamicParamsChanged(SerializableDictionary<string, string> dynamicParams)
        {
            if (dynamicParams == null)
                return;

            foreach (var child in m_Container.Children())
            {
                var key = child.userData as string;
                if (key == null || !dynamicParams.TryGetValue(key, out var value))
                    continue;

                // The field may be the direct child or nested inside a wrapper row
                var field = child is DropdownField || child is Slider || child is Toggle || child is TextField
                    ? child
                    : child.Q<DropdownField>() as VisualElement
                      ?? child.Q<Slider>() as VisualElement
                      ?? child.Q<Toggle>() as VisualElement
                      ?? child.Q<TextField>();

                switch (field)
                {
                    case DropdownField dropdown:
                        dropdown.SetValueWithoutNotify(value);
                        break;
                    case Slider slider:
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                            slider.SetValueWithoutNotify(floatVal);
                        break;
                    case Toggle toggle:
                        if (bool.TryParse(value, out var boolVal))
                            toggle.SetValueWithoutNotify(boolVal);
                        break;
                    case TextField textField:
                        textField.SetValueWithoutNotify(value);
                        break;
                }
            }
        }

        VisualElement CreateFieldForProperty(string key, SchemaProperty prop)
        {
            var label = FormatLabel(key);

            // Enum property -> DropdownField
            if (prop.Enum is { Count: > 0 })
            {
                var choices = prop.Enum.Select(e => e?.ToString() ?? "").ToList();
                if (key == k_VoiceKey) choices.Add("NoSampleVoice"); // TODO: remove – temporary test entry
                var defaultIndex = 0;
                if (prop.Default != null)
                {
                    var idx = choices.IndexOf(prop.Default.ToString());
                    if (idx >= 0)
                        defaultIndex = idx;
                }

                var dropdown = new DropdownField(label, choices, defaultIndex)
                {
                    userData = key,
                    tooltip = prop.Description ?? ""
                };

                dropdown.RegisterValueChangedCallback(evt =>
                {
                    StopPreview();
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue));
                });

                // Wrap voice dropdown with a preview button
                if (key == k_VoiceKey)
                    return CreateVoicePreviewRow(dropdown, key);

                return dropdown;
            }

            var typeStr = prop.Type?.ToString()?.ToLowerInvariant() ?? "";

            // Number/integer with min/max -> Slider
            if ((typeStr.Contains("number") || typeStr.Contains("integer")) && prop.Minimum.HasValue && prop.Maximum.HasValue)
            {
                var slider = new Slider(label, (float)prop.Minimum.Value, (float)prop.Maximum.Value)
                {
                    userData = key,
                    showInputField = true,
                    tooltip = prop.Description ?? ""
                };

                if (prop.Default != null && float.TryParse(prop.Default.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var defaultFloat))
                    slider.SetValueWithoutNotify(defaultFloat);

                slider.RegisterValueChangedCallback(evt =>
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue.ToString(CultureInfo.InvariantCulture))));

                return slider;
            }

            // Boolean -> Toggle
            if (typeStr.Contains("boolean"))
            {
                var toggle = new Toggle(label)
                {
                    userData = key,
                    tooltip = prop.Description ?? ""
                };

                if (prop.Default != null && bool.TryParse(prop.Default.ToString(), out var defaultBool))
                    toggle.SetValueWithoutNotify(defaultBool);

                toggle.RegisterValueChangedCallback(evt =>
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue.ToString())));

                return toggle;
            }

            // String -> TextField (skip if it looks like an asset reference)
            if (typeStr.Contains("string") && prop.SemanticType != ModelConstants.SemanticTypes.AssetId && prop.SemanticType != ModelConstants.SemanticTypes.AssetIdList)
            {
                var textField = new TextField(label)
                {
                    userData = key,
                    tooltip = prop.Description ?? "",
                    maxLength = prop.MaxLength ?? 500
                };

                if (prop.Default != null)
                    textField.SetValueWithoutNotify(prop.Default.ToString());

                textField.RegisterValueChangedCallback(evt =>
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue)));

                return textField;
            }

            return null;
        }

        VisualElement CreateVoicePreviewRow(DropdownField dropdown, string key)
        {
            var row = new VisualElement { userData = key };
            row.AddToClassList("voice-preview-row");

            var previewButton = new Button { tooltip = "Preview voice sample" };
            previewButton.AddToClassList("voice-preview-button");

            var playIcon = AssetDatabase.LoadAssetAtPath<Texture2D>("Packages/com.unity.ai.assistant/Modules/Unity.AI.Sound/Icons/Play.png");
            if (playIcon != null)
            {
                var iconImage = new Image { image = playIcon };
                iconImage.AddToClassList("voice-preview-icon");
                previewButton.Add(iconImage);
            }

            // Set initial enabled state based on whether a sample exists
            previewButton.SetEnabled(HasVoiceSample(dropdown.value));

            // Update enabled state when the voice selection changes
            dropdown.RegisterValueChangedCallback(evt => previewButton.SetEnabled(HasVoiceSample(evt.newValue)));

            previewButton.clicked += () =>
            {
                if (previewButton.IsSelected())
                {
                    StopPreview();
                    return;
                }

                var voiceName = dropdown.value;
                if (!string.IsNullOrEmpty(voiceName))
                    _ = PlayVoicePreview(voiceName, previewButton);
            };

            row.Add(dropdown);
            row.Add(previewButton);

            return row;
        }

        async Task PlayVoicePreview(string voiceName, Button previewButton)
        {
            StopPreview();

            var samplePath = $"{k_VoiceSamplesPath}/{voiceName}.wav";
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(samplePath);
            if (clip == null)
                return;

            m_PreviewCts = new CancellationTokenSource();
            var token = m_PreviewCts.Token;

            previewButton.SetSelected();
            try
            {
                await clip.Play(token);
            }
            finally
            {
                previewButton.SetSelected(false);
                if (m_PreviewCts?.Token == token)
                {
                    m_PreviewCts.Dispose();
                    m_PreviewCts = null;
                }
            }
        }

        void StopPreview()
        {
            m_PreviewCts?.Cancel();
            m_PreviewCts?.Dispose();
            m_PreviewCts = null;
        }

        static string GetDefaultValue(SchemaProperty prop)
        {
            if (prop.Default != null)
                return prop.Default.ToString();

            if (prop.Enum is { Count: > 0 })
                return prop.Enum[0]?.ToString();

            return null;
        }

        static List<string> GetOrderedKeys(ModelUiSchema uiSchema, List<string> availableKeys)
        {
            if (uiSchema?.Order == null || uiSchema.Order.Count == 0)
                return availableKeys;

            var ordered = new List<string>();
            foreach (var key in uiSchema.Order)
            {
                if (availableKeys.Contains(key))
                    ordered.Add(key);
            }

            // Append any remaining keys not in the order list
            foreach (var key in availableKeys)
            {
                if (!ordered.Contains(key))
                    ordered.Add(key);
            }

            return ordered;
        }

        static bool HasVoiceSample(string voiceName)
        {
            if (string.IsNullOrEmpty(voiceName))
                return false;

            var samplePath = $"{k_VoiceSamplesPath}/{voiceName}.wav";
            return AssetDatabase.LoadAssetAtPath<AudioClip>(samplePath) != null;
        }

        static string FormatLabel(string key)
        {
            // Convert snake_case or camelCase to Title Case
            var spaced = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            spaced = spaced.Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }
    }
}
