using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    static class FunctionCallParameterFormatter
    {
        public static string FormatInstanceID(JToken value)
        {
#if UNITY_6000_5_OR_NEWER
            var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)value.Value<long>()));
#elif UNITY_6000_3_OR_NEWER
            var obj = EditorUtility.EntityIdToObject((int)value.Value<long>());
#else
            var obj = EditorUtility.InstanceIDToObject((int)value.Value<long>());
#endif
            var displayName = obj ? obj.name : null;
            if (displayName != null)
                return $"{value.ConvertToString()} '{displayName}' [{obj.GetType().Name}]";
            return value.ConvertToString();
        }
    }
}
