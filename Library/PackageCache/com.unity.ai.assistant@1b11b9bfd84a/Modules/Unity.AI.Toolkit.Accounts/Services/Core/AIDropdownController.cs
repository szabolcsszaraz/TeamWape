using System;
using Unity.AI.Toolkit.Accounts.Components;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine.UIElements;

namespace Unity.AI.Toolkit.Accounts.Services
{
#if UNITY_6000_3_OR_NEWER
    static class AIDropdownController
    {
        internal static AIDropdownContent dropdownContent;
        internal static Button aiButton;
        internal static event Action OnDropdownOpened;

        [InitializeOnLoadMethod]
        internal static void Init() => AIDropdownConfig.instance.RegisterController(new()
        {
            button = button =>
            {
                if (aiButton != null)
                    aiButton.clicked -= OnButtonClicked;
                aiButton = button;
                aiButton.clicked += OnButtonClicked;
                AIToolbarButton.Init();
                PreferencesUtils.RegisterHideMenuChanged(SetButtonVisibility);
            },
            content = dropdownContent ??= new()
        });

        static void OnButtonClicked() => OnDropdownOpened?.Invoke();

        static void SetButtonVisibility(bool hidden) =>
            aiButton.style.display = hidden ? DisplayStyle.None : DisplayStyle.Flex;

        internal static void Reset()
        {
            dropdownContent = null;
            aiButton = null;
            OnDropdownOpened = null;
            AIDropdownConfig.instance.RegisterController(null);
        }
    }
#endif
}
