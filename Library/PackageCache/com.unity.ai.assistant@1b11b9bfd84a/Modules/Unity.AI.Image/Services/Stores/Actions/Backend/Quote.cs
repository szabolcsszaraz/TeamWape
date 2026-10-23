
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Requests;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Responses;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Stores.Actions.Payloads;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Stores.States;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Connect;
using UnityEngine;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Toolkit.Accounts.Services.Sdk.Logger;

namespace Unity.AI.Image.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteImagesData> quoteImages = new($"{GenerationResultsActions.slice}/quoteImagesSuperProxy", QuoteImagesAsync);

        static async Task QuoteImagesAsync(QuoteImagesData arg, AsyncThunkApi<bool> api)
        {
            if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var existingTokenSource))
            {
                existingTokenSource.Cancel();
                existingTokenSource.Dispose();
            }

            var cancellationTokenSource = new CancellationTokenSource();
            k_QuoteCancellationTokenSources[arg.asset] = cancellationTokenSource;

            try
            {
                api.DispatchValidatingUserMessage(arg.asset);

                var success = await WebUtilities.WaitForCloudProjectSettings(arg.asset);

                api.DispatchValidatingMessage(arg.asset);

                if (cancellationTokenSource.IsCancellationRequested)
                    return;

                if (!success)
                {
                    var messages = new[]
                    {
                        $"Invalid Unity Cloud configuration. Could not obtain organizations for user \"{UnityConnectProvider.userName}\"."
                    };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var asset = new AssetReference { guid = arg.asset.guid };

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (!arg.allowInvalidAsset && !asset.Exists())
                {
                    var messages = new[] { "Selected asset is invalid. Please select a valid asset." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                using var httpClientLease = HttpClientManager.instance.AcquireLease();
                var generationSetting = arg.generationSetting;

                var variations = generationSetting.SelectVariationCount();
                var refinementMode = generationSetting.SelectRefinementMode();
                if (refinementMode is RefinementMode.RemoveBackground or RefinementMode.Upscale or RefinementMode.Recolor or RefinementMode.Pixelate or RefinementMode.Spritesheet)
                {
                    variations = 1;
                }

                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var model = api.State.SelectSelectedModel(asset);
                var modelParams = model?.paramsSchema?.Properties;
                var dimensions = generationSetting.SelectImageDimensionsVector2();
                var sizingMode = api.State.SelectModelSizingMode(asset);
                var aspectRatio = api.State.SelectAspectRatioRaw(asset);
                if (string.IsNullOrEmpty(aspectRatio)) aspectRatio = "1:1";

                if (api.State.SelectIsCustomResolutionInvalid(asset))
                {
                    var (minW, maxW, minH, maxH) = api.State.SelectCustomResolutionRange(asset);
                    var rangeText = minW == minH && maxW == maxH
                        ? $"between {minW} and {maxW} pixels"
                        : $"between {minW}x{minH} and {maxW}x{maxH} pixels";
                    var messages = new[] { $"Invalid image dimensions. Width and height must be {rangeText}." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var crossFieldErrors = api.State.SelectCrossFieldValidationErrors(asset);
                if (crossFieldErrors.Count > 0)
                {
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, crossFieldErrors.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var upscaleFactor = generationSetting.SelectUpscaleFactor();
                var imageReferences = generationSetting.SelectImageReferencesByRefinement();

                var pixelateTargetSize = generationSetting.pixelateSettings.targetSize;
                var pixelateResizeToTargetSize = !generationSetting.pixelateSettings.keepImageSize;
                var pixelatePixelBlockSize = generationSetting.pixelateSettings.pixelBlockSize;
                var pixelatePixelGridSize = generationSetting.pixelateSettings.pixelGridSize;
                var pixelateMode = (int)generationSetting.pixelateSettings.mode;
                var pixelateOutlineThickness = generationSetting.SelectPixelateOutlineThickness();

                var builderLogger = new Logger();

                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: builderLogger,
                    unityAuthenticationTokenProvider: new PreCapturedAuthenticationTokenProvider(), traceIdProvider: new PreCapturedTraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout, packageInfoProvider: new PackageInfoProvider());
                var generateComponentV2 = builder.GenerateComponentV2();

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

                var generativeModelID = modelID;

                OperationResult<GenerateQuoteResultV2> quoteResultsV2 = null;

                var assetGuid = asset.IsValid() ? Guid.NewGuid() : Guid.Empty;

                var requestParams = new Dictionary<string, object>();
                var targetModelId = generativeModelID;
                var effectiveModel = model;
                List<string> requestCapabilities = null;

                switch (refinementMode)
                {
                    case RefinementMode.Pixelate:
                    {
                        if (!await api.State.SelectHasAssetToRefine(asset))
                        {
                            var messages = new[] { "No image selected. Please select an image to pixelate." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        targetModelId = api.State.SelectModelForCapability(ModelConstants.Operations.Pixelate);
                        effectiveModel = api.State.SelectModelById(targetModelId);
                        requestCapabilities = new List<string> { ModelConstants.Operations.Pixelate };
                        var pixelateParams = effectiveModel?.paramsSchema?.Properties;
                        var pixelateAssetKey = effectiveModel?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.AssetId);
                        requestParams.Add(pixelateAssetKey, assetGuid.ToString());
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.PixelGridSize))
                            requestParams.Add(ModelConstants.SchemaKeys.PixelGridSize, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.PixelGridSize, pixelatePixelGridSize));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.RemoveNoise))
                            requestParams.Add(ModelConstants.SchemaKeys.RemoveNoise, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.RemoveNoise, false));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.ResizeToTargetSize))
                            requestParams.Add(ModelConstants.SchemaKeys.ResizeToTargetSize, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.ResizeToTargetSize, pixelateResizeToTargetSize));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.TargetSize))
                            requestParams.Add(ModelConstants.SchemaKeys.TargetSize, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.TargetSize, pixelateTargetSize));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.PixelBlockSize))
                            requestParams.Add(ModelConstants.SchemaKeys.PixelBlockSize, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.PixelBlockSize, pixelatePixelBlockSize));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.Mode))
                            requestParams.Add(ModelConstants.SchemaKeys.Mode, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.Mode, pixelateMode));
                        if (pixelateParams.SupportsParam(ModelConstants.SchemaKeys.OutlineThickness))
                            requestParams.Add(ModelConstants.SchemaKeys.OutlineThickness, pixelateParams.CoerceToSchemaType(ModelConstants.SchemaKeys.OutlineThickness, pixelateOutlineThickness));
                        break;
                    }
                    case RefinementMode.RemoveBackground:
                    {
                        if (!await api.State.SelectHasAssetToRefine(asset))
                        {
                            var messages = new[] { "No image selected. Please select an image to remove background from." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        targetModelId = api.State.SelectModelForCapability(ModelConstants.Operations.RemoveBackground);
                        effectiveModel = api.State.SelectModelById(targetModelId);
                        requestCapabilities = new List<string> { ModelConstants.Operations.RemoveBackground };
                        var removeBgParams = effectiveModel?.paramsSchema?.Properties;
                        var removeBgAssetKey = effectiveModel?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.AssetId);
                        requestParams.Add(removeBgAssetKey, assetGuid.ToString());
                        break;
                    }
                    case RefinementMode.Upscale:
                    {
                        if (!await api.State.SelectHasAssetToRefine(asset))
                        {
                            var messages = new[] { "No image selected. Please select an image to upscale." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        string upscaleCapability;
                        if (asset.IsCubemap())
                        {
                            // Cubemap upscale runs on the Skybox-modality upscale model. Resolve it by
                            // modality + capability so it keeps working whether the catalog advertises the
                            // legacy "SkyboxUpscale" or the renamed "Upscale" capability (proton FUTR-2617),
                            // and send whichever the resolved model advertises so we don't depend on the
                            // server's deprecated back-compat alias.
                            targetModelId = api.State.SelectSkyboxUpscaleModel();
                            effectiveModel = api.State.SelectModelById(targetModelId);
                            upscaleCapability = effectiveModel.capabilities.Contains(ModelConstants.Operations.Upscale)
                                ? ModelConstants.Operations.Upscale
                                : ModelConstants.Operations.SkyboxUpscale;
                        }
                        else
                        {
                            upscaleCapability = ModelConstants.Operations.Upscale;
                            targetModelId = api.State.SelectModelForCapability(upscaleCapability);
                            effectiveModel = api.State.SelectModelById(targetModelId);
                        }
                        requestCapabilities = new List<string> { upscaleCapability };
                        var upscaleParams = effectiveModel?.paramsSchema?.Properties;
                        var upscaleAssetKey = effectiveModel?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.AssetId);
                        requestParams.Add(upscaleAssetKey, assetGuid.ToString());
                        var scaleKey = upscaleParams.FindKeyBySemanticType(ModelConstants.SemanticTypes.ScaleFactor);
                        if (scaleKey != null)
                            requestParams.Add(scaleKey, upscaleParams.CoerceToSchemaType(scaleKey, upscaleFactor));
                        upscaleParams.AddMinCreativityParams(requestParams);
                        break;
                    }
                    case RefinementMode.Recolor:
                    {
                        var recolorModelID = api.State.SelectRecolorModel();
                        targetModelId = recolorModelID;
                        effectiveModel = api.State.SelectModelById(targetModelId);
                        requestCapabilities = new List<string> { "Recolor" };
                        var recolorParams = effectiveModel?.paramsSchema?.Properties;

                        if (!string.IsNullOrEmpty(prompt) && recolorParams.SupportsParam(ModelConstants.SchemaKeys.Prompt))
                            requestParams.Add(ModelConstants.SchemaKeys.Prompt, prompt);

                        if (recolorParams.SupportsSizingMode(sizingMode))
                        {
                            if (ModelConstants.SchemaKeys.IsSizingModeAspectRatio(sizingMode))
                                requestParams.Add(sizingMode, aspectRatio);
                            else if (ModelConstants.SchemaKeys.IsSizingModeWidthHeight(sizingMode))
                            {
                                var whParts = sizingMode.Split('_');
                                requestParams.Add(whParts[0], recolorParams.CoerceToSchemaType(whParts[0], dimensions.x));
                                requestParams.Add(whParts[1], recolorParams.CoerceToSchemaType(whParts[1], dimensions.y));
                            }
                            else
                                requestParams.Add(sizingMode, $"{dimensions.x}x{dimensions.y}");
                        }

                        if (!string.IsNullOrEmpty(negativePrompt) && recolorParams.SupportsParam(ModelConstants.SchemaKeys.NegativePrompt))
                            requestParams.Add(ModelConstants.SchemaKeys.NegativePrompt, negativePrompt);

                        if (!await api.State.SelectHasAssetToRefine(asset))
                        {
                            var messages = new[] { "No image selected. Please select an image to recolor." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }
                        var paletteImageReference = imageReferences[refinementMode][ImageReferenceType.PaletteImage];
                        var paletteAssetGuid = paletteImageReference.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty;
                        if (paletteAssetGuid == Guid.Empty)
                        {
                            var messages = new[] { "Invalid palette image. Please select a valid palette image." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }
                        var recolorAssetKey = effectiveModel?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.AssetId);
                        requestParams.Add(recolorAssetKey, assetGuid.ToString());
                        if (recolorParams.SupportsParam(ModelConstants.SchemaKeys.RecolorReference))
                            requestParams.Add(ModelConstants.SchemaKeys.RecolorReference, paletteAssetGuid.ToString());
                        if (recolorParams.SupportsParam(ModelConstants.SchemaKeys.ColorPaletteReference))
                            requestParams.Add(ModelConstants.SchemaKeys.ColorPaletteReference, paletteAssetGuid.ToString());
                        break;
                    }
                    case RefinementMode.Spritesheet:
                    {
                        if (modelParams == null || modelParams.SupportsParam(ModelConstants.SchemaKeys.Prompt))
                            requestParams.Add(ModelConstants.SchemaKeys.Prompt, prompt ?? "");

                        if (modelParams.SupportsSizingMode(sizingMode))
                        {
                            if (ModelConstants.SchemaKeys.IsSizingModeAspectRatio(sizingMode))
                                requestParams.Add(sizingMode, aspectRatio);
                            else if (ModelConstants.SchemaKeys.IsSizingModeWidthHeight(sizingMode))
                            {
                                var whParts = sizingMode.Split('_');
                                requestParams.Add(whParts[0], modelParams.CoerceToSchemaType(whParts[0], dimensions.x));
                                requestParams.Add(whParts[1], modelParams.CoerceToSchemaType(whParts[1], dimensions.y));
                            }
                            else
                                requestParams.Add(sizingMode, $"{dimensions.x}x{dimensions.y}");
                        }

                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Resolution, out _, out var resolutionKey) && !requestParams.ContainsKey(resolutionKey))
                            requestParams.Add(resolutionKey, modelParams.CoerceToSchemaType(resolutionKey, Mathf.Max(dimensions.x, dimensions.y)));

                        if (!string.IsNullOrEmpty(negativePrompt) && modelParams.SupportsParam(ModelConstants.SchemaKeys.NegativePrompt))
                            requestParams.Add(ModelConstants.SchemaKeys.NegativePrompt, negativePrompt);

                        if (string.IsNullOrEmpty(targetModelId))
                        {
                            var messages = new[] { "No model selected. Please select a valid model." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var firstImageReference = imageReferences[refinementMode][ImageReferenceType.FirstImage];
                        var firstAssetGuid = firstImageReference.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty;

                        if (firstAssetGuid == Guid.Empty)
                        {
                            var messages = new[] { "Invalid first image. Please select a valid first image." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        if (firstImageReference.SelectImageReferenceIsSpriteSheet())
                        {
                            var messages = new[] { "Cannot make a spritesheet of a spritesheet. Please select a different image." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var lastImageReference = imageReferences[refinementMode][ImageReferenceType.LastImage];
                        var lastAssetGuid = lastImageReference.SelectImageReferenceIsValid() ? Guid.NewGuid() : (Guid?)null;

                        if (modelParams.SupportsParam(ModelConstants.SchemaKeys.Duration))
                            requestParams.Add(ModelConstants.SchemaKeys.Duration, modelParams.CoerceToSchemaType(ModelConstants.SchemaKeys.Duration, Mathf.RoundToInt(api.State.SelectDuration(asset))));
                        {
                            var firstFrameKey = modelParams.FindFirstSupportedParam(
                                ModelConstants.SchemaKeys.FirstFrameReference, ModelConstants.SchemaKeys.StartImage,
                                ModelConstants.SchemaKeys.ImageUrlSnake, ModelConstants.SchemaKeys.Image) ?? ModelConstants.SchemaKeys.FirstFrameReference;
                            requestParams.Add(firstFrameKey, firstAssetGuid.ToString());
                        }
                        if (lastAssetGuid.HasValue)
                        {
                            var lastFrameKey = modelParams.FindFirstSupportedParam(
                                ModelConstants.SchemaKeys.LastFrameReference, ModelConstants.SchemaKeys.EndImage,
                                ModelConstants.SchemaKeys.LastFrameSnake, ModelConstants.SchemaKeys.LastFrameImageSnake) ?? ModelConstants.SchemaKeys.LastFrameReference;
                            requestParams.Add(lastFrameKey, lastAssetGuid.Value.ToString());
                        }
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        if (modelParams == null || modelParams.SupportsParam(ModelConstants.SchemaKeys.Prompt))
                            requestParams.Add(ModelConstants.SchemaKeys.Prompt, prompt ?? "");

                        if (modelParams.SupportsSizingMode(sizingMode))
                        {
                            if (ModelConstants.SchemaKeys.IsSizingModeAspectRatio(sizingMode))
                                requestParams.Add(sizingMode, aspectRatio);
                            else if (ModelConstants.SchemaKeys.IsSizingModeWidthHeight(sizingMode))
                            {
                                var whParts = sizingMode.Split('_');
                                requestParams.Add(whParts[0], modelParams.CoerceToSchemaType(whParts[0], dimensions.x));
                                requestParams.Add(whParts[1], modelParams.CoerceToSchemaType(whParts[1], dimensions.y));
                            }
                            else
                                requestParams.Add(sizingMode, $"{dimensions.x}x{dimensions.y}");
                        }

                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Resolution, out _, out var resolutionKey) && !requestParams.ContainsKey(resolutionKey))
                            requestParams.Add(resolutionKey, modelParams.CoerceToSchemaType(resolutionKey, Mathf.Max(dimensions.x, dimensions.y)));

                        if (!string.IsNullOrEmpty(negativePrompt) && modelParams.SupportsParam(ModelConstants.SchemaKeys.NegativePrompt))
                            requestParams.Add(ModelConstants.SchemaKeys.NegativePrompt, negativePrompt);

                        if (string.IsNullOrEmpty(targetModelId))
                        {
                            var messages = new[] { "No model selected. Please select a valid model." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var unlabeledReferences = generationSetting.SelectUnlabeledImageReferences();
                        var validUnlabeledCount = unlabeledReferences?.Count(r => r.SelectImageReferenceIsValid()) ?? 0;

                        var supportsMultiRef = model?.constants.Contains(ModelConstants.ModelCapabilities.MultiReferenceImages) == true;
                        if (validUnlabeledCount > 0 && supportsMultiRef)
                        {
                            var refImagesArray = new List<string>();

                            // Include the labeled PromptImage if it's valid
                            var promptImageReference = imageReferences[refinementMode].GetValueOrDefault(ImageReferenceType.PromptImage);
                            if (promptImageReference != null && promptImageReference.SelectImageReferenceIsValid())
                                refImagesArray.Add(Guid.NewGuid().ToString());

                            refImagesArray.AddRange(Enumerable.Range(0, validUnlabeledCount)
                                .Select(_ => Guid.NewGuid().ToString()));
                            var refKey = model?.referenceImagesParamKey ?? "reference_images";
                            requestParams.Add(refKey, refImagesArray);
                        }
                        else if (model?.referenceImagesParamKey != null && modelParams.SupportsParam(model.referenceImagesParamKey) && !modelParams.SupportsParam(ModelConstants.SchemaKeys.ReferenceImage))
                        {
                            // Model uses array-based image references (e.g., Seedream "images")
                            // Collect any labeled references into the array key
                            var referenceGuids = imageReferences[refinementMode]
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty);

                            var refImagesArray = new List<string>();
                            foreach (var kvp in referenceGuids)
                            {
                                if (kvp.Value != Guid.Empty)
                                    refImagesArray.Add(kvp.Value.ToString());
                            }

                            if (refImagesArray.Count > 0)
                            {
                                var refKey = model.referenceImagesParamKey;
                                requestParams.Add(refKey, refImagesArray);
                            }
                        }
                        else
                        {
                            var referenceGuids = imageReferences[refinementMode]
                                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.SelectImageReferenceIsValid() ? Guid.NewGuid() : Guid.Empty);

                            var map = new Dictionary<ImageReferenceType, string> {
                                { ImageReferenceType.PromptImage, ModelConstants.SchemaKeys.ReferenceImage },
                                { ImageReferenceType.StyleImage, ModelConstants.SchemaKeys.StyleReference },
                                { ImageReferenceType.CompositionImage, ModelConstants.SchemaKeys.CompositionReference },
                                { ImageReferenceType.PoseImage, ModelConstants.SchemaKeys.PoseReference },
                                { ImageReferenceType.DepthImage, ModelConstants.SchemaKeys.DepthReference },
                                { ImageReferenceType.LineArtImage, ModelConstants.SchemaKeys.LineArtReference },
                                { ImageReferenceType.FeatureImage, ModelConstants.SchemaKeys.FeatureReference }
                            };
                            foreach (var kvp in map)
                            {
                                if (!referenceGuids.TryGetValue(kvp.Key, out var refGuid) || refGuid == Guid.Empty)
                                    continue;

                                var paramKey = kvp.Value;
                                if (!modelParams.SupportsParam(paramKey))
                                {
                                    if (kvp.Key == ImageReferenceType.PromptImage)
                                        paramKey = modelParams.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId);
                                    if (paramKey == null)
                                        continue;
                                }

                                requestParams.Add(paramKey, refGuid.ToString());
                                if (modelParams.SupportsParam($"{paramKey}_strength"))
                                    requestParams.Add($"{paramKey}_strength", modelParams.CoerceToSchemaType($"{paramKey}_strength", imageReferences[refinementMode][kvp.Key].strength));
                            }
                        }
                        break;
                    }
                }

                // Add dynamic model parameters (e.g. quality, background for GPT Image 2).
                // Applied after the switch so every refinement mode includes them, and resolved
                // against the effective model's schema (which may differ from the default model
                // for operations like Recolor that use a different target model ID).
                var dynamicParamsSchema = effectiveModel?.paramsSchema?.Properties;
                if (dynamicParamsSchema != null)
                {
                    var dynamicParams = generationSetting.SelectDynamicParams();
                    if (dynamicParams != null)
                    {
                        foreach (var kvp in dynamicParams)
                        {
                            if (!requestParams.ContainsKey(kvp.Key) && dynamicParamsSchema.SupportsParam(kvp.Key))
                                requestParams[kvp.Key] = dynamicParamsSchema.CoerceToSchemaType(kvp.Key, kvp.Value);
                        }
                    }
                }

                var requestV2 = new GenerateRequestV2
                {
                    ModelId = targetModelId,
                    Capabilities = requestCapabilities ?? Selectors.Selectors.SelectRefinementCapabilities(refinementMode, asset).ToList(),
                    Params = requestParams
                };

                quoteResultsV2 = await generateComponentV2.GenerateQuoteAsync(requestV2, Constants.realtimeTimeout, linkedTokenSource.Token);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (quoteResultsV2 == null)
                {
                    return;
                }

                var isSuccessful = quoteResultsV2.Result.IsSuccessful;

                if (!isSuccessful)
                {
                    var error = quoteResultsV2.Result.Error;
                    string[] messages;
                    var errorEnum = error?.AiResponseError ?? AiResultErrorEnum.Unknown;
                    if (error == null || error.Errors.Count == 0)
                    {
                        if (errorEnum == AiResultErrorEnum.Unknown)
                        {
                            var baseMessage = $"The endpoint at ({WebUtils.selectedEnvironment}) returned an {errorEnum} message.";
                            if (builderLogger.LastException != null)
                            {
                                var exceptionMessage = $"Encountered an exception of type '{builderLogger.LastException.GetType().Name}' from '{builderLogger.LastException.Source}'.\nDetails: {builderLogger.LastException.Message}";
                                messages = new[] { $"{baseMessage}\n{exceptionMessage}" };
                            }
                            else
                            {
                                messages = new[] { baseMessage };
                            }
                        }
                        else
                        {
                            messages = new[] { $"An error ({errorEnum}) occurred during validation ({WebUtils.selectedEnvironment})." };
                        }
                    }
                    else
                    {
                        messages = error.Errors.Distinct().ToArray();
                    }

                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(isSuccessful, errorEnum.ToString(), 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var errEnum = AiResultErrorEnum.Unknown;
                var quoteValue = quoteResultsV2.Result.Value;
                var pointsCost = quoteValue.PointsCost * variations;
                var worstCaseCost = quoteValue.WorstCasePointsCost * variations;

                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(isSuccessful,
                            errEnum.ToString(),
                            pointsCost,
                            new List<GenerationFeedbackData>(),
                            new QuotePricingDetails(worstCaseCost, quoteValue.FlatPricing, quoteValue.ProviderName))));
            }
            finally
            {
                // Only dispose if this is still the current token source for this asset
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                {
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                }

                cancellationTokenSource.Dispose();
            }
        }
    }
}
