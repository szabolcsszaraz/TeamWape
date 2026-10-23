using Unity.AI.ModelSelector.Services.Stores.States;

namespace Unity.AI.Image.Services.Utilities
{
    static class SuperProxyExtensions
    {
        public static bool CanGenerateWithReferences(this ModelSettings model,
            bool? hasPrompt = null,
            bool? hasStyle = null,
            bool? hasComposition = null,
            bool? hasPose = null,
            bool? hasDepth = null,
            bool? hasLineArt = null,
            bool? hasFeature = null)
        {
            var providedCount = 0;
            if (hasPrompt == true) providedCount++;
            if (hasStyle == true) providedCount++;
            if (hasComposition == true) providedCount++;
            if (hasPose == true) providedCount++;
            if (hasDepth == true) providedCount++;
            if (hasLineArt == true) providedCount++;
            if (hasFeature == true) providedCount++;

            return providedCount switch
            {
                0 => true,
                1 when hasPrompt == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt),
                1 when hasStyle == true => model.operations.Contains(ModelConstants.Operations.StyleReference),
                1 when hasComposition == true => model.operations.Contains(ModelConstants.Operations.CompositionReference),
                1 when hasPose == true => model.operations.Contains(ModelConstants.Operations.PoseReference),
                1 when hasDepth == true => model.operations.Contains(ModelConstants.Operations.DepthReference),
                1 when hasLineArt == true => model.operations.Contains(ModelConstants.Operations.LineArtReference),
                1 when hasFeature == true => model.operations.Contains(ModelConstants.Operations.FeatureReference),

                2 when hasPrompt == true && hasStyle == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.StyleReference),
                2 when hasPrompt == true && hasComposition == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.CompositionReference),
                2 when hasPrompt == true && hasPose == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.PoseReference),
                2 when hasPrompt == true && hasDepth == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.DepthReference),
                2 when hasPrompt == true && hasLineArt == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.LineArtReference),
                2 when hasPrompt == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.ReferencePrompt) && model.operations.Contains(ModelConstants.Operations.FeatureReference),

                2 when hasStyle == true && hasComposition == true => model.operations.Contains(ModelConstants.Operations.StyleReference) && model.operations.Contains(ModelConstants.Operations.CompositionReference),
                2 when hasStyle == true && hasPose == true => model.operations.Contains(ModelConstants.Operations.StyleReference) && model.operations.Contains(ModelConstants.Operations.PoseReference),
                2 when hasStyle == true && hasDepth == true => model.operations.Contains(ModelConstants.Operations.StyleReference) && model.operations.Contains(ModelConstants.Operations.DepthReference),
                2 when hasStyle == true && hasLineArt == true => model.operations.Contains(ModelConstants.Operations.StyleReference) && model.operations.Contains(ModelConstants.Operations.LineArtReference),
                2 when hasStyle == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.StyleReference) && model.operations.Contains(ModelConstants.Operations.FeatureReference),
                2 when hasComposition == true && hasPose == true => model.operations.Contains(ModelConstants.Operations.CompositionReference) && model.operations.Contains(ModelConstants.Operations.PoseReference),
                2 when hasComposition == true && hasDepth == true => model.operations.Contains(ModelConstants.Operations.CompositionReference) && model.operations.Contains(ModelConstants.Operations.DepthReference),
                2 when hasComposition == true && hasLineArt == true => model.operations.Contains(ModelConstants.Operations.CompositionReference) && model.operations.Contains(ModelConstants.Operations.LineArtReference),
                2 when hasComposition == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.CompositionReference) && model.operations.Contains(ModelConstants.Operations.FeatureReference),
                2 when hasPose == true && hasDepth == true => model.operations.Contains(ModelConstants.Operations.PoseReference) && model.operations.Contains(ModelConstants.Operations.DepthReference),
                2 when hasPose == true && hasLineArt == true => model.operations.Contains(ModelConstants.Operations.PoseReference) && model.operations.Contains(ModelConstants.Operations.LineArtReference),
                2 when hasPose == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.PoseReference) && model.operations.Contains(ModelConstants.Operations.FeatureReference),
                2 when hasDepth == true && hasLineArt == true => model.operations.Contains(ModelConstants.Operations.DepthReference) && model.operations.Contains(ModelConstants.Operations.LineArtReference),
                2 when hasDepth == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.DepthReference) && model.operations.Contains(ModelConstants.Operations.FeatureReference),
                2 when hasLineArt == true && hasFeature == true => model.operations.Contains(ModelConstants.Operations.LineArtReference) && model.operations.Contains(ModelConstants.Operations.FeatureReference),

                _ => false
            };
        }
    }
}
