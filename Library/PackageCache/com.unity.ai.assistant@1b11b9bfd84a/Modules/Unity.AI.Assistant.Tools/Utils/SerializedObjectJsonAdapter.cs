using System;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    class SerializedObjectJsonAdapter : JsonConverter<SerializedObject>
    {
        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, SerializedObject value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            writer.WriteStartObject();
            try
            {
                var property = value.GetIterator();
                var enterChildren = true;

                while (property.Next(enterChildren))
                {
                    serializer.Serialize(writer, property);
                    enterChildren = false;
                }
            }
            finally
            {
                writer.WriteEndObject();
            }
        }

        public override SerializedObject ReadJson(JsonReader reader, Type objectType, SerializedObject existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}
