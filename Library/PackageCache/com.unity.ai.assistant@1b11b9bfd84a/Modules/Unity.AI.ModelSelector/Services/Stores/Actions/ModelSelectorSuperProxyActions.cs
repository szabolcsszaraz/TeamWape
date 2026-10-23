using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AiEditorToolsSdk;
using AiEditorToolsSdk.Components.Common.Enums;
using AiEditorToolsSdk.Components.GenerativeModels.Requests;
using AiEditorToolsSdk.Components.GenerativeModels.Responses.ModelListV2;
using Unity.AI.Generators.IO.Utilities;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.Sdk;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.ModelSelector.Services.Stores.Selectors;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Connect;
using UnityEditor;
using UnityEngine;
using Constants = Unity.AI.Generators.Sdk.Constants;
using Logger = Unity.AI.Toolkit.Accounts.Services.Sdk.Logger;

namespace Unity.AI.ModelSelector.Services.Stores.Actions
{
    static class ModelSelectorSuperProxyActions
    {
        public static ModelSettings FromSuperProxy(ModelFullResult info, bool isFavorite)
        {
            var providerStr = info.Providers?.FirstOrDefault()?.ProviderId ?? ModelConstants.Providers.None;
            if (Enum.TryParse<ProviderEnum>(providerStr, true, out var parsedProvider))
                providerStr = parsedProvider.ToString();

            var modalityStr = info.Modality ?? ModelConstants.Modalities.None;
            if (Enum.TryParse<ModalityEnum>(modalityStr, true, out var parsedModality))
                modalityStr = parsedModality.ToString();
            modalityStr = modalityStr.Replace("Audio", "Sound"); // SDK inconsistency fix
            var model = new ModelSettings
            {
                id = info.ModelId,
                name = info.Name,
                tags = info.Tags ?? new List<string>(),
                description = info.Description,
                provider = providerStr,
                thumbnails = info.ThumbnailUrls ?? new List<string>(),
                icon = info.IconUrl,
                modality = modalityStr,
                status = info.Status,
                deprecationWarning = info.DeprecationWarning,
                replacementModelId = info.ReplacementModelId,
                minSdkVersion = info.MinSdkVersion,
                isFavorite = isFavorite,
                operations = new List<string>(),
                consumers = info.Consumers ?? new List<string>(),
                category = info.Category,
                capabilities = info.Capabilities ?? new List<string>()
            };

            // Parse ParamsSchema to extract image dimensions dynamically
            var sizes = new HashSet<ImageDimensions>();
            try
            {
                var schemaJson = info.ParamsSchema.ToString();
                var rawSchema = Newtonsoft.Json.Linq.JObject.Parse(schemaJson);

                var schema = new ModelParamsSchema { RawJson = schemaJson };
                if (rawSchema["required"] != null)
                    schema.Required = rawSchema["required"].ToObject<List<string>>();

                var anyOfToken = rawSchema["anyOf"];
                if (anyOfToken is Newtonsoft.Json.Linq.JArray anyOfArray)
                {
                    schema.AnyOf = new List<List<string>>();
                    foreach (var item in anyOfArray)
                    {
                        if (item is Newtonsoft.Json.Linq.JObject obj &&
                            obj["required"] is Newtonsoft.Json.Linq.JArray reqArray)
                        {
                            var branch = reqArray.Select(k => k?.ToString())
                                .Where(k => !string.IsNullOrEmpty(k)).ToList();
                            if (branch.Count > 0)
                                schema.AnyOf.Add(branch);
                        }
                    }
                }

                if (schema.AnyOf == null || schema.AnyOf.Count == 0)
                {
                    var oneOfToken = rawSchema["oneOf"];
                    if (oneOfToken is Newtonsoft.Json.Linq.JArray oneOfArray)
                    {
                        schema.AnyOf = new List<List<string>>();
                        foreach (var item in oneOfArray)
                        {
                            if (item is Newtonsoft.Json.Linq.JObject obj &&
                                obj["required"] is Newtonsoft.Json.Linq.JArray reqArray)
                            {
                                var branch = reqArray.Select(k => k?.ToString())
                                    .Where(k => !string.IsNullOrEmpty(k)).ToList();
                                if (branch.Count > 0)
                                    schema.AnyOf.Add(branch);
                            }
                        }
                    }
                }

                if (rawSchema["additionalProperties"] != null)
                    schema.AdditionalProperties = rawSchema["additionalProperties"].ToObject<bool>();

                var constraintsToken = rawSchema["x-cross-field-constraints"];
                if (constraintsToken is Newtonsoft.Json.Linq.JArray constraintsArray)
                    schema.CrossFieldConstraints = constraintsArray.ToObject<List<CrossFieldConstraint>>();

                var propertiesToken = rawSchema["properties"];
                if (propertiesToken != null && propertiesToken is Newtonsoft.Json.Linq.JObject propertiesObj)
                {
                    schema.Properties = new Dictionary<string, SchemaProperty>();
                    foreach (var prop in propertiesObj.Properties())
                    {
                        var schemaProp = prop.Value.ToObject<SchemaProperty>();
                        schema.Properties[prop.Name] = schemaProp;
                    }
                }

                model.paramsSchema = schema;
                
                if (model.capabilities.Contains(ModelConstants.ModelCapabilities.Recolor))
                    model.operations.Add(ModelConstants.Operations.RecolorReference);

                if (model.capabilities.Contains(ModelConstants.ModelCapabilities.PBR))
                    model.operations.Add(ModelConstants.Operations.Pbr);

                // Infer transformative operations from capabilities
                if (model.capabilities.Contains(ModelConstants.Operations.Upscale))
                    model.operations.Add(ModelConstants.Operations.Upscale);

                if (model.capabilities.Contains(ModelConstants.Operations.SkyboxUpscale))
                {
                    model.operations.Add(ModelConstants.Operations.SkyboxUpscale);
                    // Normalize the legacy "SkyboxUpscale" capability to the "Upscale" operation so a
                    // legacy-catalog skybox upscale model stays discoverable/auto-assignable, matching
                    // the renamed "Upscale" capability (proton FUTR-2617). Cubemap refinement filters on
                    // both operations and capabilities (AND-matched), and SelectRefinementOperations
                    // requires "Upscale".
                    model.operations.Add(ModelConstants.Operations.Upscale);
                }

                if (model.capabilities.Contains(ModelConstants.Operations.Pixelate))
                    model.operations.Add(ModelConstants.Operations.Pixelate);

                if (model.capabilities.Contains(ModelConstants.Operations.RemoveBackground))
                    model.operations.Add(ModelConstants.Operations.RemoveBackground);

                if (schema.Properties != null)
                {
                    // Dynamically infer operations from schema properties
                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.ReferenceImages))
                    {
                        model.operations.Add(ModelConstants.Operations.ReferencePrompt);
                        model.constants.Add(ModelConstants.ModelCapabilities.MultiReferenceImages);
                        model.referenceImagesParamKey = "reference_images";
                        if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.ReferenceImages, out var refImagesProp))
                            model.maxReferenceImages = refImagesProp.MaxItems ?? ModelConstants.ModelCapabilities.DefaultMaxReferenceImages;
                        else
                            model.maxReferenceImages = ModelConstants.ModelCapabilities.DefaultMaxReferenceImages;
                    }
                    else if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.Images))
                    {
                        model.operations.Add(ModelConstants.Operations.ReferencePrompt);
                        model.constants.Add(ModelConstants.ModelCapabilities.MultiReferenceImages);
                        model.referenceImagesParamKey = "images";

                        // For "images" layout, the max comes from a separate "max_images" integer param
                        if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.MaxImages, out var maxImagesProp))
                        {
                            model.maxReferenceImages = (int)(maxImagesProp.Maximum
                                ?? ModelConstants.ModelCapabilities.DefaultMaxReferenceImages);
                        }
                        else if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Images, out var imagesProp))
                        {
                            model.maxReferenceImages = imagesProp.MaxItems
                                ?? ModelConstants.ModelCapabilities.DefaultMaxReferenceImages;
                        }
                        else
                        {
                            model.maxReferenceImages = ModelConstants.ModelCapabilities.DefaultMaxReferenceImages;
                        }
                    }
                    else if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.ReferenceImage))
                    {
                        model.operations.Add(ModelConstants.Operations.ReferencePrompt);
                        model.maxReferenceImages = 1;
                    }
                    else if (schema.Properties.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId) != null)
                    {
                        model.operations.Add(ModelConstants.Operations.ReferencePrompt);
                        model.maxReferenceImages = 1;
                    }

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.CompositionReference))
                        model.operations.Add(ModelConstants.Operations.CompositionReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.StyleReference))
                        model.operations.Add(ModelConstants.Operations.StyleReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.PoseReference))
                        model.operations.Add(ModelConstants.Operations.PoseReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.DepthReference))
                        model.operations.Add(ModelConstants.Operations.DepthReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.LineArtReference))
                        model.operations.Add(ModelConstants.Operations.LineArtReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.FeatureReference))
                        model.operations.Add(ModelConstants.Operations.FeatureReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.MaskReference))
                        model.operations.Add(ModelConstants.Operations.MaskReference);

                    if (schema.Properties.ContainsKeyOrVariant(ModelConstants.SchemaKeys.RecolorReference))
                        model.operations.Add(ModelConstants.Operations.RecolorReference);

                    if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Dimensions, out var dimProp, out var dimKey) && dimProp.Enum != null)
                    {
                        model.sizingMode = dimKey;
                        foreach (var token in dimProp.Enum)
                        {
                            var parts = token?.ToString().Split('x');
                            if (parts != null && parts.Length == 2 && int.TryParse(parts[0], out var w) && int.TryParse(parts[1], out var h))
                            {
                                sizes.Add(new ImageDimensions { width = w, height = h });
                            }
                        }
                    }
                    else if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Width, out var wProp, out var wKey) && wProp.Enum != null &&
                             schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Height, out var hProp, out var hKey) && hProp.Enum != null)
                    {
                        model.sizingMode = $"{wKey}_{hKey}";
                        var widths = System.Linq.Enumerable.Select(wProp.Enum, Convert.ToInt32).ToList();
                        var heights = System.Linq.Enumerable.Select(hProp.Enum, Convert.ToInt32).ToList();
                        foreach (var w in widths)
                        {
                            foreach (var h in heights)
                            {
                                sizes.Add(new ImageDimensions { width = w, height = h });
                            }
                        }
                    }
                    else if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Width, out wProp, out wKey) &&
                             schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.Height, out hProp, out hKey) &&
                             wProp.Type?.ToString().Contains("integer") == true &&
                             hProp.Type?.ToString().Contains("integer") == true)
                    {
                        model.sizingMode = $"{wKey}_{hKey}";
                        model.constants.Add(ModelConstants.ModelCapabilities.CustomResolutions);

                        var minW = wProp.Minimum ?? ModelConstants.ModelCapabilities.CustomResolutionsMin;
                        var maxW = wProp.Maximum ?? ModelConstants.ModelCapabilities.CustomResolutionsMax;
                        var minH = hProp.Minimum ?? ModelConstants.ModelCapabilities.CustomResolutionsMin;
                        var maxH = hProp.Maximum ?? ModelConstants.ModelCapabilities.CustomResolutionsMax;

                        // Add default sizes from the valid range, excluding any that violate
                        // cross-field constraints (e.g. GPT Image 2's minimum total pixel count).
                        var defaultSizes = new[] { 512, 768, 1024, 1280, 1536 };
                        foreach (var s in defaultSizes)
                        {
                            if (s >= minW && s <= maxW && s >= minH && s <= maxH && schema.IsWidthHeightValid(s, s))
                                sizes.Add(new ImageDimensions { width = s, height = s });
                        }
                    }
                    else if (schema.Properties.TryGetValueOrVariant(ModelConstants.SchemaKeys.AspectRatio, out var arProp, out var arKey) && arProp.Enum != null)
                    {
                        model.sizingMode = arKey;
                        var bases = new[] { 1024, 2048 };
                        foreach (var token in arProp.Enum)
                        {
                            var strToken = token?.ToString();
                            if (!string.IsNullOrEmpty(strToken) && !model.aspectRatios.Contains(strToken))
                                model.aspectRatios.Add(strToken);

                            var parts = strToken?.Split(':');
                            if (parts != null && parts.Length == 2 && float.TryParse(parts[0], out var rw) && float.TryParse(parts[1], out var rh))
                            {
                                foreach (var b in bases)
                                {
                                    var maxR = Math.Max(rw, rh);
                                    var w = (int)Math.Round(b * (rw / maxR));
                                    var h = (int)Math.Round(b * (rh / maxR));
                                    // Ensure dimensions are multiples of 64 as typically expected by generators
                                    w = (w / 64) * 64;
                                    h = (h / 64) * 64;
                                    if (w > 0 && h > 0)
                                    {
                                        sizes.Add(new ImageDimensions { width = w, height = h });
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse ParamsSchema for model {info.ModelId}: {e.Message}");
            }

            // Parse UiSchema
            try
            {
                if (info.UiSchema != null)
                {
                    var uiSchemaJson = info.UiSchema.ToString();
                    var rawUiSchema = Newtonsoft.Json.Linq.JObject.Parse(uiSchemaJson);

                    var uiSchema = new ModelUiSchema { RawJson = uiSchemaJson };
                    if (rawUiSchema[ModelConstants.SchemaKeys.UiOrder] != null)
                        uiSchema.Order = rawUiSchema[ModelConstants.SchemaKeys.UiOrder].ToObject<List<string>>();
                    if (rawUiSchema[ModelConstants.SchemaKeys.UiGroups] != null)
                        uiSchema.Groups = rawUiSchema[ModelConstants.SchemaKeys.UiGroups].ToObject<List<UiGroup>>();

                    uiSchema.FieldSchemas = new Dictionary<string, Newtonsoft.Json.Linq.JToken>();
                    foreach (var prop in rawUiSchema.Properties())
                    {
                        if (prop.Name == ModelConstants.SchemaKeys.UiOrder || prop.Name == ModelConstants.SchemaKeys.UiGroups)
                            continue;

                        uiSchema.FieldSchemas[prop.Name] = prop.Value;
                    }

                    model.uiSchema = uiSchema;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to parse UiSchema for model {info.ModelId}: {e.Message}");
            }

            var sizeList = sizes.ToList();
            if (sizeList.Count == 0)
            {
                // Fallback safe default
                sizeList.Add(new ImageDimensions { width = 1024, height = 1024 });
            }

            model.imageSizes = sizeList.OrderBy(dim => dim.GetSquarenessFactor()).ToList();
            model.nativeResolution = model.imageSizes.FirstOrDefault() ?? new ImageDimensions { width = 1024, height = 1024 };
            model.aspectRatios = model.aspectRatios.OrderBy(ar => ar.GetSquarenessFactor()).ToList();

            var nameLower = (model.name ?? string.Empty).ToLowerInvariant();

            if (info.Tags != null)
            {
                model.constants.AddRange(info.Tags);
            }

            // Infer TextPrompt operation from schema rather than blanket-adding it to all modalities,
            // so that transform-only models (e.g. Skybox Upscale) don't appear in the Generate tab.
            if (model.SupportsParam(ModelConstants.SchemaKeys.Prompt)
                && !model.operations.Contains(ModelConstants.Operations.TextPrompt))
            {
                model.operations.Add(ModelConstants.Operations.TextPrompt);
            }

            if (model.modality == ModelConstants.Modalities.Video)
            {
                var props = model.paramsSchema?.Properties;
                var hasFirstFrame = model.operations.Contains(ModelConstants.Operations.ReferencePrompt)
                    || (props != null && (props.ContainsKeyOrVariant(ModelConstants.SchemaKeys.StartImage)
                        || props.ContainsKeyOrVariant(ModelConstants.SchemaKeys.ImageUrlSnake)
                        || props.ContainsKeyOrVariant("image")));
                var hasLastFrame = hasFirstFrame
                    && (props != null && (props.ContainsKeyOrVariant(ModelConstants.SchemaKeys.EndImage)
                        || props.ContainsKeyOrVariant(ModelConstants.SchemaKeys.LastFrameSnake)
                        || props.ContainsKeyOrVariant(ModelConstants.SchemaKeys.LastFrameImageSnake)));

                if (hasFirstFrame && !model.operations.Contains(ModelConstants.Operations.FirstFrameReference))
                    model.operations.Add(ModelConstants.Operations.FirstFrameReference);
                if (hasLastFrame && !model.operations.Contains(ModelConstants.Operations.LastFrameReference))
                    model.operations.Add(ModelConstants.Operations.LastFrameReference);
            }

            // Infer EditWithPrompt from schema: model supports both text-only and text+image
            // (i.e., image reference is optional, not required)
            if (model.operations.Contains(ModelConstants.Operations.TextPrompt) &&
                model.operations.Contains(ModelConstants.Operations.ReferencePrompt))
            {
                var imageRefKey = model.paramsSchema?.Properties?.FindKeyBySemanticType(ModelConstants.SemanticTypes.AssetId);
                var imageRefRequired = imageRefKey != null && model.paramsSchema?.IsRequired(imageRefKey) == true;
                if (!imageRefRequired)
                    model.constants.Add(ModelConstants.ModelCapabilities.EditWithPrompt);
            }

            if (model.modality == ModelConstants.Modalities.Skybox)
                model.constants.Add(ModelConstants.ModelCapabilities.SingleInputImage);

            // Infer SupportsLooping from schema: model has a "loop" parameter
            if (model.paramsSchema?.Properties?.ContainsKeyOrVariant(ModelConstants.SchemaKeys.Loop) == true)
                model.constants.Add(ModelConstants.ModelCapabilities.SupportsLooping);

            // TODO: move to backend tag "9slice" — once the backend ships the tag,
            // it will flow into model.constants automatically via info.Tags and this fallback can be removed.
            if (nameLower.StartsWith(k_GameUIStartsWith))
                model.constants.Add(ModelConstants.ModelCapabilities.Supports9SliceUI);

            // TODO: move Requires300PxReference to backend schema or tag — the 300px minimum
            // isn't in the backend schema yet.
            if (nameLower.StartsWith(k_KlingStartsWith))
            {
                model.limitations.Add(ModelConstants.ModelLimitations.Requires300PxReference);
            }

            if (model.thumbnails.Count == 0 && !string.IsNullOrWhiteSpace(model.icon))
                model.thumbnails = new List<string> { model.icon };

            return model;
        }

        // TODO: move to backend tag "9slice" — remove once backend ships the tag
        const string k_GameUIStartsWith = "game ui elements (flux)";
        // TODO: move Requires300PxReference to backend schema or tag — remove once backend ships constraint metadata
        const string k_KlingStartsWith = "kling";

        // While fetching models is safe to do concurrently, we use this mutex as a best-effort
        // mechanism to minimize redundant API calls. A short timeout allows requests to proceed
        // in extreme cases, and frequent cache checks within the method will often prevent
        // redundant work even if the semaphore wait times out.
        static readonly SemaphoreSlim k_Mutex = new(1, 1);

        public static readonly AsyncThunkCreator<DiscoverModelsData, List<ModelSettings>> fetchModels = new($"{ModelSelectorActions.slice}/fetchModelsSuperProxy", async (data, api) =>
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Fetching models from backend.");

            var taskID = ProgressUtility.Start($"Requesting models.");
            using var progressTokenSource = new CancellationTokenSource();
            List<ModelSettings> models = new();
            var semaphoreAcquired = false;

            try
            {
                // Check cache first
                if (ModelsCache.IsValid(data.environment))
                    return ModelsCache.models;

                const int millisecondsTimeout = 2000;
                semaphoreAcquired = await k_Mutex.WaitAsync(millisecondsTimeout).ConfigureAwaitMainThread();

                // Check cache again after waiting for the mutex
                if (ModelsCache.IsValid(data.environment))
                    return ModelsCache.models;

                SetProgress(0.0f, "Authenticating with UnityConnect.");
                if (!WebUtilities.AreCloudProjectSettingsValid())
                {
                    LogInvalidCloudProjectSettings();
                    api.Cancel();
                    return models;
                }

                // Check cache again after potentially long authentication
                if (ModelsCache.IsValid(data.environment))
                    return ModelsCache.models;

                SetProgress(0.1f, "Preparing request.");

                {
                    var logger = new Logger();

                    using var httpClientLease = HttpClientManager.instance.AcquireLease();
                    var timeout = Constants.modelsFetchTimeout;

                    var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                        projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: data.environment, logger: logger,
                        unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), enableDebugLogging: true, defaultOperationTimeout: timeout, packageInfoProvider: new PackageInfoProvider());
                    var generativeModelsComponentV2 = builder.GenerativeModelsComponentV2();

                    using var timeoutTokenSource = new CancellationTokenSource(timeout);

                    SetProgress(0.2f, "Requesting model list.");

                    var modelResults = await generativeModelsComponentV2.GetModelsAsync(timeoutOverride: timeout, cancellationToken: timeoutTokenSource.Token);

                    // Check cache again after potentially long request
                    if (ModelsCache.IsValid(data.environment))
                        return ModelsCache.models;

                    SetProgress(0.4f, "Finishing model list request.");
                    if (timeoutTokenSource.IsCancellationRequested)
                        throw new OperationCanceledException();

                    SetProgress(0.5f, "Validating model list.");

                    if (!modelResults.Result.IsSuccessful)
                    {
                        if (modelResults.Result.Error.Errors.Count == 0)
                            Debug.Log($"Error reason is '{modelResults.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({data.environment}).");
                        else
                            modelResults.Result.Error.Errors.ForEach(e => Debug.Log($"{modelResults.Result.Error.AiResponseError.ToString()}: {e}"));

                        // we can simply return without throwing or additional logging because the error is already logged
                        return models;
                    }

                    SetProgress(0.6f, "Requesting model favorites.");

                    var favoritesResults = await generativeModelsComponentV2.GetFavoritesAsync(timeoutOverride: timeout, cancellationToken: timeoutTokenSource.Token);
                    SetProgress(0.7f, "Finishing model favorites request.");
                    if (timeoutTokenSource.IsCancellationRequested)
                        throw new OperationCanceledException();

                    SetProgress(0.8f, "Validating model favorites.");

                    var favoritesSuccessful = favoritesResults.Result.IsSuccessful;
                    if (!favoritesSuccessful)
                    {
                        if (favoritesResults.Result.Error.Errors.Count == 0)
                            Debug.Log($"Error reason is '{favoritesResults.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({data.environment}).");
                        else
                        {
                            if (modelResults.Result.Error.AiResponseError is AiResultErrorEnum.None or AiResultErrorEnum.Unknown && logger.LastException != null)
                                Debug.LogException(logger.LastException);
                            favoritesResults.Result.Error.Errors.ForEach(e => Debug.Log($"{favoritesResults.Result.Error.AiResponseError.ToString()}: {e}"));
                        }
                    }
                    SetProgress(0.9f, "Parsing model list.");

                    var currentVersion = ParseVersionIgnoringSuffix(new PackageInfoProvider().PackageVersion);

                    foreach (var modelResult in modelResults.Result.Value.Models)
                    {
                        // Filter out non-active models as requested in the acceptance criteria
                        if (!string.Equals(modelResult.Status, "active", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        if (!IsModelCompatibleWithSdkVersion(modelResult.MinSdkVersion, currentVersion))
                        {
                            continue;
                        }

                        // We might need to check if there is an equivalent for `modelResult.ModelType != ModelTypeEnum.Base` in V2
                        // In V2, models are usually just active/inactive, base models might not be returned in summary, or they are filtered by tags.
                        // For now, let's assume all models returned in summary are valid if they are active, and have a modality.
                        if (!string.IsNullOrEmpty(modelResult.Modality))
                        {
                            var isUserFavorite = favoritesSuccessful && favoritesResults.Result.Value
                                .Any(f => string.Equals(f.GenerativeModelId, modelResult.ModelId, StringComparison.OrdinalIgnoreCase));
                            models.Add(FromSuperProxy(modelResult, isUserFavorite));
                        }
                    }

                    // Update cache only if both operations were successful
                    if (favoritesSuccessful)
                        ModelsCache.UpdateCache(models, data.environment);
                }
            }
            finally
            {
                if (semaphoreAcquired)
                    k_Mutex.Release();

                progressTokenSource.Cancel();
                SetProgress(1, models.Count > 0 ? $"Retrieved {models.Count} models." : "Failed to retrieve models.");
                ProgressUtility.Finish(taskID);
            }

            return models;

            void SetProgress(float progress, string description)
            {
                if (taskID > 0)
                    Progress.Report(taskID, progress, description);
            }

            void LogInvalidCloudProjectSettings() =>
                Debug.Log($"Error reason is 'Invalid Unity Cloud configuration': Could not obtain organizations for user \"{UnityConnectProvider.userName}\".");
        });

        public static readonly AsyncThunkCreator<(FavoriteModelPayload,string), bool> setModelFavorite = new($"{ModelSelectorActions.slice}/setModelFavoriteSuperProxy", async (arg, api) =>
        {
            var (payload, environment) = arg;

            if (string.IsNullOrEmpty(payload.modelId))
                return false;

            using var httpClientLease = HttpClientManager.instance.AcquireLease();
            var builder = Builder.Build(orgId: UnityConnectProvider.organizationKey, userId: UnityConnectProvider.userId,
                projectId: UnityConnectProvider.projectId, httpClient: httpClientLease.client, baseUrl: environment, logger: new Logger(),
                unityAuthenticationTokenProvider: new AuthenticationTokenProvider(), enableDebugLogging: true, packageInfoProvider: new PackageInfoProvider());
            var generativeModelsComponentV2 = builder.GenerativeModelsComponentV2();
            var res = await generativeModelsComponentV2.UpdateFavoriteAsync(new GenerativeModelsFavoritesRequest
            {
                GenerativeModelId = payload.modelId,
                ModelOperation = payload.isFavorite ? ModelOperationEnum.Favorite : ModelOperationEnum.Unfavorite
            });
            if (!res.Result.IsSuccessful)
            {
                if (res.Result.Error.Errors.Count == 0)
                {
                    if (Unsupported.IsDeveloperMode())
                        Debug.Log($"Error reason is '{res.Result.Error.AiResponseError.ToString()}' and no additional error information was provided ({environment}).");
                }
                else
                    res.Result.Error.Errors.ForEach(e => Debug.Log($"{res.Result.Error.AiResponseError.ToString()}: {e}"));
            }

            return res.Result.IsSuccessful;
        });

        internal static Version ParseVersionIgnoringSuffix(string versionString)
        {
            if (string.IsNullOrEmpty(versionString))
                return null;

            var versionParts = versionString.Split('-');
            return Version.TryParse(versionParts[0], out var version) ? version : null;
        }

        internal static bool IsModelCompatibleWithSdkVersion(string modelMinSdkVersion, Version currentVersion)
        {
            if (string.IsNullOrEmpty(modelMinSdkVersion) || currentVersion == null)
                return true;

            if (!Version.TryParse(modelMinSdkVersion, out var minVersion))
                return true;

            return currentVersion >= minVersion;
        }
    }

    static class ImageDimensionsExtensions
    {
        public static double GetSquarenessFactor(this ImageDimensions dimensions)
        {
            if (dimensions.width == 0 || dimensions.height == 0)
                return double.MaxValue;

            double w = dimensions.width;
            double h = dimensions.height;

            // Calculate the ratio such that it's always >= 1
            // For example, 100x200 (ratio 0.5) and 200x100 (ratio 2.0)
            // both become 2.0 using this method. A 100x100 square becomes 1.0.
            return Math.Max(w / h, h / w);
        }

        public static double GetSquarenessFactor(this string aspectRatio)
        {
            if (string.IsNullOrEmpty(aspectRatio))
                return double.MaxValue;

            var parts = aspectRatio.Split(':');
            if (parts.Length == 2 && double.TryParse(parts[0], out var w) && double.TryParse(parts[1], out var h) && w != 0 && h != 0)
            {
                return Math.Max(w / h, h / w);
            }

            return double.MaxValue;
        }
    }

    static class SchemaPropertiesExtensions
    {
        public static bool SupportsParam(this Dictionary<string, SchemaProperty> properties, string key)
        {
            if (properties == null)
                return false;
            return properties.ContainsKeyOrVariant(key);
        }

        /// <summary>
        /// Like SupportsParam, but understands the compound `width_height` sizing mode form:
        /// the model schema doesn't have a literal `width_height` property, it has separate
        /// `width` and `height` properties. Returns true if the model supports the requested
        /// sizing mode (single-key for `dimensions`/`aspect_ratio`, or both keys for `width_height`).
        /// </summary>
        public static bool SupportsSizingMode(this Dictionary<string, SchemaProperty> properties, string sizingMode)
        {
            if (properties == null || string.IsNullOrEmpty(sizingMode))
                return false;
            if (ModelConstants.SchemaKeys.IsSizingModeWidthHeight(sizingMode) && sizingMode.Contains('_'))
            {
                var parts = sizingMode.Split('_');
                return parts.Length == 2 && properties.SupportsParam(parts[0]) && properties.SupportsParam(parts[1]);
            }
            return properties.SupportsParam(sizingMode);
        }

        public static bool ContainsKeyOrVariant(this Dictionary<string, SchemaProperty> properties, string key)
        {
            if (properties.ContainsKey(key))
                return true;
            var variant = ModelConstants.SchemaKeys.GetVariant(key);
            return variant != null && properties.ContainsKey(variant);
        }

        public static bool TryGetValueOrVariant(this Dictionary<string, SchemaProperty> properties, string key, out SchemaProperty value)
        {
            if (properties == null)
            {
                value = null;
                return false;
            }
            if (properties.TryGetValue(key, out value))
                return true;
            var variant = ModelConstants.SchemaKeys.GetVariant(key);
            return variant != null && properties.TryGetValue(variant, out value);
        }

        public static bool TryGetValueOrVariant(this Dictionary<string, SchemaProperty> properties, string key, out SchemaProperty value, out string foundKey)
        {
            if (properties == null)
            {
                value = null;
                foundKey = null;
                return false;
            }
            if (properties.TryGetValue(key, out value))
            {
                foundKey = key;
                return true;
            }
            var variant = ModelConstants.SchemaKeys.GetVariant(key);
            if (variant != null && properties.TryGetValue(variant, out value))
            {
                foundKey = variant;
                return true;
            }
            foundKey = null;
            return false;
        }

        public static string FindKeyBySemanticType(this Dictionary<string, SchemaProperty> properties, string semanticType)
        {
            if (properties == null)
                return null;
            foreach (var kvp in properties)
            {
                if (kvp.Value.SemanticType == semanticType)
                    return kvp.Key;
                if (kvp.Value.Items?.SemanticType == semanticType)
                    return kvp.Key;
                if (kvp.Value.Properties != null)
                {
                    foreach (var nested in kvp.Value.Properties)
                    {
                        if (nested.Value.SemanticType == semanticType)
                            return kvp.Key;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Finds a property key by semantic type, preferring the defaultKey when it matches,
        /// then required keys, then any property with the matching semantic type.
        /// </summary>
        public static string FindKeyBySemanticType(this ModelParamsSchema schema, string semanticType, string defaultKey = null)
        {
            if (schema?.Properties == null)
                return defaultKey;

            // First: if the caller hinted at a specific key and it matches, prefer it
            if (defaultKey != null && schema.Properties.TryGetValue(defaultKey, out var defaultProp) &&
                (defaultProp.SemanticType == semanticType || defaultProp.Items?.SemanticType == semanticType
                    || defaultProp.Properties?.Values.Any(p => p.SemanticType == semanticType) == true))
                return defaultKey;

            // Second: look for a required key with this semantic type
            if (schema.Required is { Count: > 0 })
            {
                foreach (var requiredKey in schema.Required)
                {
                    if (schema.Properties.TryGetValue(requiredKey, out var prop) &&
                        (prop.SemanticType == semanticType || prop.Items?.SemanticType == semanticType
                            || prop.Properties?.Values.Any(p => p.SemanticType == semanticType) == true))
                        return requiredKey;
                }
            }

            // Third: fall back to any property with this semantic type
            var found = schema.Properties.FindKeyBySemanticType(semanticType);
            return found ?? defaultKey;
        }

        public static string FindFirstSupportedParam(this Dictionary<string, SchemaProperty> properties, params string[] candidates)
        {
            if (properties == null)
                return null;
            foreach (var candidate in candidates)
            {
                if (properties.SupportsParam(candidate))
                    return candidate;
            }
            return null;
        }

        /// <summary>
        /// Builds the appropriate value for an asset-id parameter.
        /// For nested object schemas (e.g., video_reference: { asset_id: guid }),
        /// returns a Dictionary. For flat schemas, returns the guid as a string.
        /// </summary>
        public static object BuildAssetIdValue(this Dictionary<string, SchemaProperty> properties, string key, Guid guid)
        {
            if (properties != null && properties.TryGetValue(key, out var prop) && prop.Properties != null)
            {
                foreach (var nested in prop.Properties)
                {
                    if (nested.Value.SemanticType == ModelConstants.SemanticTypes.AssetId)
                        return new Dictionary<string, object> { { nested.Key, guid.ToString() } };
                }
            }

            return guid.ToString();
        }

        public static object CoerceToSchemaType(this Dictionary<string, SchemaProperty> properties, string key, object value)
        {
            if (properties == null || !properties.TryGetValue(key, out var prop))
                return value;

            // If schema has enum values, pick the best match
            if (prop.Enum is { Count: > 0 })
            {
                // Try exact string match first
                var valueStr = value?.ToString();
                foreach (var enumVal in prop.Enum)
                {
                    if (string.Equals(enumVal?.ToString(), valueStr, System.StringComparison.OrdinalIgnoreCase))
                        return enumVal;
                }

                // Try numeric proximity match
                if (value != null && double.TryParse(valueStr, out var numericValue))
                {
                    object closest = null;
                    var closestDist = double.MaxValue;
                    foreach (var enumVal in prop.Enum)
                    {
                        if (enumVal == null)
                            continue;
                        if (TryParseNumericWithSuffix(enumVal.ToString(), out var enumNum))
                        {
                            var dist = System.Math.Abs(enumNum - numericValue);
                            if (dist < closestDist)
                            {
                                closestDist = dist;
                                closest = enumVal;
                            }
                        }
                    }
                    if (closest != null)
                        return closest;
                }

                // Fall back to default or first enum value
                return prop.Default ?? prop.Enum[0];
            }

            var schemaType = prop.Type?.ToString()?.ToLowerInvariant() ?? "";
            if (schemaType.Contains("string"))
            {
                var strValue = value?.ToString();
                // If the input is numeric but the schema expects a string with a default,
                // prefer the default (e.g., upscale_factor "4k" vs numeric 2)
                if (prop.Default != null && value != null && double.TryParse(strValue, out _))
                    return prop.Default;
                return strValue;
            }
            if (schemaType.Contains("integer") && value != null && double.TryParse(value.ToString(), out var intVal))
                return (int)intVal;
            if (schemaType.Contains("number") && value != null && double.TryParse(value.ToString(), out var numVal))
                return numVal;
            if (schemaType.Contains("boolean") && value != null && bool.TryParse(value.ToString(), out var boolVal))
                return boolVal;

            // If we can't coerce and there's a default, use it
            return prop.Default ?? value;
        }

        static bool TryParseNumericWithSuffix(string s, out double result)
        {
            if (double.TryParse(s, out result))
                return true;

            if (s != null && s.Length > 1
                && char.ToUpperInvariant(s[^1]) == 'K'
                && double.TryParse(s[..^1], out var baseVal))
            {
                result = baseVal * 1024;
                return true;
            }

            result = 0;
            return false;
        }

        /// <summary>
        /// Parses the Enum values of a SchemaProperty as floats, returning them sorted ascending.
        /// Returns null if the property has no enum values or none are valid floats.
        /// </summary>
        public static float[] GetAllowedFloatValues(this SchemaProperty property)
        {
            if (property?.Enum is not { Count: > 0 })
                return null;

            var values = property.Enum
                .Select(v => float.TryParse(v.ToString(),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var f) ? f : (float?)null)
                .Where(v => v.HasValue)
                .Select(v => v.Value)
                .OrderBy(v => v)
                .ToArray();

            return values.Length > 0 ? values : null;
        }

        /// <summary>
        /// Returns true if the schema property type is "integer".
        /// </summary>
        public static bool IsIntegerType(this SchemaProperty property)
        {
            return property?.Type?.ToString()?.ToLowerInvariant().Contains("integer") == true;
        }

        /// <summary>
        /// Snaps a value to the nearest value in the allowed array.
        /// </summary>
        public static float SnapToNearest(float value, float[] allowed)
        {
            var closest = allowed[0];
            var minDist = Mathf.Abs(value - closest);
            for (var i = 1; i < allowed.Length; i++)
            {
                var dist = Mathf.Abs(value - allowed[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = allowed[i];
                }
            }
            return closest;
        }

        /// <summary>
        /// For each known creativity key supported by the model, adds the schema minimum value to the request params.
        /// </summary>
        public static void AddMinCreativityParams(this Dictionary<string, SchemaProperty> properties, Dictionary<string, object> requestParams)
        {
            if (properties == null)
                return;

            foreach (var key in ModelConstants.SchemaKeys.CreativityKeys)
            {
                if (!properties.SupportsParam(key))
                    continue;

                if (properties.TryGetValue(key, out var prop) && prop.Minimum.HasValue)
                    requestParams[key] = properties.CoerceToSchemaType(key, prop.Minimum.Value);
            }
        }
    }

    static class ModelsCache
    {
        public static List<ModelSettings> models { get; private set; }
        public static DateTime cacheTimestamp { get; private set; }
        public static string environment { get; private set; }

        public static bool IsValid(string env)
        {
            if (models == null || environment != env)
                return false;

            var cacheAge = DateTime.Now - cacheTimestamp;
            return cacheAge.TotalSeconds <= ModelSelectorSelectors.timeToLiveGlobally;
        }

        public static void UpdateCache(IEnumerable<ModelSettings> currentModels, string env)
        {
            models = new List<ModelSettings>(currentModels);
            environment = env;
            cacheTimestamp = DateTime.Now;
        }
    }
}
