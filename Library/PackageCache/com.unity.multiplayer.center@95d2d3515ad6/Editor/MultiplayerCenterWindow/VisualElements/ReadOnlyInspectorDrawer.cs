using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Multiplayer.Center.Editor
{
    /// <summary>
    /// Use on a field to display it disabled in the inspector.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    class ReadOnlyInspectorAttribute : PropertyAttribute { }

    /// <summary>
    /// Custom drawer for the ReadOnlyInspectorAttribute.
    /// </summary>
    /// <remarks>
    /// This inspector drawer is specifically designed to work with
    /// enums from classes inheriting from <see cref="EnumBasedDescription{TEnum,TDescription}"/>.
    /// </remarks>
    [CustomPropertyDrawer(typeof(ReadOnlyInspectorAttribute))]
    class ReadOnlyInspectorDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var field = new PropertyField(property);
            field.SetEnabled(false);
            return field;
        }
    }
}
