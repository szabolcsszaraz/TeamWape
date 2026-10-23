using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    partial class SelectionPopup
    {
        int m_KeyboardFocusIndex = -1;
        VisualElement m_KeyboardFocusedElement;
        const string k_KeyboardFocusClass = "mui-selection-element-keyboard-focus";

        void ResetKeyboardFocus()
        {
            m_KeyboardFocusedElement?.RemoveFromClassList(k_KeyboardFocusClass);
            m_KeyboardFocusedElement = null;
            m_KeyboardFocusIndex = -1;
        }

        void OnSearchFieldKeyDown(KeyDownEvent evt)
        {
            switch (evt.keyCode)
            {
                case KeyCode.Escape:
                    OnDismissRequested?.Invoke();
                    evt.StopPropagation();
                    break;
                case KeyCode.DownArrow:
                    SetKeyboardFocusIndex(m_KeyboardFocusIndex + 1);
                    evt.StopPropagation();
                    break;
                case KeyCode.UpArrow:
                    SetKeyboardFocusIndex(m_KeyboardFocusIndex - 1);
                    evt.StopPropagation();
                    break;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (m_KeyboardFocusIndex >= 0)
                    {
                        SelectFocusedItem();
                        evt.StopPropagation();
                    }
                    break;
            }
        }

        void SetKeyboardFocusIndex(int newIndex)
        {
            var count = m_ManagedScrollView.Count;
            if (count == 0)
            {
                return;
            }

            ResetKeyboardFocus();

            m_KeyboardFocusIndex = Math.Max(0, Math.Min(newIndex, count - 1));

            var scrollView = m_ListViewContainer.Q<ScrollView>();
            if (scrollView != null && m_KeyboardFocusIndex < scrollView.contentContainer.childCount)
            {
                var element = scrollView.contentContainer[m_KeyboardFocusIndex];
                element.AddToClassList(k_KeyboardFocusClass);
                m_KeyboardFocusedElement = element;
                element.schedule.Execute(() => scrollView.ScrollTo(element));
            }
        }

        void SelectFocusedItem()
        {
            var data = m_ManagedScrollView.Data;
            if (m_KeyboardFocusIndex < 0 || m_KeyboardFocusIndex >= data.Count)
            {
                return;
            }

            var entry = data[m_KeyboardFocusIndex];
            var scrollView = m_ListViewContainer.Q<ScrollView>();
            var element = scrollView?.contentContainer[m_KeyboardFocusIndex] as SelectionElement;
            if (element == null)
            {
                return;
            }

            if (entry.LogData.HasValue)
            {
                SelectedLogReference(entry.LogData.Value, element);
            }
            else
            {
                SelectedObject(entry.Object, element);
            }
        }
    }
}
