using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Panel
{
    [UxmlElement]
    partial class SpritesheetPanel : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/Modules/Unity.AI.Image/Panels/SpritesheetPanel/SpritesheetPanel.uxml";
        public SpritesheetPanel()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            var authoring = this.Q<VisualElement>("authoring-section");
            var generating = this.Q<VisualElement>("generating-section");
            var firstImage = this.Q<VisualElement>("firstImage");
            var lastImage = this.Q<VisualElement>("lastImage");

            this.Use(state => state.SelectSelectedGeneration(this), result =>
            {
                authoring.SetShown(result.IsVideoClip());
                generating.SetShown(!result.IsVideoClip());
            });

            this.Use(state => state.SelectSelectedModel(this), model =>
            {
                if (model == null)
                    return;
                firstImage?.SetShown(model.SupportsParam(ModelConstants.SchemaKeys.StartImage) ||
                                     model.SupportsParam(ModelConstants.SchemaKeys.ImageUrlSnake) ||
                                     model.SupportsParam("image"));
                lastImage?.SetShown(model.SupportsParam(ModelConstants.SchemaKeys.EndImage) ||
                                    model.SupportsParam(ModelConstants.SchemaKeys.LastFrameImage) ||
                                    model.SupportsParam(ModelConstants.SchemaKeys.LastFrameSnake));
            });
        }
    }
}
