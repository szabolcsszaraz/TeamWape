using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Extensions
{
    static class VisualElementExtensions
    {
        static readonly PropertyInfo k_WorldBoundingBox =
            typeof(VisualElement).GetProperty("worldBoundingBox",
                BindingFlags.Instance | BindingFlags.NonPublic);

        internal static Rect GetWorldBoundingBox(this VisualElement element)
        {
            return (Rect)k_WorldBoundingBox.GetValue(element);
        }
    }
}
