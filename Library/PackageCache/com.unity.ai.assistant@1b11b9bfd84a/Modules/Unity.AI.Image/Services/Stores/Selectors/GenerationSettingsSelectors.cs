using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Utility;
using UnityEngine;
using UnityEngine.UIElements;
using Settings = Unity.AI.Image.Services.Stores.States.Settings;

namespace Unity.AI.Image.Services.Stores.Selectors
{
    static partial class Selectors
    {
        static readonly ImmutableArray<ImageDimensions> k_DefaultModelSettingsResolutions = new(new []{ new ImageDimensions { width = 1024, height = 1024 } });
        internal static readonly string[] tileableModalities = { ModelConstants.Modalities.Texture2d };
        internal static readonly string[] spriteModalities = { ModelConstants.Modalities.Image };
        internal static readonly string[] imageModalities = { ModelConstants.Modalities.Image, ModelConstants.Modalities.Texture2d };
        internal static readonly string[] cubemapModalities = { ModelConstants.Modalities.Skybox };
        internal static readonly string[] spritesheetModalities = { ModelConstants.Modalities.Video };

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
                RefinementMode.Generation => new [] { ModelConstants.Operations.TextPrompt },
                RefinementMode.Spritesheet => new [] { ModelConstants.Operations.TextPrompt },
                RefinementMode.Upscale => new [] { ModelConstants.Operations.Upscale },
                RefinementMode.Pixelate => new [] { ModelConstants.Operations.Pixelate },
                RefinementMode.RemoveBackground => new [] { ModelConstants.Operations.RemoveBackground },
                RefinementMode.Recolor => new [] { ModelConstants.Operations.RecolorReference },
                _ => new [] { ModelConstants.Operations.TextPrompt }
            };
            return operations;
        }
        public static string[] SelectRefinementOperations(this IState state, AssetReference asset) => SelectRefinementOperations(state.SelectRefinementMode(asset));
        public static string[] SelectRefinementOperations(this IState state, VisualElement element) => state.SelectRefinementOperations(element.GetAsset());

        public static string[] SelectRefinementCapabilities(RefinementMode mode, AssetReference asset = null)
        {
            return mode switch
            {
                RefinementMode.Generation => new[] { "Generate" },
                RefinementMode.RemoveBackground => new[] { "RemoveBackground" },
                // Cubemap upscale accepts both the legacy "SkyboxUpscale" and the renamed "Upscale"
                // capability (proton FUTR-2617); the capability filter is OR-matched, so listing both
                // keeps the skybox upscale model discoverable across the rename. This selector only
                // drives model discovery/auto-assign — the sent request capability is set explicitly
                // in Quote/Generation.
                RefinementMode.Upscale => asset != null && asset.IsCubemap() ? new[] { "Upscale", "SkyboxUpscale" } : new[] { "Upscale" },
                RefinementMode.Pixelate => new[] { "Pixelate" },
                RefinementMode.Recolor => new[] { "Recolor" },
                RefinementMode.Spritesheet => new[] { "Generate" },
                _ => new[] { "Generate" }
            };
        }
        public static string[] SelectRefinementCapabilities(this IState state, AssetReference asset) => SelectRefinementCapabilities(state.SelectRefinementMode(asset), asset);
        public static string[] SelectRefinementCapabilities(this IState state, VisualElement element) => state.SelectRefinementCapabilities(element.GetAsset());

        public static bool SelectSelectedModelOperationIsValid(this IState state, VisualElement element, string op) =>
            state.SelectSelectedModelOperationIsValid(element.GetAsset(), op);

        public static bool SelectSelectedModelOperationIsValid(this IState state, AssetReference asset, string op)
        {
            var mode = state.SelectRefinementMode(asset);
            var modelID = state.SelectGenerationSetting(asset).selectedModels.Ensure(mode).modelID;
            var model = state.SelectModelSettings().FirstOrDefault(s => s.id == modelID);
            return model != null && model.operations.Contains(op);
        }

        public static string[] SelectModalities(AssetReference asset, RefinementMode mode)
        {
            if (asset.IsCubemap())
                return cubemapModalities;
            if (mode == RefinementMode.Spritesheet)
                return spritesheetModalities;
            if(asset.IsSprite())
                return spriteModalities;
            return imageModalities;
        }

        public static string[] SelectModalities(VisualElement element, RefinementMode mode) => SelectModalities(element.GetAsset(), mode);
        public static string SelectModality(AssetReference asset, RefinementMode mode) => SelectModalities(asset, mode).First();
        public static string SelectModality(VisualElement element, RefinementMode mode) => SelectModality(element.GetAsset(), mode);

        public static string SelectPromptPlaceholderText(AssetReference asset, [CanBeNull] IState state)
        {
            if (asset.IsCubemap())
                return "Fantasy landscape, floating islands, vibrant nebula, digital painting";
            var setting = state?.SelectGenerationSetting(asset);
            return setting is { refinementMode: RefinementMode.Spritesheet }
                ? "Running, jumping, idle, attack, side-scroller animation"
                : "Small, cute slime monster, vibrant green, cartoon style, side-scroller view";
        }
        public static string SelectNegativePromptPlaceholderText(AssetReference asset) => "Blurry, messy pixels, jpeg artifacts, background, shadows, watermark";

        public static (RefinementMode mode, bool should, bool hasModels, long timestamp) SelectShouldAutoAssignModel(this IState state, VisualElement element)
        {
            var mode = state.SelectRefinementMode(element);
            var asset = element.GetAsset();
            // One filtered query yields both values; see ModelSelectorSelectors.SelectAutoAssignState.
            var (should, hasModels) = ModelSelectorSelectors.SelectAutoAssignState(state, state.SelectSelectedModelID(element),
                modalities: SelectModalities(asset, mode),
                operations: state.SelectRefinementOperations(element),
                capabilities: SelectRefinementCapabilities(mode, asset));
            return (mode, should, hasModels, ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state));
        }

        public static GenerationSetting EnsureSelectedModelID(this GenerationSetting setting, IState state, AssetReference asset)
        {
            foreach (RefinementMode mode in Enum.GetValues(typeof(RefinementMode)))
            {
                var selection = setting.selectedModels.Ensure(mode);
                var modalities = SelectModalities(asset, mode);
                var operations = SelectRefinementOperations(mode);
                var capabilities = SelectRefinementCapabilities(mode, asset);

                // Specific logic to fetch history
                var modality = modalities.First();
                var historyId = state.SelectSession().settings.lastSelectedModels.Ensure(new LastSelectedModelKey(modality, mode)).modelID;

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

            if (generationMetadata.duration > 0)
                text += $"Duration: {generationMetadata.duration}s\n";

            text = text.TrimEnd();

            if (string.IsNullOrEmpty(text))
                text = noDataFoundString;

            return text;
        }

        public static string SelectPrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectPrompt();
        public static string SelectPrompt(this GenerationSetting setting)
        {
            setting.prompt.TryGetValue(setting.refinementMode, out var prompt);
            return PromptUtilities.TruncatePrompt(prompt);
        }

        public static string SelectNegativePrompt(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectNegativePrompt();
        public static string SelectNegativePrompt(this GenerationSetting setting)
        {
            setting.negativePrompt.TryGetValue(setting.refinementMode, out var negativePrompt);
            return PromptUtilities.TruncatePrompt(negativePrompt);
        }

        public static int SelectVariationCount(this IState state, VisualElement element) => state.SelectGenerationSetting(element).variationCount;
        public static int SelectVariationCount(this GenerationSetting setting) => setting.variationCount;

        public static SerializableDictionary<string, string> SelectDynamicParams(this IState state, VisualElement element) =>
            state.SelectGenerationSetting(element).dynamicParams;
        public static SerializableDictionary<string, string> SelectDynamicParams(this GenerationSetting setting) => setting.dynamicParams;

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this IState state, VisualElement element)
        {
            var settings = state.SelectGenerationSetting(element);
            return (settings.useCustomSeed, settings.customSeed);
        }

        public static (bool useCustomSeed, int customSeed) SelectGenerationOptions(this GenerationSetting setting) =>
            (setting.useCustomSeed, setting.customSeed);

        public static int SelectDuration(this IState state, AssetReference asset, GenerationSetting setting)
        {
            var model = state.SelectSelectedModel(asset);
            if (model?.paramsSchema?.Properties != null &&
                model.paramsSchema.Properties.TryGetValue(ModelConstants.SchemaKeys.Duration, out var durationProp))
            {
                var allowed = durationProp.GetAllowedFloatValues();
                if (allowed != null)
                    return Mathf.RoundToInt(SchemaPropertiesExtensions.SnapToNearest(setting.duration, allowed));

                if (durationProp.Minimum.HasValue || durationProp.Maximum.HasValue)
                {
                    var min = (int)(durationProp.Minimum ?? 0);
                    var max = (int)(durationProp.Maximum ?? int.MaxValue);
                    return Mathf.Clamp(Mathf.RoundToInt(setting.duration), min, max);
                }
            }

            return setting.SelectDuration();
        }

        public static int SelectDuration(this IState state, AssetReference asset) =>
            state.SelectDuration(asset, state.SelectGenerationSetting(asset));

        public static int SelectDuration(this IState state, VisualElement element) => state.SelectDuration(element.GetAsset());
        public static int SelectDuration(this GenerationSetting setting) => Mathf.RoundToInt(setting.duration);
        public static float SelectDurationUnrounded(this GenerationSetting setting) => setting.duration;
        public static float SelectDurationUnrounded(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectDurationUnrounded();

        public static float SelectTrimStartTime(this IState state, VisualElement element) => state.SelectGenerationSetting(element).loopSettings.trimStartTime;
        public static float SelectTrimEndTime(this IState state, VisualElement element) => state.SelectGenerationSetting(element).loopSettings.trimEndTime;

        public static RefinementMode SelectRefinementMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).SelectRefinementMode();
        public static RefinementMode SelectRefinementMode(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectRefinementMode();
        public static RefinementMode SelectRefinementMode(this GenerationSetting setting) =>
            Enum.IsDefined(typeof(RefinementMode), setting.refinementMode) ? setting.refinementMode : RefinementMode.Generation;

        public static IEnumerable<RefinementMode> SelectAvailableRefinementModes(this IState state, VisualElement element) =>
            state.SelectAvailableRefinementModes(element.GetAsset());

        public static IEnumerable<RefinementMode> SelectAvailableRefinementModes(this IState state, AssetReference asset)
        {
            if (asset.IsCubemap())
                return new[] { RefinementMode.Generation, RefinementMode.Upscale };

            return Enum.GetValues(typeof(RefinementMode)).Cast<RefinementMode>();
        }

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

        public static int SelectUpscaleFactor(this IState state, VisualElement element) => state.SelectGenerationSetting(element).upscaleFactor;
        public static int SelectUpscaleFactor(this GenerationSetting setting) => setting.upscaleFactor;

        public static string SelectProgressLabel(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).SelectProgressLabel();

        public static string SelectProgressLabel(this GenerationSetting setting) =>
            setting.refinementMode switch
            {
                RefinementMode.Generation => $"Generating with {setting.prompt}",
                RefinementMode.RemoveBackground => "Removing background",
                RefinementMode.Upscale => "Upscaling",
                RefinementMode.Pixelate => "Pixelating",
                RefinementMode.Recolor => "Recoloring",
                RefinementMode.Spritesheet => "Creating spritesheet",
                _ => "Failing"
            };

        public static Timestamp SelectPaletteImageBytesTimeStamp(this IState state, AssetReference asset)
        {
            // UriWithTimestamp is my poor-person's memoizer
            var setting = state.SelectGenerationSetting(asset);
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            if (paletteImageReference.mode == ImageReferenceMode.Asset && paletteImageReference.asset.IsValid())
            {
                var path = Path.GetFullPath(paletteImageReference.asset.GetPath());
                return new Timestamp(File.GetLastWriteTime(path).ToUniversalTime().Ticks);
            }

            return Timestamp.FromUtcTicks(paletteImageReference.doodleTimestamp);
        }
        public static Timestamp SelectPaletteImageBytesTimeStamp(this IState state, VisualElement element) => state.SelectPaletteImageBytesTimeStamp(element.GetAsset());

        public static async Task<Stream> SelectPaletteImageStream(this GenerationSetting setting)
        {
            var paletteImageReference = setting.SelectImageReference(ImageReferenceType.PaletteImage);
            return await paletteImageReference.SelectImageReferenceStream();
        }
        public static async Task<Stream> SelectPaletteImageStream(this IState state, VisualElement element)
        {
            var setting = state.SelectGenerationSetting(element);
            return await setting.SelectPaletteImageStream();
        }

        public static byte[] SelectUnsavedAssetBytes(this GenerationSetting setting) => setting.unsavedAssetBytes.data;
        public static byte[] SelectUnsavedAssetBytes(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes.data;
        public static UnsavedAssetBytesSettings SelectUnsavedAssetBytesSettings(this IState state, VisualElement element) => state.SelectGenerationSetting(element).unsavedAssetBytes;

        public static async Task<bool> SelectHasAssetToRefine(this IState state, VisualElement element) => await state.SelectHasAssetToRefine(element.GetAsset());
        public static async Task<bool> SelectHasAssetToRefine(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();
            if (unsavedAssetBytes is { Length: > 0 })
                return true;

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            if (generations.Contains(currentSelection) && currentSelection.IsValid())
                return true;

            return asset.IsValid() && !await asset.IsOneByOnePixelOrLikelyBlankAsync();
        }

        public static async Task<bool> SelectIsAssetToRefineSpriteSheet(this IState state, VisualElement element) => await state.SelectIsAssetToRefineSpriteSheet(element.GetAsset());
        public static async Task<bool> SelectIsAssetToRefineSpriteSheet(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();
            if (unsavedAssetBytes is { Length: > 0 })
                return setting.unsavedAssetBytes.spriteSheet;

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            if (generations.Contains(currentSelection) && currentSelection.IsValid())
                return currentSelection.IsSpriteSheet();

            if (asset.IsValid() && !await asset.IsOneByOnePixelOrLikelyBlankAsync())
                return asset.IsSpriteSheet();

            return false;
        }

        public static async Task<float> SelectIsAssetToRefineDuration(this IState state, VisualElement element) => await state.SelectIsAssetToRefineDuration(element.GetAsset());
        public static async Task<float> SelectIsAssetToRefineDuration(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();
            if (unsavedAssetBytes is { Length: > 0 })
                return setting.unsavedAssetBytes.duration;

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            if (generations.Contains(currentSelection) && currentSelection.IsValid())
                return currentSelection.GetDuration();

            if (asset.IsValid() && !await asset.IsOneByOnePixelOrLikelyBlankAsync())
                return state.SelectDuration(asset);

            return 0;
        }

        public static async Task<Stream> SelectUnsavedAssetStreamWithFallback(this IState state, VisualElement element) => await state.SelectUnsavedAssetStreamWithFallback(element.GetAsset());
        public static async Task<Stream> SelectUnsavedAssetStreamWithFallback(this IState state, AssetReference asset)
        {
            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            if (!currentSelection.IsImage())
                currentSelection = new();

            // use unsaved asset bytes if available
            if (unsavedAssetBytes is { Length: > 0 })
            {
                if (ImageFileUtilities.HasAlphaChannel(unsavedAssetBytes))
                {
                    var strippedBytes = ImageFileUtilities.StripPngAlphaToGray(unsavedAssetBytes);
                    return new MemoryStream(strippedBytes);
                }

                return new MemoryStream(unsavedAssetBytes);
            }

            // fallback to selection, or asset
            var candidateStream = currentSelection.IsValid() ? await currentSelection.GetCompatibleImageStreamAsync() : await asset.GetCompatibleImageStreamAsync();

            if (candidateStream != null && ImageFileUtilities.HasAlphaChannel(candidateStream))
            {
                var strippedBytes = ImageFileUtilities.StripPngAlphaToGray(candidateStream);
                await candidateStream.DisposeAsync();
                return new MemoryStream(strippedBytes);
            }

            return candidateStream;
        }

        public static Stream SelectUnsavedAssetBytesStream(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.SelectUnsavedAssetBytes();

            // use unsaved asset bytes if available
            return unsavedAssetBytes is { Length: > 0 } ? new MemoryStream(unsavedAssetBytes) : null;
        }

        public static async Task<RenderTexture> SelectBaseAssetPreviewTexture(this IState state, AssetReference asset, int sizeHint)
        {
            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // selection, or asset
            return currentSelection.IsValid()
                ? await TextureCache.GetPreview(currentSelection.uri, sizeHint)
                : await TextureCache.GetPreview(asset.GetUri(), sizeHint);
        }

        public static Timestamp SelectBaseImageBytesTimestamp(this IState state, AssetReference asset)
        {
            var setting = state.SelectGenerationSetting(asset);
            var unsavedAssetBytes = setting.unsavedAssetBytes;

            // use unsaved asset bytes if available
            if (unsavedAssetBytes.data is { Length: > 0 })
                return Timestamp.FromUtcTicks(setting.unsavedAssetBytes.timeStamp);

            var currentSelection = state.SelectSelectedGeneration(asset);
            var generations = state.SelectGeneratedTextures(asset);

            // sanity check
            if (!generations.Contains(currentSelection))
                currentSelection = new();

            // fallback to selection
            if (currentSelection.IsValid())
                return new Timestamp(File.GetLastWriteTime(currentSelection.uri.GetAbsolutePath()).ToUniversalTime().Ticks);

            // fallback to asset
            if (!asset.IsValid())
                return new(0);

            try
            {
                var path = Path.GetFullPath(asset.GetPath());
                return new Timestamp(File.GetLastWriteTime(path).ToUniversalTime().Ticks);
            }
            catch
            {
                return new(0);
            }
        }
        public static Timestamp SelectBaseImageBytesTimestamp(this IState state, VisualElement element) => state.SelectBaseImageBytesTimestamp(element.GetAsset());

        public static ImageReferenceSettings SelectImageReference(this GenerationSetting setting, ImageReferenceType type) => setting.imageReferences[(int)type];
        public static AssetReference SelectImageReferenceAsset(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].asset;
        public static byte[] SelectImageReferenceDoodle(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].doodle;
        public static ImageReferenceMode SelectImageReferenceMode(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].mode;
        public static float SelectImageReferenceStrength(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].strength;

        public static bool SelectImageReferenceInvertStrength(this ImageReferenceType type)
        {
            IImageReference imageReference;
            switch(type)
            {
                case ImageReferenceType.PromptImage:
                    imageReference = new Unity.AI.Image.Components.PromptImageReference();
                    break;
                case ImageReferenceType.StyleImage:
                    imageReference = new Unity.AI.Image.Components.StyleImageReference();
                    break;
                case ImageReferenceType.CompositionImage:
                    imageReference = new Unity.AI.Image.Components.CompositionImageReference();
                    break;
                case ImageReferenceType.PoseImage:
                    imageReference = new Unity.AI.Image.Components.PoseImageReference();
                    break;
                case ImageReferenceType.DepthImage:
                    imageReference = new Unity.AI.Image.Components.DepthImageReference();
                    break;
                case ImageReferenceType.LineArtImage:
                    imageReference = new Unity.AI.Image.Components.LineArtImageReference();
                    break;
                case ImageReferenceType.FeatureImage:
                    imageReference = new Unity.AI.Image.Components.FeatureImageReference();
                    break;
                case ImageReferenceType.PaletteImage:
                    imageReference = new Unity.AI.Image.Components.PaletteImageReference();
                    break;
                default:
                    return false;
            }

            return imageReference.invertStrength;
        }

        public static bool SelectImageReferenceIsActive(this IState state, VisualElement element, ImageReferenceType type) => state.SelectGenerationSetting(element).imageReferences[(int)type].isActive;
        public static bool SelectImageReferenceIsActive(this ImageReferenceSettings imageReference) => imageReference.isActive;
        public static bool SelectImageReferenceAllowed(this IState state, VisualElement element, ImageReferenceType type) => true;
        public static bool SelectImageReferenceIsClear(this IState state, VisualElement element, ImageReferenceType type) =>
            !state.SelectGenerationSetting(element).imageReferences[(int)type].asset.IsValid() &&
            state.SelectImageReferenceDoodle(element, type) is not { Length: not 0 };

        public static bool SelectImageReferenceIsValid(this IState state, VisualElement element, ImageReferenceType type) =>
            state.SelectGenerationSetting(element).SelectImageReference(type).SelectImageReferenceIsValid();
        public static bool SelectImageReferenceIsValid(this ImageReferenceSettings imageReference) => imageReference.isActive &&
            (imageReference.mode == ImageReferenceMode.Doodle || imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid() && !imageReference.asset.IsOneByOnePixelOrLikelyBlank());
        public static bool SelectImageReferenceIsSpriteSheet(this ImageReferenceSettings imageReference) =>
            imageReference.isActive && imageReference.mode != ImageReferenceMode.Doodle && imageReference.mode == ImageReferenceMode.Asset &&
            imageReference.asset.IsValid() && imageReference.asset.IsSpriteSheet();

        public static async Task<Stream> SelectImageReferenceStream(this ImageReferenceSettings imageReference) =>
            imageReference.mode == ImageReferenceMode.Asset && imageReference.asset.IsValid()
                ? await imageReference.asset.GetCompatibleImageStreamAsync()
                : new MemoryStream(imageReference.doodle ?? Array.Empty<byte>());
        public static Dictionary<RefinementMode, Dictionary<ImageReferenceType, ImageReferenceSettings>> SelectImageReferencesByRefinement(this GenerationSetting setting)
        {
            var result = new Dictionary<RefinementMode, Dictionary<ImageReferenceType, ImageReferenceSettings>>();
            foreach (ImageReferenceType type in Enum.GetValues(typeof(ImageReferenceType)))
            {
                var imageReference = setting.SelectImageReference(type);
                var modes = type.GetRefinementModeForType();
                foreach (var mode in modes)
                {
                    if (!result.ContainsKey(mode))
                        result[mode] = new Dictionary<ImageReferenceType, ImageReferenceSettings>();
                    result[mode].Add(type, imageReference);
                }
            }
            return result;
        }

        public static List<ImageReferenceSettings> SelectUnlabeledImageReferences(this GenerationSetting setting)
            => setting.unlabeledImageReferences ?? new List<ImageReferenceSettings>();
        public static List<ImageReferenceSettings> SelectUnlabeledImageReferences(this IState state, VisualElement element)
            => state.SelectGenerationSetting(element).SelectUnlabeledImageReferences();
        public static List<ImageReferenceSettings> SelectUnlabeledImageReferences(this IState state, AssetReference asset)
            => state.SelectGenerationSetting(asset).SelectUnlabeledImageReferences();

        public static ImageReferenceSettings SelectUnlabeledImageReference(this IState state, VisualElement element, int index)
        {
            var refs = state.SelectUnlabeledImageReferences(element);
            return index >= 0 && index < refs.Count ? refs[index] : null;
        }

        public static AssetReference SelectUnlabeledImageReferenceAsset(this IState state, VisualElement element, int index)
            => state.SelectUnlabeledImageReference(element, index)?.asset ?? new AssetReference();
        public static byte[] SelectUnlabeledImageReferenceDoodle(this IState state, VisualElement element, int index)
            => state.SelectUnlabeledImageReference(element, index)?.doodle ?? Array.Empty<byte>();
        public static ImageReferenceMode SelectUnlabeledImageReferenceMode(this IState state, VisualElement element, int index)
            => state.SelectUnlabeledImageReference(element, index)?.mode ?? ImageReferenceMode.Asset;
        public static float SelectUnlabeledImageReferenceStrength(this IState state, VisualElement element, int index)
            => state.SelectUnlabeledImageReference(element, index)?.strength ?? 0.25f;

        public static bool SelectSupportsMultiReferenceImages(this IState state, AssetReference asset)
        {
            var model = state.SelectSelectedModel(asset);
            return model?.constants.Contains(ModelConstants.ModelCapabilities.MultiReferenceImages) ?? false;
        }
        public static bool SelectSupportsMultiReferenceImages(this IState state, VisualElement element)
            => state.SelectSupportsMultiReferenceImages(element.GetAsset());

        public static int SelectMaxReferenceImages(this IState state, AssetReference asset)
        {
            var model = state.SelectSelectedModel(asset);
            return model?.maxReferenceImages ?? 0;
        }
        public static int SelectMaxReferenceImages(this IState state, VisualElement element)
            => state.SelectMaxReferenceImages(element.GetAsset());

        public static bool SelectIsAtMaxImageReferences(this IState state, VisualElement element)
        {
            var maxImages = state.SelectMaxReferenceImages(element);
            var promptIsActive = state.SelectImageReferenceIsActive(element, ImageReferenceType.PromptImage);
            var unlabeledCount = state.SelectUnlabeledImageReferences(element)?.Count ?? 0;
            return (promptIsActive ? 1 : 0) + unlabeledCount >= maxImages;
        }

        public static int SelectPixelateTargetSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.targetSize;
        public static bool SelectPixelateKeepImageSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.keepImageSize;
        public static int SelectPixelatePixelBlockSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.pixelBlockSize;
        public static int SelectPixelatePixelGridSize(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.pixelGridSize;
        public static PixelateMode SelectPixelateMode(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pixelateSettings.mode;

        public static int SelectPixelateOutlineThickness(this IState state, VisualElement element)
        {
            return state.SelectGenerationSetting(element).SelectPixelateOutlineThickness();
        }

        public static int SelectPixelateOutlineThickness(this GenerationSetting setting)
        {
            var pixelBlockSize = setting.pixelateSettings.pixelBlockSize;
            if (pixelBlockSize < PixelateSettings.minSamplingSize)
                return 0;
            return setting.pixelateSettings.outlineThickness;
        }

        public static SpritesheetSettingsState SelectSpritesheetSettings(this IState state, VisualElement element) => state.SelectGenerationSetting(element).spritesheetSettings;
        public static SpritesheetSettingsState SelectSpritesheetSettings(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).spritesheetSettings;
        public static int SelectSpritesheetTileColumns(this IState state, VisualElement element) => state.SelectGenerationSetting(element).spritesheetSettings.tileColumns;
        public static int SelectSpritesheetTileRows(this IState state, VisualElement element) => state.SelectGenerationSetting(element).spritesheetSettings.tileRows;
        public static int SelectSpritesheetOutputWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).spritesheetSettings.outputWidth;
        public static int SelectSpritesheetOutputHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).spritesheetSettings.outputHeight;
        public static bool SelectSpritesheetSettingsButtonVisible(this IState state, VisualElement element) => state.SelectSpritesheetSettingsButtonVisible(element.GetAsset());
        public static bool SelectSpritesheetSettingsButtonVisible(this IState state, AssetReference asset) =>
            asset.IsValid() && (state.SelectRefinementMode(asset) == RefinementMode.Spritesheet || asset.IsSpriteSheet());

        public static IEnumerable<string> SelectModelSettingsResolutions(this IState state, VisualElement element)
        {
            var imageSizes = state.SelectSelectedModel(element)?.imageSizes;
            if (imageSizes == null || !imageSizes.Any())
                imageSizes = new List<ImageDimensions>(k_DefaultModelSettingsResolutions);
            return imageSizes.Select(size => $"{size.width} x {size.height}");
        }

        public static IEnumerable<string> SelectModelSettingsAspectRatios(this IState state, VisualElement element)
        {
            var aspectRatios = state.SelectSelectedModel(element)?.aspectRatios;
            if (aspectRatios == null || !aspectRatios.Any())
                return new List<string> { "1:1" };
            return aspectRatios;
        }

        public static string SelectModelSizingMode(this IState state, VisualElement element) => state.SelectModelSizingMode(element.GetAsset());
        public static string SelectModelSizingMode(this IState state, AssetReference asset)
        {
            var mode = state.SelectSelectedModel(asset)?.sizingMode;
            return string.IsNullOrEmpty(mode) ? "dimensions" : mode;
        }

        public static bool SelectModelSettingsSupportsCustomResolutions(this IState state, AssetReference asset)
        {
            var model = state.SelectSelectedModel(asset);
            return model?.constants.Contains(ModelConstants.ModelCapabilities.CustomResolutions) ?? false;
        }

        public static bool SelectModelSettingsSupportsCustomResolutions(this IState state, VisualElement element) =>
            state.SelectModelSettingsSupportsCustomResolutions(element.GetAsset());

        public static bool SelectModelRequires300PxReference(this IState state, VisualElement element) => state.SelectModelRequires300PxReference(element.GetAsset());
        public static bool SelectModelRequires300PxReference(this IState state, AssetReference asset)
        {
            var model = state.SelectSelectedModel(asset);
            return model?.limitations.Contains(ModelConstants.ModelLimitations.Requires300PxReference) ?? false;
        }


        public static bool SelectIsCustomResolutionInvalid(this IState state, AssetReference asset)
        {
            if (!state.SelectModelSettingsSupportsCustomResolutions(asset))
                return false;

            var dimensions = state.SelectGenerationSetting(asset).SelectImageDimensionsVector2();
            var (minW, maxW, minH, maxH) = state.SelectCustomResolutionRange(asset);
            return dimensions.x < minW || dimensions.x > maxW || dimensions.y < minH || dimensions.y > maxH;
        }

        /// <summary>
        /// Resolves the effective width/height min and max for the selected model.
        /// Prefers the model schema's declared minimum/maximum (e.g. GPT Image 2: 16-3840);
        /// falls back to the global CustomResolutions constants if the schema doesn't declare them.
        /// </summary>
        public static (int minWidth, int maxWidth, int minHeight, int maxHeight) SelectCustomResolutionRange(this IState state, AssetReference asset)
        {
            var properties = state.SelectSelectedModel(asset)?.paramsSchema?.Properties;
            int minW = ModelConstants.ModelCapabilities.CustomResolutionsMin;
            int maxW = ModelConstants.ModelCapabilities.CustomResolutionsMax;
            int minH = ModelConstants.ModelCapabilities.CustomResolutionsMin;
            int maxH = ModelConstants.ModelCapabilities.CustomResolutionsMax;

            if (properties != null)
            {
                if (properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Width, out var wProp) && wProp != null)
                {
                    if (wProp.Minimum.HasValue) minW = (int)wProp.Minimum.Value;
                    if (wProp.Maximum.HasValue) maxW = (int)wProp.Maximum.Value;
                }
                if (properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Height, out var hProp) && hProp != null)
                {
                    if (hProp.Minimum.HasValue) minH = (int)hProp.Minimum.Value;
                    if (hProp.Maximum.HasValue) maxH = (int)hProp.Maximum.Value;
                }
            }

            return (minW, maxW, minH, maxH);
        }

        public static List<string> SelectCrossFieldValidationErrors(this IState state, AssetReference asset)
        {
            var errors = new List<string>();
            var constraints = state.SelectSelectedModel(asset)?.paramsSchema?.CrossFieldConstraints;
            if (constraints == null || constraints.Count == 0)
                return errors;

            var dimensions = state.SelectGenerationSetting(asset).SelectImageDimensionsVector2();
            var fieldValues = new Dictionary<string, double>
            {
                [ModelConstants.SchemaKeys.Width] = dimensions.x,
                [ModelConstants.SchemaKeys.Height] = dimensions.y
            };

            foreach (var constraint in constraints)
            {
                if (constraint.Fields == null || constraint.Fields.Count == 0)
                    continue;

                var values = new List<double>();
                var resolved = true;
                foreach (var field in constraint.Fields)
                {
                    if (fieldValues.TryGetValue(field, out var val))
                        values.Add(val);
                    else
                    {
                        resolved = false;
                        break;
                    }
                }

                if (!resolved)
                    continue;

                switch (constraint.Type)
                {
                    case "total_bounds" when string.Equals(constraint.Op, "multiply", StringComparison.OrdinalIgnoreCase):
                    {
                        var product = 1.0;
                        foreach (var v in values) product *= v;
                        if ((constraint.Minimum.HasValue && product < constraint.Minimum.Value) ||
                            (constraint.Maximum.HasValue && product > constraint.Maximum.Value))
                            errors.Add(constraint.Message);
                        break;
                    }
                    case "ratio_bound" when values.Count == 2 && constraint.MaxRatio.HasValue:
                    {
                        var minVal = Math.Min(values[0], values[1]);
                        if (minVal > 0)
                        {
                            var ratio = Math.Max(values[0], values[1]) / minVal;
                            if (ratio > constraint.MaxRatio.Value)
                                errors.Add(constraint.Message);
                        }
                        break;
                    }
                }
            }

            return errors;
        }

        public static string SelectImageDimensionsRaw(this IState state, VisualElement element) => state.SelectGenerationSetting(element).imageDimensions;

        public static bool SelectUseCustomResolution(this IState state, VisualElement element) => state.SelectGenerationSetting(element).useCustomResolution;
        public static bool SelectUseCustomResolution(this GenerationSetting setting) => setting.useCustomResolution;

        public static string SelectAspectRatioRaw(this IState state, VisualElement element) => state.SelectAspectRatioRaw(element.GetAsset());
        public static string SelectAspectRatioRaw(this IState state, AssetReference asset) => state.SelectGenerationSetting(asset).selectedAspectRatio;

        public static string SelectAspectRatio(this IState state, VisualElement element)
        {
            var aspect = state.SelectAspectRatioRaw(element);
            var aspectRatios = state.SelectModelSettingsAspectRatios(element)?.ToList();
            if (aspectRatios == null || aspectRatios.Count == 0)
                return "1:1";
            return string.IsNullOrEmpty(aspect) ? aspectRatios[0] : (aspectRatios.Contains(aspect) ? aspect : aspectRatios[0]);
        }

        public static string SelectImageDimensions(this IState state, VisualElement element)
        {
            var dimension = state.SelectImageDimensionsRaw(element);
            var resolutions = state.SelectModelSettingsResolutions(element)?.ToList();
            if (resolutions == null || resolutions.Count == 0)
                return $"{k_DefaultModelSettingsResolutions[0].width} x {k_DefaultModelSettingsResolutions[0].height}";
            return resolutions.Contains(dimension) ? dimension : resolutions[0];
        }

        public static Vector2Int SelectImageDimensionsVector2(this GenerationSetting setting)
        {
            if (string.IsNullOrEmpty(setting.imageDimensions))
                return new Vector2Int(k_DefaultModelSettingsResolutions[0].width, k_DefaultModelSettingsResolutions[0].height);

            var dimensionsSplit = setting.imageDimensions.Split(" x ");

            int.TryParse(dimensionsSplit[0], out var width);
            int.TryParse(dimensionsSplit[1], out var height);

            if (width == 0 || height == 0)
            {
                width = k_DefaultModelSettingsResolutions[0].width;
                height = k_DefaultModelSettingsResolutions[0].height;
            }

            var dimensions = new Vector2Int(width, height);
            return dimensions;
        }

        public static IEnumerable<ImageReferenceType> SelectActiveReferencesTypes(this IState state, AssetReference asset)
        {
            var active = new List<ImageReferenceType>();
            var generationSetting = state.SelectGenerationSetting(asset);
            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var imageReference = generationSetting.SelectImageReference(type);
                if (imageReference.isActive)
                    active.Add(type);
            }
            return active;
        }

        public static IEnumerable<ImageReferenceType> SelectActiveReferencesTypes(this IState state, VisualElement element) =>
            state.SelectActiveReferencesTypes(element.GetAsset());

        public static IEnumerable<string> SelectActiveReferences(this IState state, VisualElement element)
        {
            var active = new List<string>();
            var generationSetting = state.SelectGenerationSetting(element);
            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var imageReference = generationSetting.SelectImageReference(type);
                if (imageReference.isActive)
                    active.Add(type.GetImageReferenceName());
            }
            return active;
        }

        /// <summary>
        /// Returns a bit mask representing active reference types.
        /// Each bit position corresponds to the integer value of the ImageReferenceType enum.
        /// </summary>
        public static int SelectActiveReferencesBitMask(this IState state, AssetReference asset)
        {
            var bitMask = 0;
            var generationSetting = state.SelectGenerationSetting(asset);

            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var typeValue = (int)type;
                if (typeValue >= 32)
                    throw new InvalidOperationException($"ImageReferenceType value {typeValue} ({type}) exceeds the maximum bit position (31) " + "that can be stored in an Int32. Consider using a long (64-bit) instead.");
                var imageReference = generationSetting.SelectImageReference(type);
                var isActiveReference = imageReference.SelectImageReferenceIsActive();
                if (isActiveReference)
                    bitMask |= 1 << typeValue;
            }

            return bitMask;
        }
        public static int SelectActiveReferencesBitMask(this IState state, VisualElement element) => state.SelectActiveReferencesBitMask(element.GetAsset());

        /// <summary>
        /// Returns a bit mask representing valid reference types with valid content.
        /// Each bit position corresponds to the integer value of the ImageReferenceType enum.
        /// </summary>
        public static int SelectValidReferencesBitMask(this IState state, AssetReference asset)
        {
            var bitMask = 0;
            var generationSetting = state.SelectGenerationSetting(asset);

            foreach (ImageReferenceType type in typeof(ImageReferenceType).GetEnumValues())
            {
                var typeValue = (int)type;
                if (typeValue >= 32)
                    throw new InvalidOperationException($"ImageReferenceType value {typeValue} ({type}) exceeds the maximum bit position (31) " + "that can be stored in an Int32. Consider using a long (64-bit) instead.");
                var imageReference = generationSetting.SelectImageReference(type);
                var isValidReference = imageReference.SelectImageReferenceIsValid();
                if (isValidReference)
                    bitMask |= 1 << typeValue;
            }

            return bitMask;
        }
        public static int SelectValidReferencesBitMask(this IState state, VisualElement element) => state.SelectValidReferencesBitMask(element.GetAsset());

        public static string SelectPendingPing(this IState state, VisualElement element) => state.SelectGenerationSetting(element).pendingPing;

        public static bool SelectAssetExists(this IState state, AssetReference asset) => asset.Exists();

        public static bool SelectAssetExists(this IState state, VisualElement element) => state.SelectAssetExists(element.GetAsset());

        public static GenerationValidationSettings SelectGenerationValidationSettings(this IState state, VisualElement element)
        {
            var asset = element.GetAsset();
            var settings = state.SelectGenerationSetting(asset);
            var prompt = string.IsNullOrWhiteSpace(settings.SelectPrompt());
            var negativePrompt = string.IsNullOrWhiteSpace(settings.SelectNegativePrompt());
            var model = state.SelectSelectedModelID(asset);
            var variations = settings.SelectVariationCount();
            var mode = settings.SelectRefinementMode();
            var dimensions = state.SelectImageDimensionsRaw(element);
            var activeReferencesBitMask = state.SelectActiveReferencesBitMask(element);
            var validReferencesBitMask = state.SelectValidReferencesBitMask(element);
            var baseImageBytesTimeStamp = state.SelectBaseImageBytesTimestamp(element);
            var modelsTimeStamp = ModelSelectorSelectors.SelectLastModelDiscoveryTimestamp(state);
            return new GenerationValidationSettings(asset: asset, valid: asset.Exists(), prompt: prompt, negativePrompt: negativePrompt, model: model,
                variations: variations, mode: mode, dimensions: dimensions, activeReferencesBitmask: activeReferencesBitMask,
                validReferencesBitmask: validReferencesBitMask, baseImageBytesTimeStampUtcTicks: baseImageBytesTimeStamp.lastWriteTimeUtcTicks,
                modelsSelectorTimeStampUtcTicks: modelsTimeStamp);
        }

        public static float SelectHistoryDrawerHeight(this IState state, VisualElement element) => state.SelectGenerationSetting(element).historyDrawerHeight;

        public static float SelectGenerationPaneWidth(this IState state, VisualElement element) => state.SelectGenerationSetting(element).generationPaneWidth;

        static bool ValidateReferenceCombination(GenerationSetting setting, ModelSettings model, int mask)
        {
            var mode = setting.SelectRefinementMode();
            var refs = setting.SelectImageReferencesByRefinement();
            bool IsActive(ImageReferenceType refType) => (mask & (1 << (int)refType)) != 0;

            bool? hasPrompt = null, hasStyle = null, hasComposition = null;
            bool? hasPose = null, hasDepth = null, hasLineArt = null, hasFeature = null;

            if (refs.TryGetValue(mode, out var modeRefs))
            {
                if (IsActive(ImageReferenceType.PromptImage) && modeRefs.ContainsKey(ImageReferenceType.PromptImage))
                    hasPrompt = true;
                if (IsActive(ImageReferenceType.StyleImage) && modeRefs.ContainsKey(ImageReferenceType.StyleImage))
                    hasStyle = true;
                if (IsActive(ImageReferenceType.CompositionImage) && modeRefs.ContainsKey(ImageReferenceType.CompositionImage))
                    hasComposition = true;
                if (IsActive(ImageReferenceType.PoseImage) && modeRefs.ContainsKey(ImageReferenceType.PoseImage))
                    hasPose = true;
                if (IsActive(ImageReferenceType.DepthImage) && modeRefs.ContainsKey(ImageReferenceType.DepthImage))
                    hasDepth = true;
                if (IsActive(ImageReferenceType.LineArtImage) && modeRefs.ContainsKey(ImageReferenceType.LineArtImage))
                    hasLineArt = true;
                if (IsActive(ImageReferenceType.FeatureImage) && modeRefs.ContainsKey(ImageReferenceType.FeatureImage))
                    hasFeature = true;
            }

            if (model.constants.Contains(ModelConstants.ModelCapabilities.SingleInputImage))
            {
                var referenceCount = 0;
                if (hasPrompt == true) referenceCount++;
                if (hasStyle == true) referenceCount++;
                if (hasComposition == true) referenceCount++;
                if (hasPose == true) referenceCount++;
                if (hasDepth == true) referenceCount++;
                if (hasLineArt == true) referenceCount++;
                if (hasFeature == true) referenceCount++;
                if (referenceCount > 1) return false;
            }

            return model.CanGenerateWithReferences(hasPrompt, hasStyle, hasComposition, hasPose, hasDepth, hasLineArt, hasFeature);
        }

        public static bool SelectIsReferenceExcess(this IState state, VisualElement element, ImageReferenceType typeToCheck)
        {
            var asset = element.GetAsset();
            var setting = state.SelectGenerationSetting(asset);
            var model = state.SelectSelectedModel(asset);
            var mode = setting.SelectRefinementMode();

            if (string.IsNullOrEmpty(model?.id) || !model.IsValid())
                return false;
            if (mode != RefinementMode.Generation && mode != RefinementMode.Spritesheet)
                return false;

            var fullMask = state.SelectActiveReferencesBitMask(asset);
            if (ValidateReferenceCombination(setting, model, fullMask))
                return false;

            var withoutMask = fullMask & ~(1 << (int)typeToCheck);
            return ValidateReferenceCombination(setting, model, withoutMask);
        }

        public static bool SelectCanAddReferencesToPrompt(this IState state, AddImageReferenceTypeData payload, ImageReferenceType typeToAdd)
        {
            var asset = new AssetReference { guid = payload.asset.guid };
            var setting = state.SelectGenerationSetting(asset);
            var model = state.SelectSelectedModel(asset);
            var mode = setting.SelectRefinementMode();

            if (string.IsNullOrEmpty(model?.id) || !model.IsValid())
                return false;
            if (model is { modality: ModelConstants.Modalities.Texture2d, provider: ModelConstants.Providers.Unity } &&
                typeToAdd == ImageReferenceType.PromptImage)
                return false;
            if (mode != RefinementMode.Generation && mode != RefinementMode.Spritesheet)
                return false;

            var testMask = state.SelectActiveReferencesBitMask(asset) | (1 << (int)typeToAdd);
            return ValidateReferenceCombination(setting, model, testMask);
        }

        public static async Task<GenerationMetadata> MakeMetadata(this IState state, AssetReference asset)
        {
            var generationSetting = state.SelectGenerationSetting(asset);
            var metadata = generationSetting.MakeMetadata(asset);
            switch (generationSetting.refinementMode)
            {
                case RefinementMode.RemoveBackground:
                case RefinementMode.Upscale:
                case RefinementMode.Pixelate:
                case RefinementMode.Recolor:
                    metadata.spriteSheet = await state.SelectIsAssetToRefineSpriteSheet(asset);
                    metadata.duration = await state.SelectIsAssetToRefineDuration(asset);
                    break;
                case RefinementMode.Spritesheet:
                    metadata.spriteSheet = true;
                    metadata.duration = state.SelectDuration(asset);
                    break;
            }

            return metadata;
        }
    }
}
