using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Unity.AI.Assistant.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    /// <summary>
    /// Works around a Unity engine error while composing CJK (e.g. Korean) IME input in a TextField:
    /// <c>TextEditingUtilities.GeneratePreviewString</c> inserts the composition at an unchecked cursor
    /// index that can overshoot the committed text and throw. We reflect into the TextElement's internal
    /// editing state and clamp the saved/live cursor to the text length before the engine reads it.
    /// Fails safe: any unresolved member disables the workaround for the session (one warning logged).
    /// </summary>
    /// <remarks>
    /// TODO: Remove this once the engine clamps the composition cursor itself in
    /// <c>TextEditingUtilities.GeneratePreviewString</c> (track via the upstream Unity issue).
    /// It reflects into private UIElements members by name, so a Unity version bump can break it;
    /// when that happens the fail-safe disables it with a single warning rather than throwing.
    /// </remarks>
    static class ImeCompositionErrorWorkaround
    {
        // Set false the first time reflection can't be resolved, so we stop retrying for the session.
        static bool s_Available = true;

        static PropertyInfo s_EditingManipulatorProp; // TextElement.editingManipulator
        static FieldInfo s_EditingUtilitiesField;      // TextEditingManipulator.editingUtilities
        static PropertyInfo s_TextProp;                // TextEditingUtilities.text
        static FieldInfo s_SavedStateField;            // TextEditingUtilities.m_CursorIndexSavedState
        static FieldInfo s_SelectingUtilField;         // TextEditingUtilities.m_TextSelectingUtility
        static PropertyInfo s_CursorIndexProp;         // TextSelectingUtilities.cursorIndex
        static PropertyInfo s_SelectIndexProp;         // TextSelectingUtilities.selectIndex

        const BindingFlags k_Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // The editing/selecting objects are stable for a TextField's lifetime, so resolve them once and
        // cache them per field. ConditionalWeakTable keeps the entry from pinning the field in memory.
        sealed class CachedState
        {
            public object EditingUtilities;
            public object SelectingUtility;
        }

        static readonly ConditionalWeakTable<TextField, CachedState> s_Cache = new();

        /// <summary>
        /// Clamps the IME composition cursor to the text length. Call from a TrickleDown KeyDownEvent
        /// handler so it runs before the engine generates the preview. No-op unless composing; never throws.
        /// </summary>
        internal static void ClampCompositionCursor(TextField field)
        {
            if (!s_Available || field == null)
                return;

            if (Input.compositionString.Length == 0)
                return;

            try
            {
                // Resolve the editing/selecting objects once per field (this also resolves the static
                // member handles); subsequent keystrokes hit the cache and skip the tree query.
                if (!s_Cache.TryGetValue(field, out var state))
                {
                    var editingUtilities = ResolveEditingUtilities(field);
                    if (editingUtilities == null)
                        return;

                    state = new CachedState
                    {
                        EditingUtilities = editingUtilities,
                        SelectingUtility = s_SelectingUtilField.GetValue(editingUtilities)
                    };
                    s_Cache.Add(field, state);
                }

                if (s_TextProp.GetValue(state.EditingUtilities) is not string text)
                    return;

                int maxIndex = text.Length;

                // The saved state is restored into the cursor before the insert, so clamping it is the fix.
                if (s_SavedStateField.GetValue(state.EditingUtilities) is int saved && saved > maxIndex)
                    s_SavedStateField.SetValue(state.EditingUtilities, maxIndex);

                // Clamp the live cursor/select too.
                if (state.SelectingUtility != null)
                {
                    ClampIndexProperty(state.SelectingUtility, s_CursorIndexProp, maxIndex);
                    ClampIndexProperty(state.SelectingUtility, s_SelectIndexProp, maxIndex);
                }
            }
            catch (Exception e)
            {
                // Pass the full exception so the stack trace (and any inner exception) is logged —
                // reflection failures are usually only diagnosable from the trace, not the message.
                SetAvailable(false, $"reflection failed: {e}");
            }
        }

        static void ClampIndexProperty(object selecting, PropertyInfo prop, int maxIndex)
        {
            if (prop == null || !prop.CanRead || !prop.CanWrite)
                return;

            if (prop.GetValue(selecting) is int value && value > maxIndex)
                prop.SetValue(selecting, maxIndex);
        }

        static object ResolveEditingUtilities(TextField field)
        {
            // Only the editable input TextElement has a non-null editingManipulator; the label doesn't.
            // Enumerate the query state directly (struct enumerator) to avoid a List allocation per
            // keystroke while composing.
            foreach (var textElement in field.Query<TextElement>().Build())
            {
                if (textElement == null || !ResolveManipulatorMembers(textElement))
                    return null;

                var manipulator = s_EditingManipulatorProp.GetValue(textElement);
                if (manipulator == null)
                    continue;

                var editingUtilities = s_EditingUtilitiesField.GetValue(manipulator);
                if (editingUtilities == null)
                    continue;

                return ResolveUtilityMembers(editingUtilities) ? editingUtilities : null;
            }

            return null;
        }

        // Returns false (and disables the workaround) if the expected members can't be resolved.
        static bool ResolveManipulatorMembers(TextElement textElement)
        {
            if (s_EditingManipulatorProp != null && s_EditingUtilitiesField != null)
                return true;

            s_EditingManipulatorProp = GetPropertyDeep(textElement.GetType(), "editingManipulator");
            if (s_EditingManipulatorProp == null)
            {
                SetAvailable(false, "TextElement.editingManipulator not found");
                return false;
            }

            // Null until editing is set up; the caller skips such elements. Resolve the field once we
            // have an instance.
            var manipulator = s_EditingManipulatorProp.GetValue(textElement);
            if (manipulator == null)
                return true;

            s_EditingUtilitiesField = GetFieldDeep(manipulator.GetType(), "editingUtilities");
            if (s_EditingUtilitiesField == null)
            {
                SetAvailable(false, "TextEditingManipulator.editingUtilities not found");
                return false;
            }

            return true;
        }

        // Resolves the TextEditingUtilities / TextSelectingUtilities member handles.
        static bool ResolveUtilityMembers(object editingUtilities)
        {
            if (s_TextProp == null || s_SavedStateField == null || s_SelectingUtilField == null)
            {
                var t = editingUtilities.GetType();
                s_TextProp = GetPropertyDeep(t, "text");
                s_SavedStateField = GetFieldDeep(t, "m_CursorIndexSavedState");
                s_SelectingUtilField = GetFieldDeep(t, "m_TextSelectingUtility");

                if (s_TextProp == null || !s_TextProp.CanRead ||
                    s_SavedStateField == null || s_SelectingUtilField == null)
                {
                    SetAvailable(false, "TextEditingUtilities members not found");
                    return false;
                }
            }

            if (s_CursorIndexProp == null || s_SelectIndexProp == null)
            {
                var selecting = s_SelectingUtilField.GetValue(editingUtilities);
                if (selecting != null)
                {
                    var st = selecting.GetType();
                    s_CursorIndexProp = GetPropertyDeep(st, "cursorIndex");
                    s_SelectIndexProp = GetPropertyDeep(st, "selectIndex");

                    // Fail safe like the other reflection checks: if these can't be resolved, disable
                    // the workaround instead of re-probing on every keystroke.
                    if (s_CursorIndexProp == null || s_SelectIndexProp == null)
                    {
                        SetAvailable(false, "TextSelectingUtilities index members not found");
                        return false;
                    }
                }
            }

            return true;
        }

        static FieldInfo GetFieldDeep(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = t.GetField(name, k_Instance);
                if (f != null)
                    return f;
            }

            return null;
        }

        static PropertyInfo GetPropertyDeep(Type type, string name)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var p = t.GetProperty(name, k_Instance | BindingFlags.DeclaredOnly);
                if (p != null)
                    return p;
            }

            return null;
        }

        internal static void SetAvailable(bool available, string reason = null)
        {
            s_Available = available;
            if (!available)
                InternalLog.LogWarning($"AI Assistant: Korean IME composition workaround disabled ({reason}). " +
                                       "The prompt field falls back to default behaviour.");
        }
    }
}
