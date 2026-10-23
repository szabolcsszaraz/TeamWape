using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Requests;
using AiEditorToolsSdk.Components.Modalities.Generate.V2.Responses;
using Unity.AI.Animate.Services.Stores.Actions.Payloads;
using Unity.AI.Animate.Services.Stores.Selectors;
using Unity.AI.Animate.Services.Stores.States;
using Unity.AI.Animate.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Accounts.Services.Sdk;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Connect;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Random = UnityEngine.Random;

namespace Unity.AI.Animate.Services.Stores.Actions.Backend
{
    static class Quote
    {
        static readonly Dictionary<AssetReference, CancellationTokenSource> k_QuoteCancellationTokenSources = new();
        public static readonly AsyncThunkCreatorWithArg<QuoteAnimationsData> quoteAnimations =
            new($"{GenerationResultsActions.slice}/quoteAnimationsSuperProxy", QuoteAnimationsAsync);

        static async Task QuoteAnimationsAsync(QuoteAnimationsData arg, AsyncThunkApi<bool> api)
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

                var prompt = generationSetting.SelectPrompt();
                var modelID = generationSetting.SelectSelectedModelID();
                var model = api.State.SelectModelSettingsWithModelId(modelID);
                var modelParams = model?.paramsSchema?.Properties;
                var variations = generationSetting.SelectVariationCount();
                var seed = Random.Range(0, int.MaxValue - variations);
                var refinementMode = generationSetting.SelectRefinementMode();

                var generativeModelID = modelID;

                if (string.IsNullOrEmpty(generativeModelID))
                {
                    var messages = new[] { "No model selected. Please select a valid model." };
                    api.Dispatch(GenerationActions.setGenerationValidationResult,
                        new(arg.asset, new(false, BackendServiceConstants.ErrorTypes.UnknownModel, 0, messages.Select(m => new GenerationFeedbackData(m)).ToList())));
                    return;
                }

                var referenceVideoGuid = generationSetting.SelectVideoReference().asset.IsValid() ? Guid.NewGuid() : Guid.Empty;

                var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                    projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: WebUtils.selectedEnvironment, logger: new Logger(),
                    unityAuthenticationTokenProvider: new PreCapturedAuthenticationTokenProvider(), traceIdProvider: new PreCapturedTraceIdProvider(asset), enableDebugLogging: true,
                    defaultOperationTimeout: Constants.realtimeTimeout, packageInfoProvider: new PackageInfoProvider());
                var generateComponent = builder.GenerateComponentV2();

                // Create a linked token source that will be canceled if the original is canceled
                // but won't throw if the original is disposed
                using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationTokenSource.Token);

                var requestParams = new Dictionary<string, object>();

                if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Seed, out _, out var seedKey))
                    requestParams.Add(seedKey, modelParams.CoerceToSchemaType(seedKey, seed));

                if (refinementMode == RefinementMode.VideoToMotion)
                {
                    var videoRefKey = model?.paramsSchema.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId, ModelConstants.SchemaKeys.VideoReference)
                        ?? ModelConstants.SchemaKeys.MotionFrames;
                    requestParams.Add(videoRefKey, modelParams.BuildAssetIdValue(videoRefKey, referenceVideoGuid));
                }
                else if (refinementMode == RefinementMode.TextToMotion)
                {
                    if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Prompt, out _, out var promptKey))
                        requestParams.Add(promptKey, prompt);
                    else
                        requestParams.Add(ModelConstants.SchemaKeys.Prompt, prompt);

                    if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Length, out var lengthProp, out var lengthKey))
                    {
                        // UI is always in seconds; convert to the unit the model advertises (frames/seconds).
                        var lengthValue = ModelUnitConversion.SecondsToWire(lengthProp, generationSetting.SelectDuration());
                        requestParams.Add(lengthKey, modelParams.CoerceToSchemaType(lengthKey, lengthValue));
                    }
                    if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.CharacterId, out _, out var characterIdKey))
                        requestParams.Add(characterIdKey, modelParams.CoerceToSchemaType(characterIdKey, AnimationClipUtilities.bipedVersion));
                    if (modelParams.TryGetValueOrVariant(ModelConstants.SchemaKeys.Temperature, out _, out var temperatureKey))
                        requestParams.Add(temperatureKey, modelParams.CoerceToSchemaType(temperatureKey, 0f));
                }

                var request = new GenerateRequestV2
                {
                    ModelId = generativeModelID,
                    Capabilities = Selectors.Selectors.SelectRefinementCapabilities(refinementMode).ToList(),
                    Params = requestParams
                };

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

                var enumErr = !quoteResults.Result.IsSuccessful ? quoteResults.Result.Error.AiResponseError : AiResultErrorEnum.Unknown;
                var quoteValue = quoteResults.Result.Value;
                var pointsCost = quoteValue.PointsCost * variations;
                var worstCaseCost = quoteValue.WorstCasePointsCost * variations;

                api.Dispatch(GenerationActions.setGenerationValidationResult,
                    new(arg.asset,
                        new(quoteResults.Result.IsSuccessful,
                            enumErr.ToString(),
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
