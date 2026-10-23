using System;
using System.Collections.Generic;
using AiEditorToolsSdk.Components.Common.Enums;

namespace Unity.AI.ModelSelector.Services.Stores.States
{
    static class ModelConstants
    {
        public static class Providers
        {
            public const string None = "None";
            public const string Unity = "Unity";
            public const string Scenario = "Scenario";
            public const string Layer = "Layer";
            public const string Uthana = "Uthana";

            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return Unity;
                yield return Scenario;
                yield return Layer;
                yield return Uthana;
            }
        }

        public static class Consumers
        {
            public const string Assistant = "Assistant";
        }

        public static class Modalities
        {
            public const string None = "None";
            public const string Image = "Image";
            public const string Texture2d = "Texture2d";
            public const string Sound = "Sound";
            public const string Animate = "Animate";
            public const string Skybox = "Skybox";
            public const string Model3D = "Model3d";
            public const string Video = "Video";

            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return Image;
                yield return Texture2d;
                yield return Sound;
                yield return Animate;
                yield return Skybox;
                yield return Model3D;
                yield return Video;
            }
        }

        public static class Operations
        {
            public const string None = "None";

            ////////////////////////////////////// Generative

            /// <summary>
            /// Generic text prompt
            /// </summary>
            public const string TextPrompt = "TextPrompt";

            /// <summary>
            /// Generic single reference
            /// </summary>
            public const string ReferencePrompt = "ReferencePrompt";

            // Specific Image references
            public const string StyleReference = "StyleReference";
            public const string CompositionReference = "CompositionReference";
            public const string PoseReference = "PoseReference";
            public const string DepthReference = "DepthReference";
            public const string LineArtReference = "LineArtReference";
            public const string FeatureReference = "FeatureReference";
            public const string MaskReference = "MaskReference";
            public const string RecolorReference = "RecolorReference";
            public const string GenerativeUpscale = "GenerativeUpscale";
            public const string TextureUpscale = "TextureUpscale";

            // Specific Stylesheet references
            public const string FirstFrameReference = "FirstFrameReference";
            public const string LastFrameReference = "LastFrameReference";

            // Specific Animate references
            public const string MotionFrameReference = "MotionFrameReference";

            // Specific Texture2D references
            public const string Pbr = "Pbr";

            ////////////////////////////////////// Transformative
            public const string Pixelate = "Pixelate";
            public const string RemoveBackground = "RemoveBackground";
            public const string Upscale = "Upscale";
            public const string SkyboxUpscale = "SkyboxUpscale";

            public static IEnumerable<string> EnumerateAll()
            {
                yield return None;
                yield return TextPrompt;
                yield return ReferencePrompt;
                yield return StyleReference;
                yield return CompositionReference;
                yield return PoseReference;
                yield return DepthReference;
                yield return LineArtReference;
                yield return FeatureReference;
                yield return MaskReference;
                yield return RecolorReference;
                yield return GenerativeUpscale;
                yield return MotionFrameReference;
                yield return Pbr;
                yield return Pixelate;
                yield return RemoveBackground;
                yield return Upscale;
                yield return SkyboxUpscale;
                yield return TextureUpscale;
            }
        }

        public static ProviderEnum ConvertToProvider(string provider)
        {
            return Enum.TryParse<ProviderEnum>(provider, out var result) ? result : ProviderEnum.None;
        }

        public static ModalityEnum ConvertToModality(string modality)
        {
            return Enum.TryParse<ModalityEnum>(modality, out var result) ? result : ModalityEnum.None;
        }

        // replace Texture2d with Material when needing to resolve ambiguities caused by Texture2d with agents
        public const string Material = "Material";
        public const string Cubemap = "Cubemap";

        public static class ModelCapabilities
        {
            public const string EditWithPrompt = "EditWithPrompt";
            public const string SingleInputImage = "SingleInputImage";
            public const string CustomResolutions = "CustomResolutions";
            public const string SupportsLooping = "SupportsLooping";
            public const string Supports9SliceUI = "Supports9SliceUI";
            public const string MultiReferenceImages = "MultiReferenceImages";

            public const int CustomResolutionsMin = 1024;
            public const int CustomResolutionsMax = 4096;
            public const int DefaultMaxReferenceImages = 14;
            public const string Recolor = "Recolor";
            public const string PBR = "PBR";
        }

        public static class ModelLimitations
        {
            public const string Requires300PxReference = "Requires300pxReference";
        }

        public static class SchemaKeys
        {
            public const string Prompt = "prompt";
            public const string NegativePrompt = "negative_prompt";
            public const string AspectRatio = "aspect_ratio";
            public const string AspectRatioCamel = "aspectRatio";
            public const string Dimensions = "dimensions";
            public const string Width = "width";
            public const string Height = "height";
            public const string Resolution = "resolution";
            public const string Seed = "seed";
            public const string GuidanceScale = "guidance_scale";
            public const string Steps = "steps";
            public const string StyleLayers = "style_layers";
            public const string ReferenceImage = "reference_image";
            public const string ReferenceImages = "reference_images";
            public const string CompositionReference = "composition_reference";
            public const string ControlMode = "control_mode";
            public const string Images = "images";
            public const string ControlImages = "control_images";
            public const string MaskReference = "mask_reference";
            public const string StyleReference = "style_reference";
            public const string Tags = "tags";
            public const string Loop = "loop";
            public const string Speed = "speed";
            public const string Duration = "duration";
            public const string Voice = "voice";
            public const string OutputFormat = "outputFormat";
            public const string LanguageCode = "languageCode";
            public const string ModelVersion = "modelVersion";
            public const string PbrType = "pbr_type";
            public const string ImageReference = "image_reference";
            public const string PoseReference = "pose_reference";
            public const string DepthReference = "depth_reference";
            public const string FeatureReference = "feature_reference";
            public const string LineArtReference = "line_art_reference";
            public const string RecolorReference = "recolor_reference";
            public const string PromptUpsampling = "promptUpsampling";
            public const string NumOutputs = "numOutputs";
            public const string InputFidelity = "inputFidelity";
            public const string Quality = "quality";
            public const string Background = "background";
            public const string NumInferenceSteps = "numInferenceSteps";
            public const string Guidance = "guidance";
            public const string CfgScale = "cfg_scale";
            public const string CfgScaleCamel = "cfgScale";
            public const string CameraFixed = "camera_fixed";
            public const string CameraFixedCamel = "cameraFixed";
            public const string NegativePromptCamel = "negativePrompt";

            // Voice/audio keys
            public const string Stability = "stability";
            public const string SimilarityBoost = "similarityBoost";
            public const string StyleExaggeration = "styleExaggeration";
            public const string PreviousText = "previousText";
            public const string NextText = "nextText";
            public const string PromptInfluence = "promptInfluence";
            public const string InputAudio = "inputAudio";
            public const string Continuation = "continuation";
            public const string ContinuationStart = "continuationStart";
            public const string ContinuationEnd = "continuationEnd";
            public const string MultiBandDiffusion = "multiBandDiffusion";
            public const string NormalizationStrategy = "normalizationStrategy";
            public const string Temperature = "temperature";
            public const string ClassifierFreeGuidance = "classifierFreeGuidance";

            // LoRA keys
            public const string ModelId = "modelId";
            public const string Loras = "loras";
            public const string LorasScale = "lorasScale";

            // Search/output keys
            public const string UseGoogleSearch = "useGoogleSearch";
            public const string UseGoogleSearchSnake = "use_google_search";
            public const string NumOutputsSnake = "num_outputs";

            // Image generation keys
            public const string Size = "size";
            public const string SequentialImageGeneration = "sequentialImageGeneration";
            public const string MaxImages = "maxImages";

            // Motion keys
            public const string CharacterId = "character_id";
            public const string Length = "length";

            // Video keys
            public const string ImageUrl = "imageUrl";
            public const string LastFrame = "lastFrame";
            public const string Image = "image";
            public const string LastFrameImage = "lastFrameImage";

            // Snake case variants for camelCase keys
            public const string PromptUpsamplingSnake = "prompt_upsampling";
            public const string InputFidelitySnake = "input_fidelity";
            public const string ModelIdSnake = "model_id";
            public const string LorasScaleSnake = "loras_scale";
            public const string NumInferenceStepsSnake = "num_inference_steps";
            public const string ModelVersionSnake = "model_version";
            public const string InputAudioSnake = "input_audio";
            public const string ContinuationStartSnake = "continuation_start";
            public const string ContinuationEndSnake = "continuation_end";
            public const string MultiBandDiffusionSnake = "multi_band_diffusion";
            public const string NormalizationStrategySnake = "normalization_strategy";
            public const string ClassifierFreeGuidanceSnake = "classifier_free_guidance";
            public const string SimilarityBoostSnake = "similarity_boost";
            public const string StyleExaggerationSnake = "style_exaggeration";
            public const string PreviousTextSnake = "previous_text";
            public const string NextTextSnake = "next_text";
            public const string LanguageCodeSnake = "language_code";
            public const string PromptInfluenceSnake = "prompt_influence";
            public const string OutputFormatSnake = "output_format";
            public const string LastFrameImageSnake = "last_frame_image";
            public const string ImageUrlSnake = "image_url";
            public const string LastFrameSnake = "last_frame";
            public const string SequentialImageGenerationSnake = "sequential_image_generation";
            public const string MaxImagesSnake = "max_images";

            // 3D model keys
            public const string TargetFormat = "targetFormat";
            public const string GeometryFileFormat = "geometry_file_format";
            public const string FaceCount = "face_count";
            public const string GenerateType = "generate_type";
            public const string EnablePbr = "enable_pbr";
            public const string ReferenceMultiviewFront = "reference_multiview_front";
            public const string ReferenceMultiviewBack = "reference_multiview_back";
            public const string ReferenceMultiviewLeft = "reference_multiview_left";
            public const string ReferenceMultiviewRight = "reference_multiview_right";
            public const string ReferenceMultiviewTop = "reference_multiview_top";
            public const string ReferenceMultiviewBottom = "reference_multiview_bottom";
            public const string ReferenceMultiviewLeftFront = "reference_multiview_leftfront";
            public const string ReferenceMultiviewRightFront = "reference_multiview_rightfront";
            public const string ReferenceModel = "reference_model";
            public const string PolygonType = "polygon_type";
            public const string FaceLevel = "face_level";
            public const string Texture = "texture";
            public const string TextureQuality = "texture_quality";
            public const string TextureAlignment = "texture_alignment";
            public const string TextureSeed = "texture_seed";
            public const string Orientation = "orientation";
            public const string EnableImageAutofix = "enable_image_autofix";
            public const string Pbr = "pbr";
            public const string FaceLimit = "face_limit";
            public const string AutoSize = "auto_size";
            public const string Quad = "quad";
            public const string Bake = "bake";
            public const string GeometryQuality = "geometry_quality";
            public const string SmartLowPoly = "smart_low_poly";
            public const string GenerateParts = "generate_parts";

            // Pixelate/transform keys
            public const string PixelGridSize = "pixel_grid_size";
            public const string RemoveNoise = "remove_noise";
            public const string ResizeToTargetSize = "resize_to_target_size";
            public const string TargetSize = "target_size";
            public const string PixelBlockSize = "pixel_block_size";
            public const string Mode = "mode";
            public const string OutlineThickness = "outline_thickness";

            // Recolor keys
            public const string ColorPaletteReference = "color_palette_reference";

            // Spritesheet/video frame keys
            public const string FirstFrameReference = "first_frame_reference";
            public const string LastFrameReference = "last_frame_reference";

            // Default fallback keys
            public const string AssetId = "asset_id";
            public const string VideoReference = "video_reference";
            public const string MotionFrames = "motion_frames";
            public const string PromptStrength = "prompt_strength";

            // Creativity-related keys
            public const string Creativity = "creativity";
            public const string Strength = "strength";
            public const string Fractality = "fractality";
            public const string LoraDetailsScale = "lora_details_scale";
            public const string StrengthDecay = "strength_decay";
            public const string StyleFidelity = "style_fidelity";
            public const string PromptFidelity = "prompt_fidelity";
            public const string DetailsLevel = "details_level";

            /// <summary>
            /// Parameter keys known to control creativity/variation in generation.
            /// Used to force minimum creativity when faithful output is desired.
            /// </summary>
            public static readonly string[] CreativityKeys =
            {
                Creativity, Strength, Fractality, LoraDetailsScale, StrengthDecay,
                StyleFidelity, PromptFidelity, DetailsLevel
            };

            // Default values
            public const string DefaultOutputFormatWav = "wav";
            public const string DefaultPbrPrompt = "PBR material";
            public const string DefaultControlModeTile = "tile";
            public const string DefaultControlModeCanny = "canny";

            // Video keys (snake case)
            public const string StartImage = "start_image";
            public const string EndImage = "end_image";
            public const string GenerateAudio = "generate_audio";

            // Ref keys
            public const string RefSkyboxReferences = "$ref:skybox-references";
            public const string RefUnityTexture2dReferences = "$ref:unity-texture2d-references";
            public const string RefVideoSeedanceParams = "$ref:video-seedance-params";
            public const string RefCommonBase = "$ref:common-base";
            public const string RefVideoKlingParams = "$ref:video-kling-params";
            public const string RefUnitySpriteReferences = "$ref:unity-sprite-references";
            public const string RefFluxDevReferences = "$ref:flux-dev-references";
            public const string RefVideoReferences = "$ref:video-references";
            public const string RefImageReferences = "$ref:image-references";
            public const string RefPixelArtReferences = "$ref:pixel-art-references";
            public const string RefFluxDimensions = "$ref:flux-dimensions";
            public const string RefModel3dReferences = "$ref:model3d-references";

            // UiSchema keys
            public const string UiOrder = "ui:order";
            public const string UiGroups = "ui:groups";
            public const string UiConditions = "ui:conditions";
            public const string UiWidget = "ui:widget";
            public const string UiHelp = "ui:help";
            public const string AssetPicker = "assetPicker";

            static readonly Dictionary<string, string> s_Variants = new Dictionary<string, string>
            {
                { AspectRatio, AspectRatioCamel },
                { AspectRatioCamel, AspectRatio },
                { NegativePrompt, NegativePromptCamel },
                { NegativePromptCamel, NegativePrompt },
                { CfgScale, CfgScaleCamel },
                { CfgScaleCamel, CfgScale },
                { CameraFixed, CameraFixedCamel },
                { CameraFixedCamel, CameraFixed },
                { NumOutputs, NumOutputsSnake },
                { NumOutputsSnake, NumOutputs },
                { UseGoogleSearch, UseGoogleSearchSnake },
                { UseGoogleSearchSnake, UseGoogleSearch },
                { PromptUpsampling, PromptUpsamplingSnake },
                { PromptUpsamplingSnake, PromptUpsampling },
                { InputFidelity, InputFidelitySnake },
                { InputFidelitySnake, InputFidelity },
                { ModelId, ModelIdSnake },
                { ModelIdSnake, ModelId },
                { LorasScale, LorasScaleSnake },
                { LorasScaleSnake, LorasScale },
                { NumInferenceSteps, NumInferenceStepsSnake },
                { NumInferenceStepsSnake, NumInferenceSteps },
                { ModelVersion, ModelVersionSnake },
                { ModelVersionSnake, ModelVersion },
                { InputAudio, InputAudioSnake },
                { InputAudioSnake, InputAudio },
                { ContinuationStart, ContinuationStartSnake },
                { ContinuationStartSnake, ContinuationStart },
                { ContinuationEnd, ContinuationEndSnake },
                { ContinuationEndSnake, ContinuationEnd },
                { MultiBandDiffusion, MultiBandDiffusionSnake },
                { MultiBandDiffusionSnake, MultiBandDiffusion },
                { NormalizationStrategy, NormalizationStrategySnake },
                { NormalizationStrategySnake, NormalizationStrategy },
                { ClassifierFreeGuidance, ClassifierFreeGuidanceSnake },
                { ClassifierFreeGuidanceSnake, ClassifierFreeGuidance },
                { SimilarityBoost, SimilarityBoostSnake },
                { SimilarityBoostSnake, SimilarityBoost },
                { StyleExaggeration, StyleExaggerationSnake },
                { StyleExaggerationSnake, StyleExaggeration },
                { PreviousText, PreviousTextSnake },
                { PreviousTextSnake, PreviousText },
                { NextText, NextTextSnake },
                { NextTextSnake, NextText },
                { LanguageCode, LanguageCodeSnake },
                { LanguageCodeSnake, LanguageCode },
                { PromptInfluence, PromptInfluenceSnake },
                { PromptInfluenceSnake, PromptInfluence },
                { OutputFormat, OutputFormatSnake },
                { OutputFormatSnake, OutputFormat },
                { LastFrameImage, LastFrameImageSnake },
                { LastFrameImageSnake, LastFrameImage },
                { ImageUrl, ImageUrlSnake },
                { ImageUrlSnake, ImageUrl },
                { LastFrame, LastFrameSnake },
                { LastFrameSnake, LastFrame },
                { SequentialImageGeneration, SequentialImageGenerationSnake },
                { SequentialImageGenerationSnake, SequentialImageGeneration },
                { MaxImages, MaxImagesSnake },
                { MaxImagesSnake, MaxImages },
            };

            public static string GetVariant(string key) => s_Variants.TryGetValue(key, out var variant) ? variant : null;

            public static bool IsSizingModeAspectRatio(string sizingMode) =>
                sizingMode == AspectRatio || sizingMode == AspectRatioCamel;

            public static bool IsSizingModeWidthHeight(string sizingMode) =>
                sizingMode != null && (sizingMode.StartsWith(Width) || sizingMode.StartsWith(Height));

            private static readonly HashSet<string> s_KnownKeys = new HashSet<string>
            {
                Prompt, NegativePrompt, AspectRatio, AspectRatioCamel, Dimensions, Width, Height,
                Resolution, Seed, GuidanceScale, Steps, StyleLayers, ReferenceImage, ReferenceImages,
                CompositionReference, ControlMode, Images, ControlImages, MaskReference, StyleReference, Tags,
                Loop, Speed, Duration, Voice, OutputFormat, LanguageCode, ModelVersion, PbrType,
                ImageReference, PoseReference, DepthReference, FeatureReference,
                LineArtReference, RecolorReference,
                PromptUpsampling, NumOutputs, InputFidelity, Quality, Background,
                NumInferenceSteps, Guidance, CfgScale, CfgScaleCamel, CameraFixed, CameraFixedCamel, NegativePromptCamel,
                Stability, SimilarityBoost, StyleExaggeration, PreviousText, NextText,
                PromptInfluence, InputAudio, Continuation, ContinuationStart, ContinuationEnd,
                MultiBandDiffusion, NormalizationStrategy, Temperature, ClassifierFreeGuidance,
                ModelId, Loras, LorasScale,
                UseGoogleSearch, UseGoogleSearchSnake, NumOutputsSnake,
                Size, SequentialImageGeneration, MaxImages,
                CharacterId, Length,
                ImageUrl, LastFrame, Image, LastFrameImage,
                RefSkyboxReferences, RefUnityTexture2dReferences, RefVideoSeedanceParams, RefCommonBase,
                RefVideoKlingParams, RefUnitySpriteReferences, RefFluxDevReferences, RefVideoReferences,
                RefImageReferences, RefPixelArtReferences, RefFluxDimensions, RefModel3dReferences,
                UiOrder, UiGroups, UiConditions, UiWidget, UiHelp, AssetPicker,
                // Snake case variants
                PromptUpsamplingSnake, InputFidelitySnake, ModelIdSnake, LorasScaleSnake,
                NumInferenceStepsSnake, ModelVersionSnake, InputAudioSnake,
                ContinuationStartSnake, ContinuationEndSnake, MultiBandDiffusionSnake,
                NormalizationStrategySnake, ClassifierFreeGuidanceSnake,
                SimilarityBoostSnake, StyleExaggerationSnake, PreviousTextSnake, NextTextSnake,
                LanguageCodeSnake, PromptInfluenceSnake, OutputFormatSnake,
                LastFrameImageSnake, ImageUrlSnake, LastFrameSnake,
                SequentialImageGenerationSnake, MaxImagesSnake,
                // 3D model keys
                TargetFormat, GeometryFileFormat, FaceCount, GenerateType, EnablePbr,
                ReferenceMultiviewFront, ReferenceMultiviewBack, ReferenceMultiviewLeft,
                ReferenceMultiviewRight, ReferenceMultiviewTop, ReferenceMultiviewBottom,
                ReferenceMultiviewLeftFront, ReferenceMultiviewRightFront,
                ReferenceModel, PolygonType, FaceLevel,
                Texture, TextureQuality, TextureAlignment, TextureSeed,
                Orientation, EnableImageAutofix, Pbr, FaceLimit, AutoSize,
                Quad, Bake, GeometryQuality, SmartLowPoly, GenerateParts,
                // Video keys
                StartImage, EndImage, GenerateAudio,
                // Transform keys
                AssetId, PixelGridSize, RemoveNoise, "remove_background",
                "color_palette", "color_palette_size",
                "background_color", "shadow_mode",
                "upscale_factor", "scaling_factor", Creativity,
                "target_width", StyleFidelity, PromptFidelity, DetailsLevel,
                Strength, Fractality, LoraDetailsScale, StrengthDecay,
                ColorPaletteReference,
                "preset", "image_prompt",
                ResizeToTargetSize, TargetSize, PixelBlockSize, Mode, OutlineThickness,
                FirstFrameReference, LastFrameReference, VideoReference, MotionFrames, PromptStrength
            };

            public static bool IsKnownKey(string key)
            {
                return s_KnownKeys.Contains(key);
            }
        }
        
        public static class SemanticTypes
        {
            public const string AssetId = "asset-id";
            public const string AssetIdList = "asset-id-list";
            public const string TextPrompt = "text-prompt";
            public const string NegativePrompt = "negative-prompt";
            public const string DimensionPixels = "dimension-pixels";
            public const string DimensionSeconds = "dimension-seconds";
            public const string RandomSeed = "random-seed";
            public const string NormalizedFloat = "normalized-float";
            public const string Percentage = "percentage";
            public const string StylePreset = "style-preset";
            public const string ModelVariant = "model-variant";
            public const string EnumSelection = "enum-selection";
            public const string ScaleFactor = "scale-factor";
        }

        public static class UiWidgets
        {
            public const string Textarea = "textarea";
            public const string Select = "select";
            public const string Range = "range";
            public const string Checkboxes = "checkboxes";
        }

        /// <summary>
        /// Values for the "x-unit" schema vendor extension. Mirrors the backend enum
        /// (see proton model-list TDD §4.2). Only the units the client acts on are named.
        /// </summary>
        public static class Units
        {
            public const string Seconds = "seconds";
            public const string Frames = "frames";
            public const string Steps = "steps";
            public const string Pixels = "pixels";
            public const string Megabytes = "megabytes";
            public const string Fps = "fps";
            public const string Meters = "meters";
            public const string Bpm = "bpm";

            /// <summary>
            /// Fallback reference frame rate used when a "frames" field omits
            /// "x-reference-frame-rate", and for legacy model lists that predate x-unit.
            /// Preserves the historical hardcoded 30 fps behavior.
            /// </summary>
            public const double DefaultFrameRate = 30d;
        }

    }
}
