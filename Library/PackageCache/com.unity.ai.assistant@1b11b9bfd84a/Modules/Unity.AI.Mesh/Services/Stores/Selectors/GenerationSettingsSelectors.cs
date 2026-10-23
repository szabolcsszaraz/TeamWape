using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.Actions.Payloads;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Mesh.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Utility;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Services.Stores.Selectors
{
    static partial class Selectors
    {
        internal static readonly string[] modalities = { ModelConstants.Modalities.Model3D };

        public static GenerationSettings SelectGenerationSettings(this IState state) => state.Get<GenerationSettings>(GenerationSettingsActions.slice);

        public static GenerationSetting SelectGenerationSetting(this IState state, AssetReference asset)
        {
            if (state == null)
                return new GenerationSetting();
            var settings = state.SelectGenerationSettings().generationSettings;
            return settings.Ensure(asset);
        }
        public static GenerationSetting SelectGenerationSetting(this IState state, VisualElement element) => state.SelectGenerationSetting(element.GetAsset());

        public static string SelectSelectedModelID(this IState state, VisualElement element) => state.SelectSelectedModelID(element.GetAsset());
        public static string SelectSelectedModelID(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            return state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
        }
        public static string SelectSelectedModelID(this GenerationSetting setting)
        {
            var mode = setting.SelectRefinementMode();
            return setting.selectedModels.Ensure(mode).modelID;
        }

        public static string SelectSelectedModelName(this GenerationSetting setting)
        {
            // The model settings are shared between all generation settings. We can use the modelID to find the model.
            // Normally we try to use the store from the window context, but here we have a design flaw and will
            // use the shared store instead of modifying the setting argument which could be risky for serialization and dictionary lookups.
            // Suggestion: we could add an overload to MakeMetadata that takes the store as an argument and passes it here
            var store = SessionPersistence.SharedStore.Store;
            if (store?.State == null)
                return null;

            var modelID = setting.SelectSelectedModelID();
            var modelSettings = store.State.SelectModelSettingsWithModelId(modelID);

            return modelSettings?.name;
        }

        public static ModelSettings SelectSelectedModel(this IState state, VisualElement element) => state.SelectSelectedModel(element.GetAsset());
        public static ModelSettings SelectSelectedModel(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            var modelID = state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
            var model = ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
            return model;
        }

        public static string[] SelectRefinementOperations(RefinementMode mode)
        {
            var operations = mode switch
            {
                RefinementMode.Generation => new [] { ModelConstants.Operations.TextPrompt, ModelConstants.Operations.ReferencePrompt },
                RefinementMode.Retopology => new [] { ModelConstants.Operations.ReferencePrompt },
                RefinementMode.Texturing => new [] { ModelConstants.Operations.TextPrompt, ModelConstants.Operations.ReferencePrompt },
                RefinementMode.Rigging => new [] { ModelConstants.Operations.ReferencePrompt },
                _ => new [] { ModelConstants.Operations.TextPrompt }
            };
            return operations;
        }
        public static string[] SelectRefinementOperations(this IState state, AssetReference asset) => SelectRefinementOperations(state.SelectRefinementMode(asset));
        public static string[] SelectRefinementOperations(this IState state, VisualElement element) => state.SelectRefinementOperations(element.GetAsset());

        public static string[] SelectRefinementCapabilities(RefinementMode mode)
        {
            return mode switch
            {
                RefinementMode.Generation => new[] { "Generate" },
                RefinementMode.Retopology => new[] { "Retopology" },
                RefinementMode.Texturing => new[] { "Texturing" },
                RefinementMode.Rigging => new[] { "Rigging" },
                _ => new[] { "Generate" }
            };
        }
        public static string[] SelectRefinementCapabilities(this IState state, AssetReference asset) => SelectRefinementCapabilities(state.SelectRefinementMode(asset));
        public static string[] SelectRefinementCapabilities(this IState state, VisualElement element) => state.SelectRefinementCapabilities(element.GetAsset());

        public static (RefinementMode mode, bool should, bool hasModels, long timestamp) SelectShouldAutoAssignModel(this IState state, AssetReference asset)
        {
            var mode = state.SelectRefinementMode(asset);
            // One filtered query yields both values; see ModelSelectorSelectors.SelectAutoAssignState.
            var (should, hasModels) = ModelSelectorSelectors.SelectAutoAssignState(state, state.SelectSelectedModelID(asset), modalities: modalities,
                operations: state.SelectRefinementOperations(asset), capabilities: SelectRefinementCapabilities(mode));
            return (mode, should, hasModels, ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state));
        }

        public static (RefinementMode mode, bool should, bool hasModels, long timestamp) SelectShouldAutoAssignModel(this IState state, VisualElement element) =>
            state.SelectShouldAutoAssignModel(element.GetAsset());

        public static GenerationSetting EnsureSelectedModelID(this GenerationSetting setting, IState state)
        {
            foreach (RefinementMode mode in Enum.GetValues(typeof(RefinementMode)))
            {
                var selection = setting.selectedModels.Ensure(mode);
                var historyId = state.SelectSession().settings.lastSelectedModels.Ensure(mode).modelID;
                var operations = SelectRefinementOperations(mode);
                var capabilities = SelectRefinementCapabilities(mode);

                selection.modelID = ModelSelectorSelectors.ResolveEffectiveModelID(
                    state,
                    selection.modelID,
                    historyId,
                    modalities: modalities,
                    operations: operations,
                    capabilities: capabilities
                );
            }
            return setting;
        }

        public static ModelSettings SelectModelSettings(this IState state, GenerationMetadata generationMetadata)
        {
            var modelID = generationMetadata.model;
            var model = ModelSelectorSelectors.SelectModelSettings(state).FirstOrDefault(s => s.id == modelID);
            return model;
        }

        public static string SelectTooltipModelSettings(this IState state, GenerationMetadata generationMetadata)
        {
            const string noDataFoundString = "No generation data found";

            if (generationMetadata == null)
                return noDataFoundString;

            var text = string.Empty;

            if (!string.IsNullOrEmpty(generationMetadata.prompt))
                text += $"Prompt: {generationMetadata.prompt}\n";

            if (!string.IsNullOrEmpty(generationMetadata.negativePrompt))
                text += $"Negative prompt: {generationMetadata.negativePrompt}\n";

            if (!string.IsNullOrEmpty(generationMetadata.refinementMode))
                text += $"Operation: {generationMetadata.refinementMode.AddSpaceBeforeCapitalLetters()}\n";

            var modelSettings = state.SelectModelSettings(generationMetadata);
            if (!string.IsNullOrEmpty(modelSettings?.name))
            {
                text += $"Model: {modelSettings.name}\n";
            }
            else if(!string.IsNullOrEmpty(generationMetadata.modelName))
            {
                text += $"Model: {generationMetadata.modelName}\n";
            }

            if (!string.IsNullOrEmpty(generationMetadata.promptImageReferenceGuid))
            {
                var imgPath = AssetDatabase.GUIDToAssetPath(generationMetadata.promptImageReferenceGuid);
                var imgName = string.IsNullOrEmpty(imgPath) ? generationMetadata.promptImageReferenceGuid : Path.GetFileName(imgPath);
                text += $"Reference image: {imgName}\n";
            }

            if (!string.IsNullOrEmpty(generationMetadata.modelReferenceGuid))
            {
                var modelPath = AssetDatabase.GUIDToAssetPath(generationMetadata.modelReferenceGuid);
                var refModelName = string.IsNullOrEmpty(modelPath) ? generationMetadata.modelReferenceGuid : Path.GetFileName(modelPath);
                text += $"Reference model: {refModelName}\n";
            }

            if (generationMetadata.faceLimit >= 0)
                text += $"Face count: {generationMetadata.faceLimit:N0}\n";

            text = text.TrimEnd();

            if (string.IsNullOrEmpty(text))
                text = noDataFoundString;

            return text;
        }

        public static string SelectPrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectPrompt();
        public static string SelectPrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.prompt);

        public static string SelectNegativePrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectNegativePrompt();
        public static string SelectNegativePrompt(this GenerationSetting setting) => PromptUtilities.TruncatePrompt(setting.negativePrompt);

        public static int SelectVariationCount(this IState state, VisualElement element) => state.SelectGenerationSetting(element).variationCount;
        public static int SelectVariationCount(this GenerationSetting setting) => setting.variationCount;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) =>
            (setting.useCustomSeed, setting.customSeed);

        public static (bool useFaceLimit, int faceLimit) SelectFaceLimitOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useFaceLimit, settings.faceLimit);
        }

        public static int SelectFaceLimit(this IState state, VisualElement element) => state.SelectGenerationSetting(element).faceLimit;
        public static int SelectFaceLimit(this GenerationSetting setting) => setting.faceLimit;

        public static string SelectTargetFormat(this IState state, VisualElement element) => state.SelectGenerationSetting(element).targetFormat;
        public static string SelectTargetFormat(this GenerationSetting setting) => setting.targetFormat;

        public static RefinementMode SelectRefinementMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).refinementMode;
        public static RefinementMode SelectRefinementMode(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).refinementMode;
        public static RefinementMode SelectRefinementMode(this GenerationSetting setting) => setting.refinementMode;

        public static bool SelectRequiresGlTFast(this GenerationSetting setting) =>
            setting.refinementMode != RefinementMode.Generation
            || string.Equals(setting.targetFormat, "glb", StringComparison.OrdinalIgnoreCase);
        public static bool SelectRequiresGlTFast(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectRequiresGlTFast();
        public static bool SelectRequiresGlTFast(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectRequiresGlTFast();

        public static PromptImageReference SelectPromptImageReference(this GenerationSetting setting) => setting.promptImageReference;

        public static AssetReference SelectPromptImageReferenceAsset(this IState state, VisualElement element) => state.SelectGenerationSetting(element).promptImageReference.asset;
        public static AssetReference SelectPromptImageReferenceAsset(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).promptImageReference.asset;

        public static List<MultiviewImageReferenceSettings> SelectMultiviewImageReferences(this GenerationSetting setting)
            => setting.multiviewImageReferences ?? MultiviewImageReferenceSettings.CreateDefaults();
        public static List<MultiviewImageReferenceSettings> SelectMultiviewImageReferences(this IState state, VisualElement element)
            => state.SelectGenerationSetting(element).SelectMultiviewImageReferences();
        public static List<MultiviewImageReferenceSettings> SelectMultiviewImageReferences(this IState state, AssetReference asset)
            => state.SelectGenerationSetting(asset).SelectMultiviewImageReferences();

        public static MultiviewImageReferenceSettings SelectMultiviewImageReference(this IState state, VisualElement element, int index)
        {
            var refs = state.SelectMultiviewImageReferences(element);
            return index >= 0 && index < refs.Count ? refs[index] : null;
        }

        public static AssetReference SelectMultiviewImageReferenceAsset(this IState state, VisualElement element, int index)
            => state.SelectMultiviewImageReference(element, index)?.asset ?? new AssetReference();

        public static ModelReference SelectModelReference(this GenerationSetting setting) => setting.modelReference;

        public static AssetReference SelectModelReferenceAsset(this IState state, VisualElement element) =>
            state.SelectGenerationSetting(element).modelReference.asset;

        public static AssetReference SelectModelReferenceAsset(this IState state, AssetReference asset) =>
            state.SelectGenerationSetting(asset).modelReference.asset;

        public static string SelectProgressLabel(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectProgressLabel();

        public static string SelectProgressLabel(this GenerationSetting setting) =>
            setting.refinementMode switch
            {
                RefinementMode.Generation => $"Generating with {setting.prompt}",
                RefinementMode.Retopology => $"Retopologizing with {setting.prompt}",
                RefinementMode.Texturing => $"Texturing with {setting.prompt}",
                RefinementMode.Rigging => $"Rigging with {setting.prompt}",
                _ => "Failing"
            };

        public static Texture2D SelectPromptImageReferenceBackground(this IState state, VisualElement element)
        {
            var promptImageReferenceAsset = state.SelectPromptImageReferenceAsset(element);
            if (promptImageReferenceAsset.IsValid())
                return null; // already shown on top layer

            return null;
        }

        public static async Task<Stream> SelectPromptImageReferenceAssetStream(this IState state, AssetReference asset)
        {
            var promptImageReferenceAsset = state.SelectPromptImageReferenceAsset(asset);
            if (!promptImageReferenceAsset.IsValid())
                return null;

            return await promptImageReferenceAsset.GetCompatibleImageStreamAsync();
        }

        public static Stream SelectModelReferenceAssetStream(this IState state, AssetReference asset)
        {
            var modelReferenceAsset = state.SelectModelReferenceAsset(asset);
            if (!modelReferenceAsset.IsValid())
                return null;
            var path = modelReferenceAsset.GetPath();
            if (string.IsNullOrEmpty(path))
                return null;
            return new FileStream(Path.GetFullPath(path), FileMode.Open, FileAccess.Read);
        }

        public static bool SelectAssetExists(this IState state, AssetReference asset) => asset.Exists();

        public static bool SelectAssetExists(this IState state, VisualElement element) => state.SelectAssetExists(element.GetAsset());

        public static int SelectActiveReferencesCount(this IState state, VisualElement element)
        {
            var count = 0;
            var generationSetting = state.SelectGenerationSetting(element);
            var promptImageReference = generationSetting.SelectPromptImageReference();
            if (promptImageReference.asset.IsValid())
                count++;
            var multiviewRefs = generationSetting.SelectMultiviewImageReferences();
            count += multiviewRefs.EnumerateValidViews().Count();
            var modelReference = generationSetting.SelectModelReference();
            if (modelReference.asset.IsValid())
                count++;

            return count;
        }

        public static GenerationValidationSettings SelectGenerationValidationSettings(this IState state, VisualElement element)
        {
            var asset = element.GetAsset();
            var settings = state.SelectGenerationSetting(asset);
            var prompt = string.IsNullOrWhiteSpace(settings.SelectPrompt());
            var negativePrompt = string.IsNullOrWhiteSpace(settings.SelectNegativePrompt());
            var model = state.SelectSelectedModelID(asset);
            var variations = settings.SelectVariationCount();
            var mode = settings.SelectRefinementMode();
            var referenceCount = state.SelectActiveReferencesCount(element);
            var modelsTimeStamp = ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state);
            var targetFormat = settings.SelectTargetFormat();
            return new GenerationValidationSettings(asset, asset.Exists(), prompt, negativePrompt, model, variations, mode, referenceCount, modelsTimeStamp, targetFormat);
        }

        public static IEnumerable<RefinementMode> SelectAvailableRefinementModes(this IState state, AssetReference asset) =>
            Enum.GetValues(typeof(RefinementMode)).Cast<RefinementMode>();

        public static IEnumerable<RefinementMode> SelectAvailableRefinementModesOrdered(this IState state, AssetReference asset)
        {
            var available = state.SelectAvailableRefinementModes(asset);
            return available.OrderBy(mode =>
            {
                var member = typeof(RefinementMode).GetMember(mode.ToString()).FirstOrDefault();
                var attr = member?.GetCustomAttributes(typeof(DisplayOrderAttribute), false).FirstOrDefault() as DisplayOrderAttribute;
                return attr?.order ?? 0;
            });
        }

        public static IEnumerable<RefinementMode> SelectAvailableRefinementModesOrdered(this IState state, VisualElement element) =>
            state.SelectAvailableRefinementModesOrdered(element.GetAsset());

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;

        public static float SelectGenerationPaneWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).generationPaneWidth;
        
        
        
        public static MeshSettingsState SelectMeshSettings(this IState state, VisualElement element) => state.SelectGenerationSetting(element).meshSettings;
        public static MeshSettingsState SelectMeshSettings(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).meshSettings;
        public static bool SelectMeshSettingsButtonVisible(this IState state, VisualElement element) => state.SelectMeshSettingsButtonVisible(element.GetAsset());
        public static bool SelectMeshSettingsButtonVisible(this IState state, AssetReference asset) =>
            asset.IsValid();
    }
}
