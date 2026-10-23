using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.ModelSelector.Services.Stores.States;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Utility;

namespace Unity.AI.Mesh.Services.Stores.States
{
    [Serializable]
    record GenerationSetting
    {
        public SerializableDictionary<RefinementMode, ModelSelection> selectedModels = new();
        public string prompt = "";
        public string negativePrompt = "";
        public int variationCount = 1;
        public bool useCustomSeed;
        public int customSeed;
        public bool useFaceLimit;
        public int faceLimit = 50000;
        public string targetFormat = "";
        public RefinementMode refinementMode;

        public PromptImageReference promptImageReference = new();
        public List<MultiviewImageReferenceSettings> multiviewImageReferences = MultiviewImageReferenceSettings.CreateDefaults();
        public ModelReference modelReference = new();

        public float historyDrawerHeight = 200;
        public float generationPaneWidth = 280;

        public MeshSettingsState meshSettings = new();
    }

    [Serializable]
    record PromptImageReference
    {
        public AssetReference asset = new();
    }

    [Serializable]
    record ModelReference
    {
        public AssetReference asset = new();
    }

    [Serializable]
    record MultiviewImageReferenceSettings
    {
        public string viewKey = "";
        public string label = "";
        public AssetReference asset = new();
        public bool isRequired;

        public static List<MultiviewImageReferenceSettings> CreateDefaults() => new()
        {
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewFront, label = "Front Reference *", isRequired = true },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewBack, label = "Back Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewLeft, label = "Left Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewRight, label = "Right Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewLeftFront, label = "Left Front Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewRightFront, label = "Right Front Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewTop, label = "Top Reference" },
            new() { viewKey = ModelConstants.SchemaKeys.ReferenceMultiviewBottom, label = "Bottom Reference" },
        };
    }

    static class MultiviewImageReferenceExtensions
    {
        public static bool HasAnyValidView(this List<MultiviewImageReferenceSettings> refs)
            => refs?.Any(r => r.asset.IsValid()) ?? false;

        public static IEnumerable<(string key, AssetReference asset)> EnumerateValidViews(this List<MultiviewImageReferenceSettings> refs)
            => refs?.Where(r => r.asset.IsValid()).Select(r => (r.viewKey, r.asset)) ?? Enumerable.Empty<(string, AssetReference)>();

        public static AssetReference GetViewAsset(this List<MultiviewImageReferenceSettings> refs, string viewKey)
            => refs?.FirstOrDefault(r => r.viewKey == viewKey)?.asset ?? new AssetReference();
    }

    [AttributeUsage(AttributeTargets.Field)]
    class DisplayLabelAttribute : Attribute
    {
        public string Label { get; }
        public DisplayLabelAttribute(string label) => Label = label;
    }

    [AttributeUsage(AttributeTargets.Field)]
    sealed class DisplayOrderAttribute : Attribute
    {
        public int order { get; }
        public DisplayOrderAttribute(int order) => this.order = order;
    }

    enum RefinementMode : int
    {
        [DisplayOrder(0), DisplayLabel("Generate")]
        Generation = 0,
        [DisplayOrder(1), DisplayLabel("Retopology")]
        Retopology = 1,
        [DisplayOrder(2), DisplayLabel("Texturing")]
        Texturing = 2,
        [DisplayOrder(3), DisplayLabel("Rigging")]
        Rigging = 3,
    }

    static class RefinementModeExtensions
    {
        public static string GetDisplayLabel(this RefinementMode mode)
        {
            var type = typeof(RefinementMode);
            var memInfo = type.GetMember(mode.ToString());
            var attr = memInfo[0].GetCustomAttributes(typeof(DisplayLabelAttribute), false).FirstOrDefault() as DisplayLabelAttribute;
            return attr?.Label ?? mode.ToString();
        }
    }

    enum MeshPivotMode : int
    {
        Center = 0,
        BottomCenter = 1,
    }

    [Serializable]
    record ModelSelection
    {
        public string modelID = "";
    }
}