using System;
using System.Data;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace Unity.InferenceEngine.Tokenization.Parsers
{
    static class NewtonsoftJsonUtility
    {
        public static T GetValue<T>(this JToken @this, JTokenType type, string field)
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var data = @this[field];
            if (data == null)
                throw new DataException($"Missing field {field}");

            if (data.Type != type)
                throw new DataException($"{type} expected, {data.Type} found");

            return data.Value<T>();
        }

        static TJ GetToken<TJ>(this JToken @this, JTokenType type, string field) where TJ : JToken
        {
            if (@this == null)
                throw new ArgumentNullException(nameof(@this));

            var data = @this[field];
            if (data == null)
                throw new DataException($"Missing field {field}");

            if (data.Type != type)
                throw new DataException($"{type} expected, {data.Type} found");

            return data as TJ;
        }


        public static bool GetBooleanOptional(this JToken @this, string field,
            bool @default = false)
        {
            if (@this == null)
                throw new NullReferenceException("Invalid source");

            var data = @this[field];

            if (data == null)
                return @default;

            if (data.Type == JTokenType.Null)
                return @default;

            if (data.Type == JTokenType.Boolean)
                return data.Value<bool>();

            throw new DataException($"Unexpected data type {data.Type} for field {field}");
        }

        public static float GetFloatOptional(this JToken @this, string field, float @default = 0)
        {
            if (@this == null)
                throw new NullReferenceException("Invalid source");

            var data = @this[field];

            if (data == null)
                return @default;

            if (data.Type == JTokenType.Null)
                return @default;

            if (data.Type == JTokenType.Float)
                return data.Value<float>();

            throw new DataException($"Unexpected data type {data.Type} for field {field}");
        }

        public static string GetStringOptional(this JToken @this, string field,
            string @default = null)
        {
            if (@this == null)
                throw new NullReferenceException("Invalid source");

            var data = @this[field];

            if (data == null)
                return @default;

            if (data.Type == JTokenType.Null)
                return @default;

            if (data.Type == JTokenType.String)
                return data.Value<string>();

            throw new DataException($"Unexpected data type {data.Type} for field {field}");
        }

        public static int GetIntegerOptional(this JToken @this, string field, int @default = 0)
        {
            if (@this == null)
                throw new NullReferenceException("Invalid source");

            var data = @this[field];

            if (data == null)
                return @default;

            if (data.Type == JTokenType.Null)
                return @default;

            if (data.Type == JTokenType.Integer)
                return data.Value<int>();

            throw new DataException($"Unexpected data type {data.Type} for field {field}");
        }


        public static JObject GetObject([NotNull] this JToken @this, string field) =>
            GetToken<JObject>(@this, JTokenType.Object, field);
        public static JArray GetArray([NotNull] this JToken @this, string field) =>
            GetToken<JArray>(@this, JTokenType.Array, field);
        public static bool GetBoolean([NotNull] this JToken @this, string field) =>
            GetValue<bool>(@this, JTokenType.Boolean, field);
        public static int GetInteger([NotNull] this JToken @this, string field) =>
            GetValue<int>(@this, JTokenType.Integer, field);
        public static float GetFloat([NotNull] this JToken @this, string field) =>
            GetValue<float>(@this, JTokenType.Float, field);
        public static string GetString([NotNull] this JToken @this, string field) =>
            GetValue<string>(@this, JTokenType.String, field);
    }
}
