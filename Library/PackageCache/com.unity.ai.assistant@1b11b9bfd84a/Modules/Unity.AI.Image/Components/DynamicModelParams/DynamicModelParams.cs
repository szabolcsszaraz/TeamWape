using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Toolkit.Utility;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class DynamicModelParams : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Image/Components/DynamicModelParams/DynamicModelParams.uxml";

        // Keys already handled by other UI components or backend code paths.
        // Anything in this set is intentionally skipped here to avoid duplicates.
        static readonly HashSet<string> k_HandledKeys = new()
        {
            ModelConstants.SchemaKeys.Prompt,
            ModelConstants.SchemaKeys.NegativePrompt,
            ModelConstants.SchemaKeys.NegativePromptCamel,
            ModelConstants.SchemaKeys.Seed,
            ModelConstants.SchemaKeys.AspectRatio,
            ModelConstants.SchemaKeys.AspectRatioCamel,
            ModelConstants.SchemaKeys.Dimensions,
            ModelConstants.SchemaKeys.Width,
            ModelConstants.SchemaKeys.Height,
            ModelConstants.SchemaKeys.Resolution,
            ModelConstants.SchemaKeys.ReferenceImage,
            ModelConstants.SchemaKeys.ReferenceImages,
            ModelConstants.SchemaKeys.MaskReference,
            "mask",
            ModelConstants.SchemaKeys.NumOutputs,
            ModelConstants.SchemaKeys.NumOutputsSnake,
            ModelConstants.SchemaKeys.Duration,
        };

        readonly VisualElement m_Container;

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
                m_Container.Clear();
                m_CurrentModelId = null;
                this.SetShown(false);
                return;
            }

            if (m_CurrentModelId == model.id && m_Container.childCount > 0)
                return;

            m_Container.Clear();

            var dynamicProperties = model.paramsSchema.Properties
                .Where(kvp => !k_HandledKeys.Contains(kvp.Key))
                .Where(kvp => !kvp.Key.StartsWith("$ref:"))
                .Where(kvp => !kvp.Key.StartsWith("ui:"))
                .Where(kvp => kvp.Value.SemanticType != ModelConstants.SemanticTypes.AssetId)
                .Where(kvp => kvp.Value.SemanticType != ModelConstants.SemanticTypes.AssetIdList)
                .Where(kvp => kvp.Value.Enum is { Count: > 0 }) // Only enum dropdowns are exposed for image models
                .ToList();

            if (dynamicProperties.Count == 0)
            {
                m_CurrentModelId = model.id;
                this.SetShown(false);
                return;
            }

            var orderedKeys = GetOrderedKeys(model.uiSchema, dynamicProperties.Select(p => p.Key).ToList());

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

            if (modelChanged)
            {
                var existing = this.GetState().SelectDynamicParams(this);
                var merged = new SerializableDictionary<string, string>();

                if (existing != null)
                {
                    foreach (var kvp in existing)
                        merged[kvp.Key] = kvp.Value;
                }

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

                switch (child)
                {
                    case DropdownField dropdown:
                        dropdown.SetValueWithoutNotify(value);
                        break;
                    case Slider slider:
                        if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var floatVal))
                            slider.SetValueWithoutNotify(floatVal);
                        break;
                    case SliderInt sliderInt:
                        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intVal))
                            sliderInt.SetValueWithoutNotify(intVal);
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

            if (prop.Enum is { Count: > 0 })
            {
                var choices = prop.Enum.Select(e => e?.ToString() ?? "").ToList();
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
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue)));

                return dropdown;
            }

            var typeStr = prop.Type?.ToString()?.ToLowerInvariant() ?? "";

            if (typeStr.Contains("integer") && prop.Minimum.HasValue && prop.Maximum.HasValue)
            {
                var sliderInt = new SliderInt(label, (int)prop.Minimum.Value, (int)prop.Maximum.Value)
                {
                    userData = key,
                    showInputField = true,
                    tooltip = prop.Description ?? ""
                };

                if (prop.Default != null && int.TryParse(prop.Default.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var defaultInt))
                    sliderInt.SetValueWithoutNotify(defaultInt);

                sliderInt.RegisterValueChangedCallback(evt =>
                    this.Dispatch(GenerationSettingsActions.setDynamicParam, new KeyValuePair<string, string>(key, evt.newValue.ToString(CultureInfo.InvariantCulture))));

                return sliderInt;
            }

            if (typeStr.Contains("number") && prop.Minimum.HasValue && prop.Maximum.HasValue)
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

            foreach (var key in availableKeys)
            {
                if (!ordered.Contains(key))
                    ordered.Add(key);
            }

            return ordered;
        }

        static string FormatLabel(string key)
        {
            var spaced = Regex.Replace(key, "([a-z])([A-Z])", "$1 $2");
            spaced = spaced.Replace('_', ' ');
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(spaced);
        }
    }
}
