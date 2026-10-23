using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    /// <summary>
    /// Strongly typed representation of the JSON Schema used in ModelFullResult.ParamsSchema.
    /// The raw JSON is stored in a Unity-serializable field so that the parsed dictionaries
    /// survive domain reloads (Unity cannot serialize Dictionary or auto-properties).
    /// </summary>
    [Serializable]
    class CrossFieldConstraint
    {
        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("fields")]
        public List<string> Fields { get; set; }

        [JsonProperty("op")]
        public string Op { get; set; }

        [JsonProperty("minimum")]
        public double? Minimum { get; set; }

        [JsonProperty("maximum")]
        public double? Maximum { get; set; }

        [JsonProperty("max_ratio")]
        public double? MaxRatio { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }
    }

    [Serializable]
    class ModelParamsSchema
    {
        public const string DimensionsProperty = "dimensions";
        public const string WidthProperty = "width";
        public const string HeightProperty = "height";
        public const string AspectRatioProperty = "aspect_ratio";

        [SerializeField]
        string m_RawJson;

        [NonSerialized]
        Dictionary<string, SchemaProperty> m_Properties;

        [NonSerialized]
        List<string> m_Required;

        [NonSerialized]
        List<List<string>> m_AnyOf;

        [NonSerialized]
        List<CrossFieldConstraint> m_CrossFieldConstraints;

        [NonSerialized]
        bool m_AdditionalProperties;

        [NonSerialized]
        bool m_Parsed;

        [JsonProperty("properties")]
        public Dictionary<string, SchemaProperty> Properties
        {
            get
            {
                EnsureParsed();
                return m_Properties;
            }
            set => m_Properties = value;
        }

        [JsonProperty("required")]
        public List<string> Required
        {
            get
            {
                EnsureParsed();
                return m_Required;
            }
            set => m_Required = value;
        }

        /// <summary>
        /// Structured representation of JSON Schema anyOf branches.
        /// Each inner list contains the required keys for one branch.
        /// At least one branch must be fully satisfied.
        /// </summary>
        [JsonProperty("anyOf")]
        public List<List<string>> AnyOf
        {
            get
            {
                EnsureParsed();
                return m_AnyOf;
            }
            set => m_AnyOf = value;
        }

        [JsonProperty("additionalProperties")]
        public bool AdditionalProperties
        {
            get
            {
                EnsureParsed();
                return m_AdditionalProperties;
            }
            set => m_AdditionalProperties = value;
        }

        [JsonProperty("x-cross-field-constraints")]
        public List<CrossFieldConstraint> CrossFieldConstraints
        {
            get
            {
                EnsureParsed();
                return m_CrossFieldConstraints;
            }
            set => m_CrossFieldConstraints = value;
        }

        public string RawJson
        {
            get => m_RawJson;
            set => m_RawJson = value;
        }

        void EnsureParsed()
        {
            if (m_Parsed || m_Properties != null || string.IsNullOrEmpty(m_RawJson))
                return;

            try
            {
                var rawSchema = Newtonsoft.Json.Linq.JObject.Parse(m_RawJson);

                if (rawSchema["required"] != null)
                    m_Required = rawSchema["required"].ToObject<List<string>>();

                var anyOfToken = rawSchema["anyOf"];
                if (anyOfToken is Newtonsoft.Json.Linq.JArray anyOfArray)
                {
                    m_AnyOf = new List<List<string>>();
                    foreach (var item in anyOfArray)
                    {
                        if (item is Newtonsoft.Json.Linq.JObject obj &&
                            obj["required"] is Newtonsoft.Json.Linq.JArray reqArray)
                        {
                            var branch = reqArray.Select(k => k?.ToString())
                                .Where(k => !string.IsNullOrEmpty(k)).ToList();
                            if (branch.Count > 0)
                                m_AnyOf.Add(branch);
                        }
                    }
                }

                if (m_AnyOf == null || m_AnyOf.Count == 0)
                {
                    var oneOfToken = rawSchema["oneOf"];
                    if (oneOfToken is Newtonsoft.Json.Linq.JArray oneOfArray)
                    {
                        m_AnyOf = new List<List<string>>();
                        foreach (var item in oneOfArray)
                        {
                            if (item is Newtonsoft.Json.Linq.JObject obj &&
                                obj["required"] is Newtonsoft.Json.Linq.JArray reqArray)
                            {
                                var branch = reqArray.Select(k => k?.ToString())
                                    .Where(k => !string.IsNullOrEmpty(k)).ToList();
                                if (branch.Count > 0)
                                    m_AnyOf.Add(branch);
                            }
                        }
                    }
                }

                if (rawSchema["additionalProperties"] != null)
                    m_AdditionalProperties = rawSchema["additionalProperties"].ToObject<bool>();

                var constraintsToken = rawSchema["x-cross-field-constraints"];
                if (constraintsToken is Newtonsoft.Json.Linq.JArray constraintsArray)
                    m_CrossFieldConstraints = constraintsArray.ToObject<List<CrossFieldConstraint>>();

                var propertiesToken = rawSchema["properties"];
                if (propertiesToken is Newtonsoft.Json.Linq.JObject propertiesObj)
                {
                    m_Properties = new Dictionary<string, SchemaProperty>();
                    foreach (var prop in propertiesObj.Properties())
                    {
                        var schemaProp = prop.Value.ToObject<SchemaProperty>();
                        m_Properties[prop.Name] = schemaProp;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to re-parse ModelParamsSchema from raw JSON: {e.Message}");
            }
            finally
            {
                m_Parsed = true;
            }
        }

        /// <summary>
        /// Returns true if the key is unconditionally required (in top-level Required),
        /// or if every anyOf branch requires it.
        /// </summary>
        public bool IsRequired(string key)
        {
            if (Required?.Contains(key) == true)
                return true;
            if (AnyOf is { Count: > 0 } && AnyOf.All(branch => branch.Contains(key)))
                return true;
            return false;
        }

        /// <summary>
        /// Returns true if at least one anyOf branch is fully satisfied by the provided keys.
        /// Returns true when there are no anyOf branches.
        /// </summary>
        public bool IsAnyOfSatisfied(ISet<string> providedKeys)
        {
            if (AnyOf == null || AnyOf.Count == 0)
                return true;
            return AnyOf.Any(branch => branch.All(key => providedKeys.Contains(key)));
        }

        /// <summary>
        /// Evaluates the x-cross-field-constraints for the given field values and returns the
        /// message of the first violated constraint, or null if all constraints are satisfied.
        /// Only fields present in <paramref name="fieldValues"/> are considered; a constraint
        /// referencing an unknown field is skipped (treated as satisfied).
        /// Supported constraint types: "total_bounds" (op "multiply"), "ratio_bound" (max_ratio).
        /// </summary>
        public string GetCrossFieldViolation(IReadOnlyDictionary<string, double> fieldValues)
        {
            if (CrossFieldConstraints == null || CrossFieldConstraints.Count == 0 || fieldValues == null)
                return null;

            foreach (var constraint in CrossFieldConstraints)
            {
                if (IsConstraintViolated(constraint, fieldValues))
                    return constraint.Message;
            }

            return null;
        }

        static bool IsConstraintViolated(CrossFieldConstraint constraint, IReadOnlyDictionary<string, double> fieldValues)
        {
            if (constraint.Fields == null || constraint.Fields.Count == 0)
                return false;

            var values = new List<double>();
            foreach (var field in constraint.Fields)
            {
                // A constraint referencing a field we don't have a value for is skipped (treated as satisfied).
                if (!fieldValues.TryGetValue(field, out var val))
                    return false;
                values.Add(val);
            }

            switch (constraint.Type)
            {
                case "total_bounds" when string.Equals(constraint.Op, "multiply", StringComparison.OrdinalIgnoreCase):
                {
                    var product = 1.0;
                    foreach (var v in values) product *= v;
                    return (constraint.Minimum.HasValue && product < constraint.Minimum.Value) ||
                        (constraint.Maximum.HasValue && product > constraint.Maximum.Value);
                }
                case "ratio_bound" when values.Count == 2 && constraint.MaxRatio.HasValue:
                {
                    var minVal = Math.Min(values[0], values[1]);
                    if (minVal <= 0)
                        return false;
                    var ratio = Math.Max(values[0], values[1]) / minVal;
                    return ratio > constraint.MaxRatio.Value;
                }
                default:
                    return false;
            }
        }

        /// <summary>
        /// Convenience overload for the common width/height case. Returns true if the
        /// dimensions satisfy all cross-field constraints (or there are none).
        /// </summary>
        public bool IsWidthHeightValid(int width, int height)
        {
            var fieldValues = new Dictionary<string, double>
            {
                [WidthProperty] = width,
                [HeightProperty] = height
            };
            return GetCrossFieldViolation(fieldValues) == null;
        }
    }

    [Serializable]
    class SchemaProperty
    {
        [JsonProperty("type")]
        public object Type { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("enum")]
        public List<object> Enum { get; set; }

        [JsonProperty("default")]
        public object Default { get; set; }

        [JsonProperty("minLength")]
        public int? MinLength { get; set; }

        [JsonProperty("maxLength")]
        public int? MaxLength { get; set; }

        [JsonProperty("minimum")]
        public double? Minimum { get; set; }

        [JsonProperty("maximum")]
        public double? Maximum { get; set; }

        [JsonProperty("maxItems")]
        public int? MaxItems { get; set; }

        [JsonProperty("minItems")]
        public int? MinItems { get; set; }

        [JsonProperty("items")]
        public SchemaProperty Items { get; set; }

        [JsonProperty("x-semantic-type")]
        public string SemanticType { get; set; }

        /// <summary>
        /// Physical unit of a numeric parameter (JSON Schema vendor extension "x-unit").
        /// Allowed values mirror the backend contract: seconds, frames, steps, pixels,
        /// megabytes, fps, meters, bpm. Null when the field has no physical dimension.
        /// </summary>
        [JsonProperty("x-unit")]
        public string Unit { get; set; }

        /// <summary>
        /// Reference frame rate used to convert between frames and seconds. Present (and
        /// required by the backend) only when <see cref="Unit"/> is "frames"; null otherwise.
        /// </summary>
        [JsonProperty("x-reference-frame-rate")]
        public double? ReferenceFrameRate { get; set; }

        [JsonProperty("properties")]
        public Dictionary<string, SchemaProperty> Properties { get; set; }

        [JsonProperty("required")]
        public List<string> Required { get; set; }
    }
}
