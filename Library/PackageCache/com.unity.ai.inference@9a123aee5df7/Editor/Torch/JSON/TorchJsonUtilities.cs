using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace TorchPt2
{
    abstract class Union
    {
        public string kind { get; set; }
        public object value { get; set; }

        public abstract Type FromKind(string kind);

        public void Deserialize(JsonSerializer serializer, JProperty prop)
        {
            kind = prop.Name;
            value = prop.Value.ToObject(FromKind(prop.Name), serializer);
        }
    }

    abstract class UnionConverter<T> : JsonConverter where T : Union, new()
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(T);

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
                return null;
            var obj = JObject.Load(reader);
            var prop = obj.Properties().First();
            var union = new T();
            union.Deserialize(serializer, prop);
            return union;
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var union = (T)value;

            if (union.kind == null || union.value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            writer.WritePropertyName(union.kind);
            serializer.Serialize(writer, union.value);
            writer.WriteEndObject();
        }
    }
}
