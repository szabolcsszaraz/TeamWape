using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    /// <summary>
    /// Strongly typed representation of the UI schema returned alongside a model's params schema.
    /// The raw JSON is stored in a Unity-serializable field so that the parsed collections
    /// survive domain reloads (Unity cannot serialize Dictionary or auto-properties).
    /// </summary>
    [Serializable]
    class ModelUiSchema
    {
        [SerializeField]
        string m_RawJson;

        [NonSerialized]
        List<string> m_Order;

        [NonSerialized]
        List<UiGroup> m_Groups;

        [NonSerialized]
        Dictionary<string, JToken> m_FieldSchemas;

        [NonSerialized]
        bool m_Parsed;

        [JsonProperty(ModelConstants.SchemaKeys.UiOrder)]
        public List<string> Order
        {
            get
            {
                EnsureParsed();
                return m_Order;
            }
            set => m_Order = value;
        }

        [JsonProperty(ModelConstants.SchemaKeys.UiGroups)]
        public List<UiGroup> Groups
        {
            get
            {
                EnsureParsed();
                return m_Groups;
            }
            set => m_Groups = value;
        }

        [JsonExtensionData]
        public Dictionary<string, JToken> FieldSchemas
        {
            get
            {
                EnsureParsed();
                return m_FieldSchemas;
            }
            set => m_FieldSchemas = value;
        }

        public string RawJson
        {
            get => m_RawJson;
            set => m_RawJson = value;
        }

        /// <summary>
        /// Returns the MIME types from the assetPicker.accept array for the given field,
        /// or null if the field has no assetPicker or the schema is missing/malformed.
        /// </summary>
        public List<string> GetAssetPickerAcceptTypes(string fieldName)
        {
            try
            {
                if (FieldSchemas == null)
                    return null;

                if (!FieldSchemas.TryGetValue(fieldName, out var fieldToken))
                    return null;

                if (fieldToken is not JObject fieldObj)
                    return null;

                if (fieldObj[ModelConstants.SchemaKeys.AssetPicker] is not JObject assetPickerObj)
                    return null;

                if (assetPickerObj["accept"] is not JArray acceptArray)
                    return null;

                return acceptArray.ToObject<List<string>>();
            }
            catch
            {
                return null;
            }
        }

        void EnsureParsed()
        {
            if (m_Parsed || m_FieldSchemas != null || string.IsNullOrEmpty(m_RawJson))
                return;

            try
            {
                var rawUiSchema = JObject.Parse(m_RawJson);

                if (rawUiSchema[ModelConstants.SchemaKeys.UiOrder] != null)
                    m_Order = rawUiSchema[ModelConstants.SchemaKeys.UiOrder].ToObject<List<string>>();
                if (rawUiSchema[ModelConstants.SchemaKeys.UiGroups] != null)
                    m_Groups = rawUiSchema[ModelConstants.SchemaKeys.UiGroups].ToObject<List<UiGroup>>();

                m_FieldSchemas = new Dictionary<string, JToken>();
                foreach (var prop in rawUiSchema.Properties())
                {
                    if (prop.Name == ModelConstants.SchemaKeys.UiOrder || prop.Name == ModelConstants.SchemaKeys.UiGroups)
                        continue;
                    m_FieldSchemas[prop.Name] = prop.Value;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to re-parse ModelUiSchema from raw JSON: {e.Message}");
            }
            finally
            {
                m_Parsed = true;
            }
        }
    }

    [Serializable]
    class UiGroup
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("label")]
        public string Label { get; set; }

        [JsonProperty("children")]
        public List<string> Children { get; set; }

        [JsonProperty("collapsible")]
        public bool Collapsible { get; set; }
    }
}
