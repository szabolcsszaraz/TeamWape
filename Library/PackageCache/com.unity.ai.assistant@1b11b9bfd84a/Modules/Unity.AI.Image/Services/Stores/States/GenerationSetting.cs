using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.UI.Utilities;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.Image.Utilities;
using Unity.AI.Toolkit.Asset;
using Unity.AI.Toolkit.Utility;

namespace Unity.AI.Image.Services.Stores.States
{
    [Serializable]
    record GenerationSetting
    {
        public SerializableDictionary<RefinementMode, ModelSelection> selectedModels = new();
        public SerializableDictionary<RefinementMode, string> prompt = new();
        public SerializableDictionary<RefinementMode, string> negativePrompt = new();
        public int variationCount = 1;
        public bool useCustomSeed;
        public int customSeed;
        public RefinementMode refinementMode;
        public string imageDimensions;
        public bool useCustomResolution;
        public string selectedAspectRatio;
        public bool replaceBlankAsset = true;
        public bool replaceRefinementAsset = true;
        public int upscaleFactor = 2;
        public float duration = VideoResultFrameCache.Duration;

        public UnsavedAssetBytesSettings unsavedAssetBytes = new();

        // order must match ImageReferenceType enum values (index 4 reserved for removed InPaintMaskImage)
        public ImageReferenceSettings[] imageReferences = {
            new CompositionImageReference(),  // 0 = CompositionImage
            new DepthImageReference(),        // 1 = DepthImage
            new FeatureImageReference(),      // 2 = FeatureImage
            new LineArtImageReference(),      // 3 = LineArtImage
            new ImageReferenceSettings(0.25f),  // 4 = (reserved, was InPaintMaskImage)
            new PaletteImageReference(),      // 5 = PaletteImage
            new PoseImageReference(),         // 6 = PoseImage
            new PromptImageReference(),       // 7 = PromptImage
            new StyleImageReference(),        // 8 = StyleImage
            new FirstImageReference(),        // 9 = FirstImage
            new LastImageReference()          // 10 = LastImage
        };

        public List<ImageReferenceSettings> unlabeledImageReferences = new();

        public PixelateSettings pixelateSettings = new();
        public SpritesheetSettingsState spritesheetSettings = new();

        public SerializableDictionary<string, string> dynamicParams = new();

        public string pendingPing;
        public float historyDrawerHeight = 200;
        public float generationPaneWidth = 280;

        public LoopSettings loopSettings = new();
    }

    [Serializable]
    record LoopSettings
    {
        public float trimStartTime = 0f;
        public float trimEndTime = 1f;
    }

    [Serializable]
    record UnsavedAssetBytesSettings
    {
        public byte[] data = Array.Empty<byte>();
        public long timeStamp;
        public Uri uri;
        public bool spriteSheet;
        public float duration = 0;
    }

    [Serializable]
    record ImageReferenceSettings
    {
        public ImageReferenceSettings(float strength, bool isActive = false) { this.strength = strength; this.isActive = isActive; }
        public float strength = 0.25f;
        public AssetReference asset = new();
        public byte[] doodle = Array.Empty<byte>();
        public long doodleTimestamp;
        public ImageReferenceMode mode = ImageReferenceMode.Asset;
        public bool isActive;
    }

    record PromptImageReference() : ImageReferenceSettings(0.25f);
    record StyleImageReference() : ImageReferenceSettings(0.25f);
    record CompositionImageReference() : ImageReferenceSettings(0.75f);
    record PoseImageReference() : ImageReferenceSettings(0.90f);
    record DepthImageReference() : ImageReferenceSettings(0.75f);
    record LineArtImageReference() : ImageReferenceSettings(0.25f);
    record FeatureImageReference() : ImageReferenceSettings(0.25f);
    record PaletteImageReference() : ImageReferenceSettings(1, true);


    record FirstImageReference() : ImageReferenceSettings(1, true);
    record LastImageReference() : ImageReferenceSettings(1, true);



    [AttributeUsage(AttributeTargets.Field)]
    class DisplayLabelAttribute : Attribute
    {
        public string Label { get; }
        public DisplayLabelAttribute(string label) => Label = label;
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

    enum RefinementMode : int
    {
        [DisplayOrder(0), DisplayLabel("Generate")]
        Generation = 0,
        [DisplayOrder(1), DisplayLabel("Remove BG")]
        RemoveBackground = 1,
        [DisplayOrder(3), DisplayLabel("Upscale")]
        Upscale = 2,
        [DisplayOrder(4), DisplayLabel("Pixelate")]
        Pixelate = 3,
        [DisplayOrder(5), DisplayLabel("Recolor")]
        Recolor = 4,
        [DisplayOrder(2), DisplayLabel("Spritesheet")]
        Spritesheet = 6
    }

    enum ImageReferenceMode : int
    {
        Asset = 0,
        Doodle = 1
    }

    [Serializable]
    record ModelSelection
    {
        public string modelID = "";
    }
}
