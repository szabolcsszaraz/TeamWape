using System;
using Unity.AI.Generators.Asset;
using Unity.AI.Mesh.Services.Stores.Actions.Creators;
using Unity.AI.Mesh.Services.Stores.Actions.Payloads;
using Unity.AI.Mesh.Services.Stores.States;
using Unity.AI.Mesh.Services.Stores.Selectors;
using Unity.AI.Mesh.Services.Utilities;
using Unity.AI.Mesh.Windows;
using Unity.AI.ModelSelector.Windows;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UI.Payloads;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit.Asset;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Services.Stores.Actions
{
    static class GenerationSettingsActions
    {
        public static readonly string slice = "generationSettings";

        public static AssetActionCreator<(RefinementMode mode, string modelID)> setSelectedModelID => new($"{slice}/{nameof(setSelectedModelID)}");
        public static AssetActionCreator<string> setPrompt => new($"{slice}/{nameof(setPrompt)}");
        public static AssetActionCreator<string> setNegativePrompt => new($"{slice}/{nameof(setNegativePrompt)}");
        public static AssetActionCreator<int> setVariationCount => new($"{slice}/{nameof(setVariationCount)}");
        public static AssetActionCreator<bool> setUseCustomSeed => new($"{slice}/{nameof(setUseCustomSeed)}");
        public static AssetActionCreator<int> setCustomSeed => new($"{slice}/{nameof(setCustomSeed)}");
        public static AssetActionCreator<bool> setUseFaceLimit => new($"{slice}/{nameof(setUseFaceLimit)}");
        public static AssetActionCreator<int> setFaceLimit => new($"{slice}/{nameof(setFaceLimit)}");
        public static AssetActionCreator<string> setTargetFormat => new($"{slice}/{nameof(setTargetFormat)}");
        public static AssetActionCreator<RefinementMode> setRefinementMode => new($"{slice}/{nameof(setRefinementMode)}");

        public static AssetActionCreator<AssetReference> setPromptImageReferenceAsset => new($"{slice}/{nameof(setPromptImageReferenceAsset)}");
        public static AssetActionCreator<PromptImageReference> setPromptImageReference => new($"{slice}/{nameof(setPromptImageReference)}");
        public static AssetActionCreator<ImageReferenceClearAllData> clearImageReferences => new($"{slice}/{nameof(clearImageReferences)}");

        public static AssetActionCreator<(int index, AssetReference asset)> setMultiviewImageReference => new($"{slice}/{nameof(setMultiviewImageReference)}");
        public static AssetActionCreator<bool> clearMultiviewImageReferences => new($"{slice}/{nameof(clearMultiviewImageReferences)}");

        public static AssetActionCreator<AssetReference> setModelReferenceAsset => new($"{slice}/{nameof(setModelReferenceAsset)}");
        public static AssetActionCreator<ModelReference> setModelReference => new($"{slice}/{nameof(setModelReference)}");

        public static readonly AsyncThunkCreatorWithArg<VisualElement> openSelectModelPanel = new($"{slice}/{nameof(openSelectModelPanel)}", async (element, api) =>
        {
            var selectedModelID = api.State.SelectSelectedModelID(element);
            var operations = api.State.SelectRefinementOperations(element);
            var capabilities = api.State.SelectRefinementCapabilities(element);
            selectedModelID = await ModelSelectorWindow.Open(element, selectedModelID, Unity.AI.Mesh.Services.Stores.Selectors.Selectors.modalities, operations, capabilities);
            element.Dispatch(setSelectedModelID, (api.State.SelectRefinementMode(element), selectedModelID));
        });

        public static readonly AsyncThunkCreatorWithArg<GenerationDataWindowArgs> openGenerationDataWindow = new($"{slice}/{nameof(openGenerationDataWindow)}",
            async (args, api) => await GenerationMetadataWindow.Open(args.element.GetStore(), args.asset, args.element, args.result));

        public static readonly AssetActionCreator<float> setHistoryDrawerHeight = new($"{slice}/{nameof(setHistoryDrawerHeight)}");

        public static readonly AssetActionCreator<float> setGenerationPaneWidth = new($"{slice}/{nameof(setGenerationPaneWidth)}");


        public static AssetActionCreator<MeshSettingsState> setMeshSettings => new($"{slice}/{nameof(setMeshSettings)}");
        public static AssetActionCreator<MeshPivotMode> setPivotMode => new($"{slice}/{nameof(setPivotMode)}");

        public static readonly AsyncThunkCreatorWithArg<VisualElement> openMeshSettingsWindow = new($"{slice}/{nameof(openMeshSettingsWindow)}",
            async (element, api) => await MeshSettingsWindow.Open(element.GetStore(), element.GetAsset(), element));
    }
}