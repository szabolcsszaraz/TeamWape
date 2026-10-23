using System.Collections.Generic;
using System.Linq;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Utilities;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class AspectRatioDropDown : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Components/AspectRatioDropDown/AspectRatioDropDown.uxml";

        readonly DropdownField m_AspectRatioDropdown;

        public AspectRatioDropDown()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_AspectRatioDropdown = this.Q<DropdownField>("aspect-ratio-dropdown");

            m_AspectRatioDropdown.RegisterValueChangedCallback(evt =>
            {
                this.Dispatch(GenerationSettingsActions.setSelectedAspectRatio, evt.newValue);
            });

            this.UseArray(state => state.SelectModelSettingsAspectRatios(this).ToList(), OnAspectRatiosChanged);
            this.Use(state => state.SelectAspectRatio(this), OnAspectRatioChanged);
            this.UseAsset(asset => this.SetShown(!asset.IsCubemap()));
        }

        void OnAspectRatiosChanged(List<string> ratios)
        {
            m_AspectRatioDropdown.choices = ratios;
        }

        void OnAspectRatioChanged(string ratio)
        {
            m_AspectRatioDropdown.SetValueWithoutNotify(ratio);
        }
    }
}
