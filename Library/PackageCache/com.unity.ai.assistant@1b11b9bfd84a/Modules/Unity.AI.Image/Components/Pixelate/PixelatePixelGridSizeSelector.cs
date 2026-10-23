using System.Collections.Generic;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class PixelatePixelGridSizeSelector : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Image/Components/Pixelate/PixelatePixelGridSizeSelector.uxml";

        static readonly List<int> k_GridSizes = new() { 16, 32, 64, 128, 256 };
        static readonly List<string> k_GridSizeLabels = new() { "16", "32", "64", "128", "256" };

        readonly DropdownField m_Dropdown;

        public PixelatePixelGridSizeSelector()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Dropdown = this.Q<DropdownField>("pixel-grid-size-dropdown");
            m_Dropdown.choices = k_GridSizeLabels;
            m_Dropdown.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out var size))
                    this.Dispatch(GenerationSettingsActions.setPixelatePixelGridSize, size);
            });

            this.Use(state => state.SelectPixelatePixelGridSize(this), OnPixelGridSizeChanged);
        }

        void OnPixelGridSizeChanged(int pixelGridSize)
        {
            var index = k_GridSizes.IndexOf(pixelGridSize);
            if (index >= 0)
                m_Dropdown.SetValueWithoutNotify(k_GridSizeLabels[index]);
        }
    }
}
