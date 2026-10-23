using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.InferenceEngine.Serialization
{
    class TensorInfo
    {
        public string dtype;
        public List<long> shape;
        public List<ulong> data_offsets;
    }

    [JsonConverter(typeof(SafetensorsInfoConverter))]
    class SafetensorsInfo
    {
        public Dictionary<string, TensorInfo> tensors = new();
        public Dictionary<string, string> metadata = new();
    }

    class SafetensorsInfoConverter : JsonConverter<SafetensorsInfo>
    {
        public override SafetensorsInfo ReadJson(JsonReader reader, Type objectType, SafetensorsInfo existingValue, bool hasExistingValue, JsonSerializer serializer)
        {
            var obj = JObject.Load(reader);
            var parsed = new SafetensorsInfo();

            foreach (var property in obj.Properties())
            {
                if (property.Name == "__metadata__")
                {
                    parsed.metadata = property.Value.ToObject<Dictionary<string, string>>(serializer);
                }
                else
                {
                    var tensor = property.Value.ToObject<TensorInfo>(serializer);
                    parsed.tensors[property.Name] = tensor;
                }
            }

            return parsed;
        }

        public override void WriteJson(JsonWriter writer, SafetensorsInfo value, JsonSerializer serializer)
        {
            writer.WriteStartObject();

            if (value.metadata != null && value.metadata.Count > 0)
            {
                writer.WritePropertyName("__metadata__");
                serializer.Serialize(writer, value.metadata);
            }

            foreach (var kv in value.tensors)
            {
                writer.WritePropertyName(kv.Key);
                serializer.Serialize(writer, kv.Value);
            }

            writer.WriteEndObject();
        }
    }
}
