using System.Collections.Generic;
using System.Linq;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Utilities;
using Unity.AI.Generators.UI;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Components
{
    class MeshGenerator : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Mesh/Components/MeshGenerator/MeshGenerator.uxml";

        public MeshGenerator()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            this.Q<Splitter>("vertical-splitter").Bind(
                this,
                GenerationSettingsActions.setHistoryDrawerHeight,
                Selectors.SelectHistoryDrawerHeight);

            this.Q<Splitter>("horizontal-splitter").BindHorizontal(
                this,
                GenerationSettingsActions.setGenerationPaneWidth,
                Selectors.SelectGenerationPaneWidth);

            this.UseArray(state => state.SelectGenerationFeedback(this), OnGenerationFeedbackChanged);

            var singleImageContainers = this.Query(name: "single-image-reference-container").ToList();
            var multiviewContainers = this.Query(name: "multiview-image-reference-container").ToList();
            // Fallback: also find PromptImageReference parents not in named containers (e.g., Texturing tab)
            var unnamedImageReferenceContainers = this.Query<PromptImageReference>().ToList()
                .Select(e => e.parent).Where(p => p != null && p.name != "single-image-reference-container").ToList();
            var promptContainers = this.Query<Prompt>().ToList().Select(e => e.parent).Where(p => p != null).ToList();
            var modelReferenceContainers = this.Query<PromptModelReference>().ToList().Select(e => e.parent).Where(p => p != null).ToList();

            this.Use(state => state.SelectSelectedModel(this), model =>
            {
                var supportsMultiview = model?.SupportsParam(ModelConstants.SchemaKeys.ReferenceMultiviewFront) ?? false;
                var supportsImageReference = model?.SupportsParam(ModelConstants.SchemaKeys.ReferenceImage) ?? false;

                // In the Generate tab: show single-image when model supports reference_image but not multiview
                foreach (var container in singleImageContainers)
                    container.SetShown(supportsImageReference && !supportsMultiview);

                // In the Generate tab: show multiview when model supports multiview
                foreach (var container in multiviewContainers)
                    container.SetShown(supportsMultiview);

                // Other tabs (Texturing) that have PromptImageReference outside named containers
                foreach (var container in unnamedImageReferenceContainers)
                    container.SetShown(supportsImageReference);

                var supportsTextPrompt = model?.operations.Contains(ModelConstants.Operations.TextPrompt) ?? false;
                foreach (var container in promptContainers)
                    container.SetShown(supportsTextPrompt);

                var supportsModelReference = model?.SupportsParam(ModelConstants.SchemaKeys.ReferenceModel) ?? false;
                foreach (var container in modelReferenceContainers)
                    container.SetShown(supportsModelReference);
            });
        }

        void OnGenerationFeedbackChanged(IEnumerable<GenerationFeedbackData> messages)
        {
            foreach (var feedback in messages)
            {
                this.ShowToast(feedback.message);
                this.Dispatch(GenerationActions.removeGenerationFeedback, this.GetAsset());
            }
        }
    }
}
