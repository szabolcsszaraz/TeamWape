
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Common.Responses.OperationResponses;
using AiEditorToolsSdk.Components.Common.Responses.Wrappers;
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
using Unity.AI.Pbr.Services.Stores.Actions.Payloads;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Pbr.Services.Stores.Selectors;
using Unity.AI.Pbr.Services.Stores.States;
using Unity.AI.Pbr.Services.Utilities;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services.Sdk;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Connect;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;

namespace Unity.AI.Pbr.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteMaterialsData> quoteMaterials = new($"{GenerationResultsActions.slice}/quoteMaterialsSuperProxy", QuoteMaterialsAsync);

        static async Task QuoteMaterialsAsync(QuoteMaterialsData arg, AsyncThunkApi<bool> api)
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

                var variations = arg.generationSetting.SelectVariationCount();
                var refinementMode = generationSetting.SelectRefinementMode();
                if (refinementMode is RefinementMode.Upscale or RefinementMode.Pbr)
                {
                    variations = 1;
                }

                var prompt = generationSetting.SelectPrompt();
                var negativePrompt = generationSetting.SelectNegativePrompt();
                var modelID = api.State.SelectSelectedModelID(asset);
                var model = api.State.SelectSelectedModel(asset);
                var modelParams = model?.paramsSchema?.Properties;
                var dimensions = generationSetting.SelectImageDimensionsVector2();
                var patternImageReference = generationSetting.SelectPatternImageReference();
                var seed = Random.Range(0, int.MaxValue - variations);
                var generativeModelID = modelID;
                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new PreCapturedAuthenticationTokenProvider(), traceIdProvider: new PreCapturedTraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout, packageInfoProvider: new PackageInfoProvider());
                var generateComponent = builder.GenerateComponentV2();

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

                var assetGuid = Guid.NewGuid();

                OperationResult<GenerateQuoteResultV2> quoteResults = null;

                if (string.IsNullOrEmpty(generativeModelID))
                {
                    var messages = new[] { "No model selected. Please select a valid model." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                switch (refinementMode)
                {
                    case RefinementMode.Upscale:
                    {
                        if (!api.State.HasReferenceAssetWithFallback(asset))
                        {
                            var messages = new[] { "No reference material available. Please generate a material first." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var assetKey = model?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.ReferenceImage);
                        var requestParams = new Dictionary<string, object>();
                        requestParams.Add(assetKey, assetGuid.ToString());
                        var scaleKey = modelParams.FindKeyBySemanticType(ModelConstants.SemanticTypes.ScaleFactor);
                        if (scaleKey != null)
                            requestParams.Add(scaleKey, modelParams.CoerceToSchemaType(scaleKey, 2));
                        var request = new GenerateRequestV2 { ModelId = generativeModelID, Capabilities = Selectors.Selectors.SelectRefinementCapabilities(refinementMode).ToList(), Params = requestParams };
                        quoteResults = await generateComponent.GenerateQuoteAsync(request, Constants.realtimeTimeout, linkedTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Pbr:
                    {
                        if (!api.State.HasReferenceAssetWithFallback(asset))
                        {
                            var messages = new[] { "No reference material available. Please generate a material first." };
                            api.Dispatch(GenerationActions.setGenerationValidationResult,
                                new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.Unknown, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                            return;
                        }

                        var assetKey = model?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.ReferenceImage);
                        var requestParams = new Dictionary<string, object>
                        {
                            { assetKey, assetGuid.ToString() }
                        };
                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Prompt, out _, out var promptKey))
                            requestParams.Add(promptKey, !string.IsNullOrEmpty(prompt) ? prompt : ModelConstants.SchemaKeys.DefaultPbrPrompt);
                        if (!string.IsNullOrEmpty(negativePrompt) && modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.NegativePrompt, out _, out var negPromptKey))
                            requestParams.Add(negPromptKey, negativePrompt);
                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Seed, out _, out var seedKey))
                            requestParams.Add(seedKey, modelParams.CoerceToSchemaType(seedKey, seed));
                        var request = new GenerateRequestV2 { ModelId = generativeModelID, Params = requestParams, Capabilities = Selectors.Selectors.SelectRefinementCapabilities(refinementMode).ToList() };
                        quoteResults = await generateComponent.GenerateQuoteAsync(request, Constants.realtimeTimeout, linkedTokenSource.Token);
                        break;
                    }
                    case RefinementMode.Generation:
                    {
                        var patternGuid = Guid.Empty;
                        if (patternImageReference.asset.IsValid())
                        {
                            patternGuid = Guid.NewGuid();
                        }

                        var requestParams = new Dictionary<string, object>
                        {
                            { ModelConstants.SchemaKeys.Prompt, prompt }
                        };

                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Dimensions, out _, out var dimensionsKey))
                            requestParams.Add(dimensionsKey, $"{dimensions.x}x{dimensions.y}");
                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Width, out _, out var widthKey))
                            requestParams.Add(widthKey, modelParams.CoerceToSchemaType(widthKey, dimensions.x));
                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Height, out _, out var heightKey))
                            requestParams.Add(heightKey, modelParams.CoerceToSchemaType(heightKey, dimensions.y));
                        if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Seed, out _, out var genSeedKey))
                            requestParams.Add(genSeedKey, modelParams.CoerceToSchemaType(genSeedKey, seed));

                        if (!string.IsNullOrEmpty(negativePrompt) && modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.NegativePrompt, out _, out var genNegPromptKey))
                            requestParams.Add(genNegPromptKey, negativePrompt);

                        if (patternGuid != Guid.Empty)
                        {
                            if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.CompositionReference, out _, out var compKey))
                            {
                                requestParams.Add(compKey, patternGuid.ToString());
                                var strengthKey = $"{compKey}_strength";
                                if (modelParams.SupportsParam(strengthKey))
                                    requestParams.Add(strengthKey, modelParams.CoerceToSchemaType(strengthKey, patternImageReference.strength));
                                if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.ControlMode, out var controlModeProp))
                                {
                                    var controlMode = controlModeProp.Enum?.Contains(ModelConstants.SchemaKeys.DefaultControlModeCanny) == true
                                        ? ModelConstants.SchemaKeys.DefaultControlModeCanny
                                        : ModelConstants.SchemaKeys.DefaultControlModeTile;
                                    requestParams.Add(ModelConstants.SchemaKeys.ControlMode, modelParams.CoerceToSchemaType(ModelConstants.SchemaKeys.ControlMode, controlMode));
                                }
                            }
                        }

                        var request = new GenerateRequestV2
                        {
                            ModelId = generativeModelID,
                            Capabilities = Selectors.Selectors.SelectRefinementCapabilities(refinementMode).ToList(),
                            Params = requestParams
                        };

                        quoteResults = await generateComponent.GenerateQuoteAsync(request, Constants.realtimeTimeout, linkedTokenSource.Token);
                        break;
                    }
                }

                if (cancellationTokenSource.IsCancellationRequested)
                {
                    api.DispatchValidatingMessage(arg.asset);
                    return;
                }

                if (quoteResults == null)
                {
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
                if (k_QuoteCancellationTokenSources.TryGetValue(arg.asset, out var storedTokenSource) && storedTokenSource == cancellationTokenSource)
                {
                    k_QuoteCancellationTokenSources.Remove(arg.asset);
                }

                cancellationTokenSource.Dispose();
            }
        }
    }
}
