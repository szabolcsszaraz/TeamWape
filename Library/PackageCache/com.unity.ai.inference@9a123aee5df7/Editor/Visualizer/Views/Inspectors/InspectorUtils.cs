using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Unity.Profiling;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    static class InspectorUtils
    {
        public record PropertyData(string name, string value);

        public static List<PropertyData> GetPublicProperties(object instance, Model model, string[] excludedProperties = null)
        {
            var propertiesList = new List<PropertyData>();
            var type = instance.GetType();

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
            var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var property in properties)
            {
                if (excludedProperties?.Contains(property.Name) == true)
                    continue;

                var value = property.GetValue(instance);
                if (value is ProfilerMarker)
                    continue;

                propertiesList.Add(FormatPropertyData(property.Name, value));
            }

            foreach (var field in fields)
            {
                if (excludedProperties?.Contains(field.Name) == true)
                    continue;

                var value = field.GetValue(instance);
                if (value is DynamicTensorShape shape)
                {
                    var shapeString = model.DynamicShapeToString(shape);
                    propertiesList.Add(new PropertyData(field.Name, shapeString));
                }
                else
                {
                    propertiesList.Add(FormatPropertyData(field.Name, value));
                }
            }

            return propertiesList;
        }

        static PropertyData FormatPropertyData(string propName, object value)
        {
            if (value is IEnumerable<object> enumerable)
            {
                var elements = string.Join(", ", enumerable.Select(e => e?.ToString() ?? "null"));
                return new PropertyData(propName, $"[{elements}]");
            }

            if (value is IEnumerable enumerableNonGeneric and not string)
            {
                var items = new List<string>();
                foreach (var item in enumerableNonGeneric)
                {
                    items.Add(item?.ToString() ?? "null");
                }

                var elements = string.Join(", ", items);
                return new PropertyData(propName, $"[{elements}]");
            }

            return new PropertyData(propName, value?.ToString() ?? "null");
        }
    }
}

// We add this class to allow the use of init-only properties in records.
namespace System.Runtime.CompilerServices
{
    static class IsExternalInit {}
}
