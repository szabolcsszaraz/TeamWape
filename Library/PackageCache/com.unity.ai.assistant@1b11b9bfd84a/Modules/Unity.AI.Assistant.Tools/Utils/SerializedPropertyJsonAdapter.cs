using System;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Tools.Editor
{
    class SerializedPropertyJsonAdapter : JsonConverter<SerializedProperty>
    {
        public class MaxLengthException : Exception
        {
            public int Length;
        }

        public int MaxArrayElements { get; set; } = -1;
        public int MaxDepth { get; set; } = -1;
        public int MaxLength { get; set; } = -1;
        public bool UseDisplayName { get; set; } = false;
        public bool IndicateDepthTruncation { get; set; } = true;
        public Func<int> GetCurrentOutputLength { get; set; }

        public override bool CanRead => false;

        public override void WriteJson(JsonWriter writer, SerializedProperty value, JsonSerializer serializer)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            // If this is the root object, we need an object scope
            if (writer.WriteState == WriteState.Start)
            {
                writer.WriteStartObject();
                try
                {
                    WriteProperty(writer, serializer, value, 0, true);
                }
                finally
                {
                    writer.WriteEndObject();
                }
            }
            else
            {
                WriteProperty(writer, serializer, value, 0, true);
            }
        }

        public override SerializedProperty ReadJson(JsonReader reader, Type objectType, SerializedProperty existingValue,
            bool hasExistingValue, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        void WriteProperty(JsonWriter writer, JsonSerializer serializer, SerializedProperty prop, int depth, bool includeName)
        {
            if (MaxDepth >= 0 && depth > MaxDepth)
                return;

            if (depth > 0 && MaxLength > 0 && GetCurrentOutputLength != null)
            {
                var length = GetCurrentOutputLength();
                if (length > MaxLength)
                    throw new MaxLengthException { Length = length };
            }

            if (includeName)
                writer.WritePropertyName(UseDisplayName ? prop.displayName : prop.name);

            // None: strings are also considered arrays of chars
            if (prop.isArray && prop.propertyType != SerializedPropertyType.String)
            {
                WriteArrayProperty(writer, serializer, prop, depth);
                return;
            }

            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer:
                    writer.WriteValue(prop.intValue);
                    break;

                case SerializedPropertyType.Boolean:
                    writer.WriteValue(prop.boolValue);
                    break;

                case SerializedPropertyType.Float:
                    SafeWrite(writer, prop.floatValue);
                    break;

                case SerializedPropertyType.String:
                    writer.WriteValue(prop.stringValue);
                    break;

                case SerializedPropertyType.Color:
                    writer.WriteValue(prop.colorValue.ToString());
                    break;

                case SerializedPropertyType.ObjectReference:
                    WriteObjectReference(writer, prop.objectReferenceValue);
                    break;

                case SerializedPropertyType.Enum:
                    WriteEnumValue(writer, prop);
                    break;

                case SerializedPropertyType.Vector2:
                    writer.WriteValue(prop.vector2Value.ToString("F2"));
                    break;

                case SerializedPropertyType.Vector3:
                    writer.WriteValue(prop.vector3Value.ToString("F2"));
                    break;

                case SerializedPropertyType.Vector4:
                    writer.WriteValue(prop.vector4Value.ToString("F2"));
                    break;

                case SerializedPropertyType.Rect:
                    writer.WriteValue(prop.rectValue.ToString());
                    break;

                case SerializedPropertyType.Quaternion:
                    writer.WriteValue(prop.quaternionValue.ToString("F2"));
                    break;

                case SerializedPropertyType.LayerMask:
                    WriteLayerMask(writer, prop);
                    break;

                case SerializedPropertyType.ArraySize:
                    writer.WriteValue(prop.intValue);
                    break;

                case SerializedPropertyType.Character:
                    writer.WriteValue(Convert.ToChar(prop.intValue).ToString());
                    break;

                case SerializedPropertyType.AnimationCurve:
                    writer.WriteValue("(Type: AnimationCurve)");
                    break;

                case SerializedPropertyType.Bounds:
                    writer.WriteValue(prop.boundsValue.ToString("F2"));
                    break;

                case SerializedPropertyType.Gradient:
                    writer.WriteValue("(Type: Gradient)");
                    break;

                case SerializedPropertyType.ExposedReference:
                    WriteObjectReference(writer, prop.exposedReferenceValue);
                    break;

                case SerializedPropertyType.FixedBufferSize:
                    writer.WriteValue(prop.intValue);
                    break;

                case SerializedPropertyType.Vector2Int:
                    writer.WriteValue(prop.vector2IntValue.ToString());
                    break;

                case SerializedPropertyType.Vector3Int:
                    writer.WriteValue(prop.vector3IntValue.ToString());
                    break;

                case SerializedPropertyType.RectInt:
                    writer.WriteValue(prop.rectIntValue.ToString());
                    break;

                case SerializedPropertyType.BoundsInt:
                    writer.WriteValue(prop.boundsIntValue.ToString());
                    break;

                case SerializedPropertyType.ManagedReference:
                    WriteManagedReference(writer, prop);
                    break;

                case SerializedPropertyType.Hash128:
                    writer.WriteValue(prop.hash128Value.ToString());
                    break;

                case SerializedPropertyType.RenderingLayerMask:
                    WriteRenderingLayerMask(writer, prop);
                    break;

                case SerializedPropertyType.Generic:
                default:
                    WriteGenericProperty(writer, serializer, prop, depth);
                    break;
            }

            return;
        }

        void WriteManagedReference(JsonWriter writer, SerializedProperty prop)
        {
            writer.WriteValue($"(Type: {prop.managedReferenceFieldTypename}, ID: {prop.managedReferenceId})");
        }

        void WriteGenericProperty(JsonWriter writer, JsonSerializer serializer, SerializedProperty prop, int depth)
        {
            if (prop == null || !prop.hasChildren)
            {
                writer.WriteNull();
                return;
            }

            // If the children properties are being truncated
            if (MaxDepth >= 0 && depth == MaxDepth && prop.hasChildren)
            {
                writer.WriteValue("...");
                return;
            }

            // Specific case for ComponentPair
            var isComponentPair = prop.type == "ComponentPair";

            writer.WriteStartObject();
            try
            {
                var iterator = prop.Copy();
                var endProp = iterator.GetEndProperty();
                var enterChildren = true;
                while (iterator.Next(enterChildren) && !SerializedProperty.EqualContents(iterator, endProp))
                {
                    // Specific case for ComponentPair to avoid recursing on components
                    if (isComponentPair && iterator.name == "data")
                        continue;

                    WriteProperty(writer, serializer, iterator, depth + 1, true);
                    enterChildren = false;
                }
            }
            finally
            {
                writer.WriteEndObject();
            }
        }

        void WriteLayerMask(JsonWriter writer, SerializedProperty prop)
        {
            var mask = prop.intValue;

            var sb = new StringBuilder();
            var first = true;

            // Unity supports up to 32 layers
            for (var i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    if (!first)
                        sb.Append(" | ");
                    else
                        first = false;

                    var layerName = LayerMask.LayerToName(i);
                    if (string.IsNullOrEmpty(layerName))
                        layerName = "Layer" + i;
                    sb.Append($"{layerName} ({i})");
                }
            }

            var maskString = sb.Length > 0 ? sb.ToString() : "None";
            writer.WriteValue(maskString);
        }

        void WriteRenderingLayerMask(JsonWriter writer, SerializedProperty prop)
        {
            var mask = prop.intValue;

            var sb = new StringBuilder();
            var first = true;

            // Unity supports up to 32 layers
            for (var i = 0; i < 32; i++)
            {
                if ((mask & (1 << i)) != 0)
                {
                    if (!first)
                        sb.Append(" | ");
                    else
                        first = false;

                    var layerName = i < RenderingLayerMask.GetRenderingLayerCount() ? RenderingLayerMask.RenderingLayerToName(i) : null;
                    if (string.IsNullOrEmpty(layerName))
                        layerName = "Layer" + i;
                    sb.Append($"{layerName} ({i})");
                }
            }

            var maskString = sb.Length > 0 ? sb.ToString() : "None";
            writer.WriteValue(maskString);
        }

        void WriteEnumValue(JsonWriter writer, SerializedProperty prop)
        {
            // Mixed value
            if (prop.enumValueIndex == -1)
            {
                var value = prop.enumValueFlag;
                if (value == 0)
                {
                    writer.WriteValue("None");
                }
                else
                {
                    var sb = new StringBuilder();
                    var first = true;

                    for (var i = 0; i < prop.enumNames.Length; i++)
                    {
                        if ((value & (1 << i)) != 0)
                        {
                            if (!first)
                                sb.Append(" | ");
                            sb.Append(prop.enumNames[i]);
                            first = false;
                        }
                    }
                    writer.WriteValue(sb.ToString());
                }
            }
            else
            {
                writer.WriteValue(prop.enumNames[prop.enumValueIndex]);
            }
        }

        void WriteObjectReference(JsonWriter writer, Object value)
        {
            if (value == null)
            {
                writer.WriteNull();
                return;
            }

            var name = value.name;
            var typeName = GetTypeName(value.GetType());
#if UNITY_6000_5_OR_NEWER
            var instanceID = (long)EntityId.ToULong(value.GetEntityId());
#else
            var instanceID = value.GetInstanceID();
#endif

            writer.WriteValue($"(Name: {name}, Type: {typeName}, InstanceID: {instanceID})");
        }

        void WriteArrayProperty(JsonWriter writer, JsonSerializer serializer, SerializedProperty value, int depth)
        {
            writer.WriteStartArray();
            try
            {
                var numElements = MaxArrayElements >= 0 ? Mathf.Min(value.arraySize, MaxArrayElements) : value.arraySize;
                for (var i = 0; i < numElements; ++i)
                {
                    var element = value.GetArrayElementAtIndex(i);
                    WriteProperty(writer, serializer, element, depth + 1, false);
                }

                if (MaxArrayElements >= 0 && value.arraySize > MaxArrayElements)
                    writer.WriteValue("... (truncated)");
            }
            finally
            {
                writer.WriteEndArray();
            }
        }

        static void SafeWrite(JsonWriter writer, float value)
        {
            if (float.IsFinite(value))
                writer.WriteValue(value);
            else
                writer.WriteValue(value.ToString(CultureInfo.InvariantCulture));
        }

        static string GetTypeName(Type type)
        {
            return type?.Name ?? "(null)";
        }
    }
}
