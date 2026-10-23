using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Unity.InferenceEngine.EditorTests")]

namespace Unity.InferenceEngine.Editor.Onnx
{
    class ONNXNodeWrapper
    {
        NodeProto m_ONNXNode;
        List<ONNXModelConverter.ImporterWarning> m_ImporterWarnings;

        public string OperatorType => m_ONNXNode.OpType;

        // Outputs
        public int OutputCount => m_ONNXNode.Output.Count;
        public string[] Outputs => m_ONNXNode.Output.ToArray();

        // Inputs
        public int InputCount => m_ONNXNode.Input.Count;
        public string[] Inputs => m_ONNXNode.Input.ToArray();

        // ---------------------------------------------------------------------------------
        // Implementation

        public ONNXNodeWrapper(NodeProto ONNXNode)
        {
            m_ONNXNode = ONNXNode;
            m_ImporterWarnings = new List<ONNXModelConverter.ImporterWarning>();
        }

        // Logging helpers
        public void Warn(string message, ONNXModelConverter.WarningType severity)
        {
            m_ImporterWarnings.Add(new ONNXModelConverter.ImporterWarning(message, severity));
            Debug.LogWarning(message);
        }

        public bool HasAttribute(string name)
        {
            return TryFindAttribute(name, out _);
        }

        public void UnsupportedAttribute(string name)
        {
            if (HasAttribute(name))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored.", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, int defaultValue)
        {
            if (HasAttribute(name))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, float defaultValue)
        {
            if (HasAttribute(name))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, string defaultValue)
        {
            if (HasAttribute(name))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, int[] defaultValue)
        {
            var valueArray = GetOptionalIntArray(name, defaultValue);
            if (!Enumerable.SequenceEqual(valueArray, defaultValue))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, string[] defaultValue)
        {
            var stringArray = GetOptionalStringArray(name, defaultValue);
            if (!Enumerable.SequenceEqual(stringArray, defaultValue))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        public void UnsupportedAttribute(string name, Func<int, bool> predicate, int[] defaultValue)
        {
            var valueArray = GetOptionalIntArray(name, defaultValue);
            if (!Enumerable.All(valueArray, predicate))
                Warn($"<b>{OperatorType}:</b> Unsupported attribute `<b>{name}</b>`. Value will be ignored and defaulted to [{string.Join(", ", defaultValue)}].", ONNXModelConverter.WarningType.Warning);
        }

        // Attribute helpers
        internal bool TryFindAttribute(string name, out AttributeProto attr)
        {
            return TryFindAttribute(name, AttributeProto.Types.AttributeType.Undefined, out attr);
        }

        internal bool TryFindAttribute(string name, AttributeProto.Types.AttributeType type, out AttributeProto attr)
        {
            const AttributeProto.Types.AttributeType undefined = AttributeProto.Types.AttributeType.Undefined;
            var attributes = m_ONNXNode.Attribute;
            for (var i = 0; i < attributes.Count; ++i)
            {
                attr = attributes[i];
                if (attr.Name == name && (attr.Type == type || attr.Type == undefined || type == undefined))
                    return true;
            }

            attr = null;
            return false;
        }

        internal AttributeProto FindAttribute(string name, AttributeProto.Types.AttributeType type = AttributeProto.Types.AttributeType.Undefined)
        {
            if (TryFindAttribute(name, type, out var attr))
                return attr;

            throw new OnnxLayerImportException($"Couldn't find attribute {name} of type {type}");
        }

        public float GetOptionalFloat(string name, float defaultValue)
        {
            return TryFindAttribute(name, AttributeProto.Types.AttributeType.Float, out var attr) ? attr.F : defaultValue;
        }

        public float GetRequiredFloat(string name)
        {
            return FindAttribute(name, AttributeProto.Types.AttributeType.Float).F;
        }

        public float[] GetOptionalFloatArray(string name, float[] defaultValue)
        {
            return TryFindAttribute(name, AttributeProto.Types.AttributeType.Float, out var attr) ? attr.Floats.ToArray() : defaultValue;
        }

        public float[] GetRequiredFloatArray(string name)
        {
            return FindAttribute(name, AttributeProto.Types.AttributeType.Floats).Floats.ToArray();
        }

        public TensorProto GetRequiredTensor(string name)
        {
            return FindAttribute(name, AttributeProto.Types.AttributeType.Tensor).T;
        }

        public int GetOptionalInt(string name, int defaultValue)
        {
            var v = TryFindAttribute(name, AttributeProto.Types.AttributeType.Int, out var attr) ? attr.I : defaultValue;
            return v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v;
        }

        public int GetRequiredInt(string name)
        {
            var v = FindAttribute(name, AttributeProto.Types.AttributeType.Int).I;
            return v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v;
        }

        public int[] GetOptionalIntArray(string name, int[] defaultValue)
        {
            if (!TryFindAttribute(name, AttributeProto.Types.AttributeType.Ints, out var attr))
                return defaultValue;
            return attr.Ints.Select(v => v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v).ToArray();
        }

        public int[] GetRequiredIntArray(string name)
        {
            var attribute = FindAttribute(name, AttributeProto.Types.AttributeType.Ints);
            return attribute.Ints.Select(v => v < int.MinValue ? int.MinValue : v > int.MaxValue ? int.MaxValue : (int)v).ToArray();
        }

        public string GetOptionalString(string name, string defaultValue)
        {
            if (!TryFindAttribute(name, AttributeProto.Types.AttributeType.String, out var attr))
                return defaultValue;
            return attr.S.ToStringUtf8();
        }

        public string GetRequiredString(string name)
        {
            var raw = FindAttribute(name, AttributeProto.Types.AttributeType.String).S;
            return raw.ToStringUtf8();
        }

        public string[] GetOptionalStringArray(string name, string[] defaultValue)
        {
            if (!TryFindAttribute(name, AttributeProto.Types.AttributeType.Strings, out var attr))
                return defaultValue;
            return attr.Strings.Select(s => s.ToStringUtf8()).ToArray();
        }

        public string[] GetRequiredStringArray(string name)
        {
            var attribute = FindAttribute(name, AttributeProto.Types.AttributeType.Strings);
            return attribute.Strings.Select(s => s.ToStringUtf8()).ToArray();
        }

        /// <summary>
        /// Checks if an ONNX data type is supported by Sentis.
        /// </summary>
        public static bool IsDataTypeSupported(TensorProto.Types.DataType dataType)
        {
            return dataType switch
            {
                TensorProto.Types.DataType.Undefined => true,
                TensorProto.Types.DataType.Float => true,
                TensorProto.Types.DataType.Float16 => true,
                TensorProto.Types.DataType.Double => true,
                TensorProto.Types.DataType.Bfloat16 => true,
                TensorProto.Types.DataType.Uint8 => true,
                TensorProto.Types.DataType.Int8 => true,
                TensorProto.Types.DataType.Uint16 => true,
                TensorProto.Types.DataType.Int16 => true,
                TensorProto.Types.DataType.Int32 => true,
                TensorProto.Types.DataType.Int64 => true,
                TensorProto.Types.DataType.Bool => true,
                TensorProto.Types.DataType.Uint32 => true,
                TensorProto.Types.DataType.Uint64 => true,
                _ => false
            };
        }

        public static DataType DataTypeFromOnnxDataType(TensorProto.Types.DataType dataType, DataType defaultValue = DataType.Float, Action OnUnsupported = null)
        {
            switch (dataType)
            {
                case TensorProto.Types.DataType.Undefined:
                    return defaultValue;
                case TensorProto.Types.DataType.Float:
                case TensorProto.Types.DataType.Float16:
                case TensorProto.Types.DataType.Double:
                case TensorProto.Types.DataType.Bfloat16:
                    return DataType.Float;
                case TensorProto.Types.DataType.Uint8:
                case TensorProto.Types.DataType.Int8:
                case TensorProto.Types.DataType.Uint16:
                case TensorProto.Types.DataType.Int16:
                case TensorProto.Types.DataType.Int32:
                case TensorProto.Types.DataType.Int64:
                case TensorProto.Types.DataType.Bool:
                case TensorProto.Types.DataType.Uint32:
                case TensorProto.Types.DataType.Uint64:
                    return DataType.Int;
                default:
                    OnUnsupported?.Invoke();
                    return defaultValue;
            }
        }
    }
}
