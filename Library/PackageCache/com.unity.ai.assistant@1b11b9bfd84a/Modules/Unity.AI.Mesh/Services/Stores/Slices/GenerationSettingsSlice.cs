using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Mesh.Services.Stores.Actions;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UI.Actions;
using Unity.AI.Mesh.Services.Stores.Actions.Backend;
using Unity.AI.ModelSelector.Services.Stores.Actions;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Utility;
using UnityEngine;

namespace Unity.AI.Mesh.Services.Stores.Slices
{
    static class GenerationSettingsSlice
    {
        public static void Create(Store store) => store.CreateSlice(
            GenerationSettingsActions.slice,
            new GenerationSettings(),
            reducers => reducers
                .Add(GenerationActions.initializeAsset, (state, payload) =>
                {
                    if (payload == null || !payload.IsValid())
                        return;
                    state.generationSettings.Ensure(payload).EnsureSelectedModelID(store.State);
                })
                .Slice<GenerationSetting, IContext<AssetContext>>(
                    (state, action, slice) =>
                    {
                        if (action?.context?.asset == null) return;
                        var subState = state.generationSettings.Ensure(action.context.asset).EnsureSelectedModelID(store.State);
                        state.generationSettings[action.context.asset] = slice(subState);
                    },
                    reducers => reducers
                        .Add(GenerationSettingsActions.setGenerationPaneWidth, (state, payload) => state.generationPaneWidth = payload)
                        .Add(GenerationSettingsActions.setHistoryDrawerHeight, (state, payload) => state.historyDrawerHeight = payload)
                        .Add(GenerationSettingsActions.setSelectedModelID, (state, payload) => state.selectedModels.Ensure(payload.mode).modelID = payload.modelID)
                        .Add(GenerationSettingsActions.setPrompt, (state, payload) => state.prompt = payload)
                        .Add(GenerationSettingsActions.setNegativePrompt, (state, payload) => state.negativePrompt = payload)
                        .Add(GenerationSettingsActions.setVariationCount, (state, payload) => state.variationCount = payload)
                        .Add(GenerationSettingsActions.setUseCustomSeed, (state, payload) => state.useCustomSeed = payload)
                        .Add(GenerationSettingsActions.setCustomSeed, (state, payload) => state.customSeed = Math.Max(0, payload))
                        .Add(GenerationSettingsActions.setUseFaceLimit, (state, payload) => state.useFaceLimit = payload)
                        .Add(GenerationSettingsActions.setFaceLimit, (state, payload) => state.faceLimit = Math.Max(1, payload))
                        .Add(GenerationSettingsActions.setTargetFormat, (state, payload) => state.targetFormat = payload ?? "")
                        .Add(GenerationSettingsActions.setRefinementMode, (state, payload) => state.refinementMode = payload)
                        .Add(GenerationSettingsActions.setPromptImageReferenceAsset, (state, payload) => state.promptImageReference.asset = payload)
                        .Add(GenerationSettingsActions.setPromptImageReference, (state, payload) => state.promptImageReference = payload)
                        .Add(GenerationSettingsActions.clearImageReferences, (state, _) =>
                        {
                            state.promptImageReference = new GenerationSetting().promptImageReference;
                            state.multiviewImageReferences = MultiviewImageReferenceSettings.CreateDefaults();
                        })
                        .Add(GenerationSettingsActions.setMultiviewImageReference, (state, payload) =>
                        {
                            if (payload.index >= 0 && payload.index < state.multiviewImageReferences.Count)
                                state.multiviewImageReferences[payload.index].asset = payload.asset;
                        })
                        .Add(GenerationSettingsActions.clearMultiviewImageReferences, (state, _) => state.multiviewImageReferences = MultiviewImageReferenceSettings.CreateDefaults())
                        .Add(GenerationSettingsActions.setModelReferenceAsset, (state, payload) => state.modelReference.asset = payload)
                        .Add(GenerationSettingsActions.setModelReference, (state, payload) => state.modelReference = payload)
                        .Add(GenerationSettingsActions.setPivotMode, (state, payload) => state.meshSettings.pivotMode = payload)
                        .Add(GenerationSettingsActions.setMeshSettings, (state, payload) => { state.meshSettings.pivotMode = payload.pivotMode; })
                ),
            extraReducers => extraReducers
                .AddCase(AppActions.init).With((state, payload) =>
                {
                    var loaded = payload.payload.generationSettingsSlice with { };
                    foreach (var (_, setting) in loaded.generationSettings)
                        setting.variationCount = 1;
                    return loaded;
                })
                .AddCase(AppActions.deleteAsset).With((state, payload) =>
                {
                    if (state.generationSettings.ContainsKey(payload.payload))
                        state.generationSettings.Remove(payload.payload);
                    return state with { };
                })
                .AddCase(ModelSelectorActions.setLastModelDiscoveryTimestamp).With((state, _) =>
                {
                    state = state with { };
                    foreach (var (_, generationSetting) in state.generationSettings)
                    {
                        generationSetting.EnsureSelectedModelID(store.State);
                    }

                    return state;
                })
                .AddCase(ModelSelectorActions.discoverModels.Fulfilled, async (state, payload) =>
                {
                    var models = new List<ModelSettings>();
                    foreach (var service in GenerationServices.GetAllServices())
                    {
                        try
                        {
                            var modelsPerService = await service.GetModelsAsync();
                            if (modelsPerService is { Count: > 0 })
                                models.AddRange(modelsPerService);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Failed to get models from service {service.GetType().Name}: {e.Message}");
                        }
                    }

                    foreach (var model in models)
                    {
                        store.Dispatch(ModelSelectorActions.addCustomModel, model);
                    }

                    foreach (var (key, value) in state.generationSettings)
                    {
                        store.Dispatch(GenerationActions.initializeAsset, key);
                    }
                }),
            state => state with
            {
                generationSettings = new SerializableDictionary<AssetReference, GenerationSetting>(
                    state.generationSettings.ToDictionary(kvp => kvp.Key, entry => entry.Value with
                    {
                        selectedModels = new SerializableDictionary<RefinementMode, ModelSelection>(
                            entry.Value.selectedModels.ToDictionary(kvp => kvp.Key, kvp => kvp.Value with
                            {
                                modelID = kvp.Value.modelID
                            })),
                        prompt = entry.Value.prompt,
                        negativePrompt = entry.Value.negativePrompt,
                        variationCount = entry.Value.variationCount,
                        useCustomSeed = entry.Value.useCustomSeed,
                        customSeed = entry.Value.customSeed,
                        useFaceLimit = entry.Value.useFaceLimit,
                        faceLimit = entry.Value.faceLimit,
                        targetFormat = entry.Value.targetFormat,
                        refinementMode = entry.Value.refinementMode,
                        promptImageReference = entry.Value.promptImageReference with
                        {
                            asset = entry.Value.promptImageReference.asset
                        },
                        multiviewImageReferences = entry.Value.multiviewImageReferences
                            .Select(r => r with { asset = r.asset }).ToList(),
                        modelReference = entry.Value.modelReference with
                        {
                            asset = entry.Value.modelReference.asset
                        },
                        generationPaneWidth = entry.Value.generationPaneWidth,
                        historyDrawerHeight = entry.Value.historyDrawerHeight,
                    })
                )
            });
    }
}