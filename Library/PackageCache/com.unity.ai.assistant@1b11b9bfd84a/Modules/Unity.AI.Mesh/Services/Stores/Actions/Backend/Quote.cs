
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Requests;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Responses;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Mesh.Services.Stores.Actions.Payloads;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Mesh.Windows;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.ModelSelector.Services.Utilities;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services.Sdk;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Connect;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;
using WebUtils = Unity.AI.Mesh.Services.Utilities.WebUtils;

namespace Unity.AI.Mesh.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteMeshesData> quoteMeshes = new($"{GenerationResultsActions.slice}/quoteMeshesSuperProxy", QuoteMeshesAsync);

        static async Task QuoteMeshesAsync(QuoteMeshesData arg, AsyncThunkApi<bool> api)
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

                if (!asset.Exists() && !arg.allowInvalidAsset)
                {
                    var messages = new[] { "Selected asset is invalid. Please select a valid asset." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                using var httpClientLease = HttpClientManager.instance.AcquireLease();
                var generationSetting = arg.generationSetting;

                var variations = generationSetting.SelectVariationCount();
                var prompt = generationSetting.SelectPrompt();
                var model = api.State.SelectSelectedModel(asset);
                var modelParams = model?.paramsSchema?.Properties;
                var modelID = api.State.SelectSelectedModelID(asset);
                var mode = generationSetting.refinementMode;

                if (!GlTFastInstaller.IsInstalled && generationSetting.SelectRequiresGlTFast())
                {
                    var messages = new[] { "glTFast is required for this operation." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var imageReference = mode == RefinementMode.Retopology || mode == RefinementMode.Rigging
                    ? new PromptImageReference()
                    : generationSetting.SelectPromptImageReference();
                var multiviewRefs = generationSetting.SelectMultiviewImageReferences();
                var modelReference = generationSetting.SelectModelReference();

                var seed = Random.Range(0, int.MaxValue - variations);
                var generativeModelID = modelID;

                if (string.IsNullOrEmpty(generativeModelID) || !model.IsValid())
                {
                    var messages = new[] { "No model selected. Please select a valid model." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new PreCapturedAuthenticationTokenProvider(), traceIdProvider: new PreCapturedTraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout, packageInfoProvider: new PackageInfoProvider());
                var generateComponent = builder.GenerateComponentV2();

                var referenceImageGuid = Guid.Empty;
                if (imageReference.asset.IsValid())
                {
                    referenceImageGuid = Guid.NewGuid();
                }

                var supportsTextPrompt = model.operations.Contains(ModelConstants.Operations.TextPrompt);
                var supportsImagePrompt = model.operations.Contains(ModelConstants.Operations.ReferencePrompt);

                var hasTextPrompt = !string.IsNullOrWhiteSpace(prompt);
                var hasImagePrompt = referenceImageGuid != Guid.Empty;

                // Detect multiview model support
                var isMultiview = modelParams.SupportsParam(ModelConstants.SchemaKeys.ReferenceMultiviewFront);
                var multiviewGuids = new Dictionary<string, Guid>();
                if (isMultiview && multiviewRefs.HasAnyValidView())
                {
                    foreach (var (viewKey, _) in multiviewRefs.EnumerateValidViews())
                    {
                        multiviewGuids[viewKey] = Guid.NewGuid();
                    }
                }
                var hasMultiviewPrompt = multiviewGuids.Count > 0;

                if (isMultiview && hasMultiviewPrompt && multiviewGuids.Count < 2)
                {
                    var messages = new[] { "Multiview requires at least two reference images. Please provide the front reference plus at least one additional view." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var schema = model?.paramsSchema;
                var imageRefKey = schema.FindKeyBySemanticType(
                    ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.ReferenceImage);

                // FindKeyBySemanticType may return "reference_model" when "reference_image" is absent,
                // since both share the "asset-id" semantic type. Exclude model reference keys.
                if (imageRefKey == ModelConstants.SchemaKeys.ReferenceModel)
                    imageRefKey = null;

                // Validate unconditionally required fields (skip for multiview models that have multiview refs)
                if (imageRefKey != null && schema?.IsRequired(imageRefKey) == true && !hasImagePrompt && !hasMultiviewPrompt)
                {
                    var messages = new[] { "This model requires an image reference. Please provide a reference image." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                if (schema?.IsRequired(ModelConstants.SchemaKeys.ReferenceModel) == true && !modelReference.asset.IsValid())
                {
                    var messages = new[] { "This model requires a model reference. Please provide a reference model." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0,
                            messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                // Validate anyOf branches (e.g., "prompt OR reference_image")
                if (schema?.AnyOf is { Count: > 0 })
                {
                    var providedKeys = new HashSet<string>();
                    if (hasTextPrompt)
                    {
                        var promptKey = modelParams.FindFirstSupportedParam(ModelConstants.SchemaKeys.Prompt) ?? ModelConstants.SchemaKeys.Prompt;
                        providedKeys.Add(promptKey);
                    }
                    if (hasImagePrompt && imageRefKey != null)
                        providedKeys.Add(imageRefKey);
                    // Add multiview keys as provided
                    foreach (var viewKey in multiviewGuids.Keys)
                        providedKeys.Add(viewKey);
                    if (modelReference.asset.IsValid())
                        providedKeys.Add(ModelConstants.SchemaKeys.ReferenceModel);

                    if (!schema.IsAnyOfSatisfied(providedKeys))
                    {
                        var branchDescriptions = schema.AnyOf.Select(branch => string.Join(" and ", branch));
                        var message = $"This model requires at least one of: {string.Join(", or ", branchDescriptions)}.";
                        api.Dispatch(GenerationActions.setGenerationValidationResult,
                            new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0,
                                new List<GenerationFeedbackData> { new(message) })));
                        return;
                    }
                }

                var requestParams = new Dictionary<string, object>();

                if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Seed, out _, out var seedKey))
                    requestParams.Add(seedKey, modelParams.CoerceToSchemaType(seedKey, seed));

                if (supportsTextPrompt && hasTextPrompt)
                {
                    var promptKey = modelParams.FindFirstSupportedParam(ModelConstants.SchemaKeys.Prompt) ?? ModelConstants.SchemaKeys.Prompt;
                    requestParams.Add(promptKey, prompt);
                }
                else if (supportsTextPrompt && !hasImagePrompt && !hasMultiviewPrompt)
                {
                    // get the server side validation message for missing text prompt
                    var promptKey = modelParams.FindFirstSupportedParam(ModelConstants.SchemaKeys.Prompt) ?? ModelConstants.SchemaKeys.Prompt;
                    requestParams.Add(promptKey, string.Empty);
                }

                if (supportsImagePrompt && hasImagePrompt && !isMultiview)
                {
                    if (hasTextPrompt && modelParams.SupportsParam(ModelConstants.SchemaKeys.StyleReference))
                    {
                        requestParams.Add(ModelConstants.SchemaKeys.StyleReference, referenceImageGuid.ToString());
                    }
                    else
                    {
                        var refKey = model?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.ReferenceImage);
                        if (refKey != null)
                            requestParams.Add(refKey, referenceImageGuid.ToString());
                    }
                }

                // Add multiview reference GUIDs to request params
                foreach (var (viewKey, guid) in multiviewGuids)
                {
                    requestParams[viewKey] = guid.ToString();
                }

                var referenceModelGuid = Guid.Empty;
                if (modelReference.asset.IsValid())
                {
                    referenceModelGuid = Guid.NewGuid();
                }

                if (referenceModelGuid != Guid.Empty &&
                    modelParams.SupportsParam(ModelConstants.SchemaKeys.ReferenceModel))
                {
                    requestParams[ModelConstants.SchemaKeys.ReferenceModel] = referenceModelGuid.ToString();
                }

                var faceLimitKey = modelParams.FindFirstSupportedParam(ModelConstants.SchemaKeys.FaceLimit, ModelConstants.SchemaKeys.FaceCount);
                if (faceLimitKey != null && generationSetting.useFaceLimit)
                {
                    var faceLimitValue = generationSetting.SelectFaceLimit();
                    if (modelParams.TryGetValue(faceLimitKey, out var faceLimitProp))
                    {
                        var min = (int)(faceLimitProp.Minimum ?? 1);
                        var max = (int)(faceLimitProp.Maximum ?? int.MaxValue);
                        faceLimitValue = Math.Clamp(faceLimitValue, min, max);
                    }
                    requestParams.Add(faceLimitKey, modelParams.CoerceToSchemaType(faceLimitKey, faceLimitValue));
                }

                var targetFormatKey = modelParams.FindFirstSupportedParam(
                    ModelConstants.SchemaKeys.TargetFormat, ModelConstants.SchemaKeys.GeometryFileFormat);
                if (targetFormatKey != null)
                {
                    var formatValue = generationSetting.SelectTargetFormat();
                    if (!string.IsNullOrEmpty(formatValue))
                        requestParams.Add(targetFormatKey, modelParams.CoerceToSchemaType(targetFormatKey, formatValue));
                }

                var hasModelReference = referenceModelGuid != Guid.Empty;
                if (!supportsTextPrompt && !supportsImagePrompt && !hasModelReference)
                {
                    var messages = new[] { "Model does not support text or image prompts." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnsupportedModelOperation, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var request = new GenerateRequestV2
                {
                    ModelId = generativeModelID,
                    Capabilities = Selectors.Selectors.SelectRefinementCapabilities(mode).ToList(),
                    Params = requestParams
                };

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);
                var quoteResults = await generateComponent.GenerateQuoteAsync(request, Constants.realtimeTimeout, linkedTokenSource.Token);

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (!quoteResults.Result.IsSuccessful)
                {
                    var errorEnum = quoteResults.Result.Error.AiResponseError;
                    var messages = quoteResults.Result.Error.Errors.Count == 0
                        ? new[] { $"An error occurred during validation ({WebUtils.selectedEnvironment})." }
                        : quoteResults.Result.Error.Errors.Distinct().ToArray();

                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset,
                            new(quoteResults.Result.IsSuccessful, errorEnum.ToString(), 0,
                                messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var errEnum = !quoteResults.Result.IsSuccessful ? quoteResults.Result.Error.AiResponseError : AiResultErrorEnum.Unknown;
                var quoteValue = quoteResults.Result.Value;
                var pointsCost = quoteValue.PointsCost * variations;
                var worstCaseCost = quoteValue.WorstCasePointsCost * variations;

                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(quoteResults.Result.IsSuccessful,
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
