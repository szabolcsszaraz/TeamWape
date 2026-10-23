using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using Unity.AI.Generators.Tools;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Video;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class GenerateAssetTool
    {
        public const string ToolName = "Unity.AssetGeneration.GenerateAsset";

        // Stop a stalled server-dependent generation step (download / background-removal) from
        // hanging the chat turn forever (UUM-144682). Comfortably above a normal
        // generation+download+post-process; on timeout the turn hands off to the background
        // (see InProgressOutput) rather than failing. 3D/mesh generations legitimately run
        // longer, so they get a larger bound.
        // Stop a stalled server-dependent generation step (download / background-removal) from
        // hanging the chat turn forever (UUM-144682). Comfortably above a normal
        // generation+download+post-process; on timeout the turn hands off to the background
        // (see InProgressOutput) rather than failing. 3D/mesh generations legitimately run
        // longer, so they get a larger bound.
        static readonly TimeSpan k_GenerateAssetTimeout = TimeSpan.FromMinutes(5);
        static readonly TimeSpan k_MeshGenerateAssetTimeout = TimeSpan.FromMinutes(10);

        static readonly Dictionary<string, int> k_ViewLabelToIndex = new(StringComparer.OrdinalIgnoreCase)
        {
            { "front", 0 },
            { "back", 1 },
            { "left", 2 },
            { "right", 3 },
            { "left_front", 4 },
            { "right_front", 5 },
            { "top", 6 },
            { "bottom", 7 },
        };

        [AgentTool(Constants.GenerateAssetFunctionDescription, ToolName)]
        [AgentToolSettings(mcp: McpAvailability.Default, tags: Constants.AssetGenerationFunctionTag)]
        public static async Task<GenerateAssetOutput> GenerateAsset(
            ToolExecutionContext context,
            [ToolParameter(Constants.CommandDescription)]
            GenerationCommands command,
            [ToolParameter(Constants.ModelIdDescription)]
            string modelId = null,
            [ToolParameter(Constants.PromptDescription)]
            string prompt = null,
            [ToolParameter(Constants.SavePathDescription)]
            string savePath = null,
            [ToolParameter(Constants.WaitForCompletionDescription)]
            bool waitForCompletion = true,
            [ToolParameter(Constants.TargetAssetPathDescription)]
            string targetAssetPath = null,
            [ToolParameter(Constants.ReferenceImageInstanceIdDescription)]
            long referenceImageInstanceId = -1,
            [ToolParameter(Constants.ReferenceImageInstanceIdsDescription)]
            long[] referenceImageInstanceIds = null,
            [ToolParameter(Constants.ReferenceImageLabelsDescription)]
            string[] referenceImageLabels = null,
            [ToolParameter(Constants.DurationInSecondsDescription)]
            float durationInSeconds = 0,
            [ToolParameter(Constants.LoopDescription)]
            bool loop = false,
            [ToolParameter(Constants.SpriteWidthDescription)]
            int width = 0,
            [ToolParameter(Constants.SpriteHeightDescription)]
            int height = 0,
            [ToolParameter(Constants.VoiceNameDescription)]
            string voiceName = null,
            [ToolParameter(Constants.ForceGenerationDescription)]
            bool forceGeneration = false,
            [ToolParameter(Constants.MeshFormatDescription)]
            string meshFormat = "fbx")
        {
            AssetTypes assetType = AssetTypes.None;
            try
            {
                if (!forceGeneration && AssetGenerators.HasInterruptedDownloads())
                {
                    throw new AssetGenerationBusyException("There are interrupted asset generations. Please 'resume' them before generating new assets. To bypass this check, set 'forceGeneration' to 'true'.");
                }

                var interruptedAssetPaths = AssetGenerators.GetAllDownloadAssets()
                    .Select(AssetDatabase.GetAssetPath)
                    .ToList();

                if (!string.IsNullOrEmpty(targetAssetPath) && interruptedAssetPaths.Contains(targetAssetPath))
                    throw new AssetGenerationBusyException($"Asset {targetAssetPath} is still being generated, check back later.");

                // Resolve reference images: prefer array over single
                Object[] referenceImages = null;
                Object referenceImage = null;

                if (referenceImageInstanceIds is { Length: > 0 })
                {
                    if (referenceImageInstanceIds.Length > 10)
                        throw new ArgumentException("A maximum of 10 reference images can be provided.");

                    referenceImages = new Object[referenceImageInstanceIds.Length];
                    for (int i = 0; i < referenceImageInstanceIds.Length; i++)
                    {
                        var id = referenceImageInstanceIds[i];
    #if UNITY_6000_5_OR_NEWER
                        var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)id));
    #elif UNITY_6000_3_OR_NEWER
                        var obj = EditorUtility.EntityIdToObject((int)id);
    #else
                        var obj = EditorUtility.InstanceIDToObject((int)id);
    #endif
                        if (obj == null)
                            throw new Exception($"Reference image with InstanceID {id} does not exist. Please provide a valid InstanceID.");
                        if (obj is not Texture2D)
                            throw new ArgumentException($"Reference image with InstanceID {id} is not a valid {nameof(Texture2D)}.");

                        var refPath = AssetDatabase.GetAssetPath(obj);
                        if (!string.IsNullOrEmpty(refPath) && interruptedAssetPaths.Contains(refPath))
                            throw new AssetGenerationBusyException($"Asset {refPath} is still being generated, check back later.");

                        referenceImages[i] = obj;
                    }
                    referenceImage = referenceImages[0];

                    if (referenceImageLabels != null)
                    {
                        if (referenceImageLabels.Length != referenceImageInstanceIds.Length)
                            throw new ArgumentException(
                                $"'referenceImageLabels' length ({referenceImageLabels.Length}) must match " +
                                $"'referenceImageInstanceIds' length ({referenceImageInstanceIds.Length}).");

                        foreach (var label in referenceImageLabels)
                        {
                            if (!k_ViewLabelToIndex.ContainsKey(label))
                                throw new ArgumentException(
                                    $"Unknown view label '{label}'. Accepted values: {string.Join(", ", k_ViewLabelToIndex.Keys)}.");
                        }

                        var duplicates = referenceImageLabels
                            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                            .Where(g => g.Count() > 1)
                            .Select(g => g.Key)
                            .ToList();
                        if (duplicates.Count > 0)
                            throw new ArgumentException(
                                $"Duplicate view labels: {string.Join(", ", duplicates)}. Each label must be unique.");
                    }
                }
                else if (referenceImageInstanceId != -1)
                {
    #if UNITY_6000_5_OR_NEWER
                    if (EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)referenceImageInstanceId)) == null)
    #elif UNITY_6000_3_OR_NEWER
                    if (EditorUtility.EntityIdToObject((int)referenceImageInstanceId) == null)
    #else
                    if (EditorUtility.InstanceIDToObject((int)referenceImageInstanceId) == null)
    #endif
                        throw new Exception($"Reference image with InstanceID {referenceImageInstanceId} does not exist. Please provide a valid InstanceID.");

                    {
    #if UNITY_6000_5_OR_NEWER
                        var referenceImagePath = AssetDatabase.GetAssetPath(EntityId.FromULong((ulong)referenceImageInstanceId));
    #elif UNITY_6000_3_OR_NEWER
                        var referenceImagePath = AssetDatabase.GetAssetPath((EntityId)(int)referenceImageInstanceId);
    #else
                        var referenceImagePath = AssetDatabase.GetAssetPath(EditorUtility.InstanceIDToObject((int)referenceImageInstanceId));
    #endif
                        if (!string.IsNullOrEmpty(referenceImagePath) && interruptedAssetPaths.Contains(referenceImagePath))
                            throw new AssetGenerationBusyException($"Asset {referenceImagePath} is still being generated, check back later.");
                    }

    #if UNITY_6000_5_OR_NEWER
                    referenceImage = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)referenceImageInstanceId));
    #elif UNITY_6000_3_OR_NEWER
                    referenceImage = EditorUtility.EntityIdToObject((int)referenceImageInstanceId);
    #else
                    referenceImage = EditorUtility.InstanceIDToObject((int)referenceImageInstanceId);
    #endif
                    referenceImages = referenceImage != null ? new[] { referenceImage } : null;
                }

                GenerationHandle<Object> generationHandle;
                Object targetAsset = null;

                switch (command)
                {
                    case GenerationCommands.GenerateHumanoidAnimation:
                    {
                        if (referenceImage) throw new ArgumentException("A 'referenceImage' cannot be used when generating a Humanoid Animation.");
                        assetType = AssetTypes.HumanoidAnimation;
                        VideoClip videoAsset = null;

                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<AnimationClip>(targetAssetPath);
                            videoAsset = AssetDatabase.LoadAssetAtPath<VideoClip>(targetAssetPath);
                            if (targetAsset == null && videoAsset == null)
                                throw new ArgumentException($"Failed to find a valid AnimationClip or VideoClip asset at the specified path: '{targetAssetPath}'.");
                        }

                        if (videoAsset == null && string.IsNullOrEmpty(prompt))
                            throw new ArgumentException(Constants.PromptRequired);

                        var parameters = new GenerationParameters<AnimationSettings>
                        {
                            AssetType = typeof(AnimationClip),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = new AnimationSettings { DurationInSeconds = durationInSeconds, VideoReference = videoAsset },
                            TargetAsset = targetAsset,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(AnimationClip), cost)
                        };
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.GenerateCubemap:
                    {
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException(Constants.PromptRequired);
                        if (referenceImage) throw new ArgumentException("A 'referenceImage' cannot be used when generating a Cubemap.");
                        assetType = AssetTypes.Cubemap;
                        var parameters = new GenerationParameters<CubemapSettings>
                        {
                            AssetType = typeof(Cubemap),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = new CubemapSettings { Upscale = false },
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Cubemap), cost)
                        };
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Cubemap>(targetAssetPath);
                            if (targetAsset == null)
                                throw new ArgumentException($"Failed to find a valid Cubemap asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.UpscaleCubemap:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for upscaling a cubemap.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when upscaling a cubemap.");
                        if (referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' cannot be used when upscaling a cubemap.");

                        assetType = AssetTypes.Cubemap;
                        var targetCubemap = AssetDatabase.LoadAssetAtPath<Cubemap>(targetAssetPath);
                        if (targetCubemap == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(Cubemap)} asset at the specified path: '{targetAssetPath}'.");

                        generationHandle = AssetGenerators.UpscaleCubemapAsync(targetCubemap, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Cubemap), cost));
                        targetAsset = targetCubemap;
                        break;
                    }
                    case GenerationCommands.GenerateMaterial:
                    case GenerationCommands.GenerateTerrainLayer:
                    {
                        var isTerrainLayer = command == GenerationCommands.GenerateTerrainLayer;

                        if (referenceImage == null && string.IsNullOrEmpty(prompt))
                            throw new ArgumentException($"Either '{nameof(referenceImageInstanceId)}' or '{nameof(prompt)}' must be provided.");

                        if (isTerrainLayer)
                        {
                            assetType = AssetTypes.TerrainLayer;
                            var settings = new TerrainLayerSettings();
                            if (referenceImage != null)
                            {
                                if (referenceImage is not Texture2D)
                                    throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                                settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                            }
                            var parameters = new GenerationParameters<TerrainLayerSettings>
                            {
                                AssetType = typeof(TerrainLayer),
                                Prompt = prompt,
                                SavePath = savePath,
                                ModelId = modelId,
                                Settings = settings,
                                PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(TerrainLayer), cost)
                            };
                            if (!string.IsNullOrEmpty(targetAssetPath))
                            {
                                targetAsset = AssetDatabase.LoadAssetAtPath<TerrainLayer>(targetAssetPath);
                                if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(TerrainLayer)} asset at the specified path: '{targetAssetPath}'.");
                                parameters.TargetAsset = targetAsset;
                            }
                            generationHandle = AssetGenerators.GenerateAsync(parameters);
                        }
                        else
                        {
                            assetType = AssetTypes.Material;
                            var settings = new MaterialSettings();
                            if (referenceImage != null)
                            {
                                if (referenceImage is not (Texture2D or Cubemap))
                                    throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)} or {nameof(Cubemap)}.");
                                settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                            }
                            var parameters = new GenerationParameters<MaterialSettings>
                            {
                                AssetType = typeof(Material),
                                Prompt = prompt,
                                SavePath = savePath,
                                ModelId = modelId,
                                Settings = settings,
                                PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Material), cost)
                            };
                            if (!string.IsNullOrEmpty(targetAssetPath))
                            {
                                targetAsset = AssetDatabase.LoadAssetAtPath<Material>(targetAssetPath);
                                if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(Material)} asset at the specified path: '{targetAssetPath}'.");
                                parameters.TargetAsset = targetAsset;
                            }
                            generationHandle = AssetGenerators.GenerateAsync(parameters);
                        }
                        break;
                    }
                    case GenerationCommands.AddPbrToMaterial:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required to add PBR to a material.");
                        assetType = AssetTypes.Material;

                        var targetMaterial = AssetDatabase.LoadAssetAtPath<Material>(targetAssetPath);
                        if (targetMaterial == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(Material)} asset at the specified path: '{targetAssetPath}'.");

                        var settings = new MaterialSettings();
                        if (referenceImage != null)
                        {
                            if (referenceImage is not (Texture2D or Cubemap))
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)} or {nameof(Cubemap)}.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }
                        generationHandle = AssetGenerators.AddPbrToMaterialAsync(targetMaterial, settings, modelId, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Material), cost));
                        targetAsset = targetMaterial;
                        break;
                    }
                    case GenerationCommands.AddPbrToTerrainLayer:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required to add PBR to a terrain layer.");
                        assetType = AssetTypes.TerrainLayer;

                        var targetTerrainLayer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(targetAssetPath);
                        if (targetTerrainLayer == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(TerrainLayer)} asset at the specified path: '{targetAssetPath}'.");

                        var settings = new TerrainLayerSettings();
                        if (referenceImage != null)
                        {
                            if (referenceImage is not Texture2D)
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }
                        generationHandle = AssetGenerators.AddPbrToTerrainLayerAsync(targetTerrainLayer, settings, modelId, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(TerrainLayer), cost));
                        targetAsset = targetTerrainLayer;
                        break;
                    }
                    case GenerationCommands.GenerateMesh:
                    {
                        if (string.IsNullOrEmpty(prompt) && referenceImage == null)
                            throw new ArgumentException($"Either '{nameof(prompt)}' or '{nameof(referenceImageInstanceId)}' must be provided for Mesh generation.");
                        assetType = AssetTypes.Mesh;
                        var settings = new MeshSettings { TargetFormat = meshFormat?.ToLowerInvariant() };
                        if (referenceImages != null)
                        {
                            foreach (var img in referenceImages)
                            {
                                if (img is not Texture2D)
                                    throw new ArgumentException($"Reference image is not a valid {nameof(Texture2D)}.");
                            }
                            settings.ImageReferences = referenceImages.Select((r, i) => new ObjectReference
                            {
                                Image = r,
                                Label = referenceImageLabels != null && i < referenceImageLabels.Length ? referenceImageLabels[i] : null
                            }).ToArray();
                        }
                        else if (referenceImage != null)
                        {
                            if (referenceImage is not Texture2D)
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }
                        var parameters = new GenerationParameters<MeshSettings>
                        {
                            AssetType = typeof(GameObject),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(GameObject), cost)
                        };
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);
                            if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(GameObject)} asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.RetopologyMesh:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for mesh retopology.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when retopologizing a mesh.");
                        if (referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' cannot be used when retopologizing a mesh.");

                        assetType = AssetTypes.Mesh;
                        var targetMesh = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);
                        if (targetMesh == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(GameObject)} asset at the specified path: '{targetAssetPath}'.");

                        generationHandle = AssetGenerators.RetopologyMeshAsync(targetMesh, modelId, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(GameObject), cost));
                        targetAsset = targetMesh;
                        break;
                    }
                    case GenerationCommands.TextureMesh:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for mesh texturing.");
                        if (string.IsNullOrEmpty(prompt) && !referenceImage) throw new ArgumentException($"Either '{nameof(prompt)}' or '{nameof(referenceImageInstanceId)}' must be provided for mesh texturing.");

                        assetType = AssetTypes.Mesh;
                        var targetMesh = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);
                        if (targetMesh == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(GameObject)} asset at the specified path: '{targetAssetPath}'.");

                        var textureMeshSettings = new MeshSettings { ModelReference = targetMesh };
                        if (referenceImage)
                        {
                            if (referenceImage is not Texture2D) throw new ArgumentException($"'{nameof(referenceImageInstanceId)}' is not a valid Texture2D.");
                            textureMeshSettings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }

                        generationHandle = AssetGenerators.TextureMeshAsync(targetMesh, prompt, modelId, textureMeshSettings, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(GameObject), cost));
                        targetAsset = targetMesh;
                        break;
                    }
                    case GenerationCommands.RigMesh:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for mesh rigging.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when rigging a mesh.");
                        if (referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' cannot be used when rigging a mesh.");

                        assetType = AssetTypes.Mesh;
                        var targetMesh = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);
                        if (targetMesh == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(GameObject)} asset at the specified path: '{targetAssetPath}'.");

                        generationHandle = AssetGenerators.RigMeshAsync(targetMesh, modelId, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(GameObject), cost));
                        targetAsset = targetMesh;
                        break;
                    }
                    case GenerationCommands.GenerateSound:
                    {
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException(Constants.PromptRequired);
                        if (referenceImage) throw new ArgumentException("A 'referenceImage' cannot be used when generating a Sound.");
                        assetType = AssetTypes.Sound;
                        var parameters = new GenerationParameters<SoundSettings>
                        {
                            AssetType = typeof(AudioClip),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = new SoundSettings { DurationInSeconds = durationInSeconds, Loop = loop, VoiceName = voiceName },
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(AudioClip), cost)
                        };
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<AudioClip>(targetAssetPath);
                            if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(AudioClip)} asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.GenerateSprite:
                    {
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException(Constants.PromptRequired);
                        assetType = AssetTypes.Sprite;
                        var settings = new SpriteSettings { RemoveBackground = false, Width = width, Height = height };
                        if (referenceImages is { Length: > 0 })
                        {
                            settings.ImageReferences = referenceImages.Select(img => new ObjectReference { Image = img }).ToArray();
                        }
                        else if (referenceImage)
                        {
                            if (referenceImage is not Texture2D) throw new ArgumentException("ReferenceImage is not a valid Texture2D.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }
                        var parameters = new GenerationParameters<SpriteSettings>
                        {
                            AssetType = typeof(Texture2D),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost)
                        };
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                            if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.GenerateImage:
                    {
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException(Constants.PromptRequired);
                        assetType = AssetTypes.Image;
                        var settings = new ImageSettings { RemoveBackground = false, Width = width, Height = height };
                        if (referenceImages is { Length: > 0 })
                        {
                            settings.ImageReferences = referenceImages.Select(img => new ObjectReference { Image = img }).ToArray();
                        }
                        else if (referenceImage)
                        {
                            if (referenceImage is not Texture2D) throw new ArgumentException("ReferenceImage is not a valid Texture2D.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }
                        var parameters = new GenerationParameters<ImageSettings>
                        {
                            AssetType = typeof(Texture2D),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost)
                        };
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                            if (targetAsset == null) throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.RemoveSpriteBackground:
                    case GenerationCommands.RemoveImageBackground:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for removing a sprite's background.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when removing a sprite's background.");
                        if (referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' cannot be used when removing a background.");

                        assetType = command == GenerationCommands.RemoveSpriteBackground ? AssetTypes.Sprite : AssetTypes.Image;
                        var targetSprite = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                        if (targetSprite == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");

                        generationHandle = AssetGenerators.RemoveSpriteBackgroundAsync(targetSprite, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost));
                        targetAsset = targetSprite;
                        break;
                    }
                    case GenerationCommands.UpscaleImage:
                    case GenerationCommands.UpscaleSprite:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for upscaling an image.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when upscaling an image.");
                        if (referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' cannot be used when upscaling an image.");

                        assetType = command == GenerationCommands.UpscaleSprite ? AssetTypes.Sprite : AssetTypes.Image;
                        var targetImage = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                        if (targetImage == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");

                        generationHandle = AssetGenerators.UpscaleImageAsync(targetImage, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost));
                        targetAsset = targetImage;
                        break;
                    }
                    case GenerationCommands.RecolorImage:
                    case GenerationCommands.RecolorSprite:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath)) throw new ArgumentException($"'{nameof(targetAssetPath)}' is required for recoloring an image.");
                        if (!referenceImage) throw new ArgumentException($"A '{nameof(referenceImageInstanceId)}' is required as the color palette image for recoloring.");
                        if (!string.IsNullOrEmpty(prompt)) throw new ArgumentException($"A '{nameof(prompt)}' cannot be used when recoloring an image.");

                        assetType = command == GenerationCommands.RecolorSprite ? AssetTypes.Sprite : AssetTypes.Image;
                        var targetRecolorImage = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                        if (targetRecolorImage == null)
                            throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                        if (referenceImage is not Texture2D paletteImage)
                            throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");

                        generationHandle = AssetGenerators.RecolorImageAsync(targetRecolorImage, paletteImage, (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost));
                        targetAsset = targetRecolorImage;
                        break;
                    }
                    case GenerationCommands.EditSpriteWithPrompt:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath) && !referenceImage) throw new ArgumentException($"'{nameof(targetAssetPath)}' or '{nameof(referenceImageInstanceId)}' is required when editing an asset with a prompt.");
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException($"'{nameof(prompt)}' is required when editing an asset.");

                        assetType = AssetTypes.Sprite;
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                            if (targetAsset == null)
                                throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                        }

                        Texture2D imageReference;
                        if (referenceImage != null)
                        {
                            imageReference = referenceImage as Texture2D;
                            if (imageReference == null)
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                        }
                        else
                        {
                            imageReference = (Texture2D)targetAsset;
                        }

                        var settings = new SpriteSettings
                        {
                            RemoveBackground = false,
                            Width = width,
                            Height = height,
                            ImageReferences = new[] { new ObjectReference { Image = imageReference } }
                        };
                        var parameters = new GenerationParameters<SpriteSettings>
                        {
                            AssetType = typeof(Texture2D),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            TargetAsset = targetAsset,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost)
                        };
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    case GenerationCommands.EditImageWithPrompt:
                    {
                        if (string.IsNullOrEmpty(targetAssetPath) && !referenceImage) throw new ArgumentException($"'{nameof(targetAssetPath)}' or '{nameof(referenceImageInstanceId)}' is required when editing an asset with a prompt.");
                        if (string.IsNullOrEmpty(prompt)) throw new ArgumentException($"'{nameof(prompt)}' is required when editing an asset.");

                        assetType = AssetTypes.Image;
                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                            if (targetAsset == null)
                                throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                        }

                        Texture2D imageReference;
                        if (referenceImage != null)
                        {
                            imageReference = referenceImage as Texture2D;
                            if (imageReference == null)
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                        }
                        else
                        {
                            imageReference = (Texture2D)targetAsset;
                        }

                        var settings = new ImageSettings
                        {
                            RemoveBackground = false,
                            Width = width,
                            Height = height,
                            ImageReferences = new[] { new ObjectReference { Image = imageReference } }
                        };
                        var parameters = new GenerationParameters<ImageSettings>
                        {
                            AssetType = typeof(Texture2D),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            TargetAsset = targetAsset,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost)
                        };
                        generationHandle = AssetGenerators.GenerateAsync(parameters);
                        break;
                    }
                    default:
                        throw new ArgumentException($"Unsupported command: '{nameof(command)}'.");
                    case GenerationCommands.GenerateSpritesheet:
                    {
                        if (string.IsNullOrEmpty(prompt) || referenceImage == null)
                            throw new ArgumentException($"Both '{nameof(prompt)}' and '{nameof(referenceImageInstanceId)}' must be provided for Spritesheet generation.");

                        assetType = AssetTypes.Spritesheet;
                        var settings = new SpriteSettings { RemoveBackground = false, Width = width, Height = height, Loop = loop };

                        if (referenceImage != null)
                        {
                            if (referenceImage is not Texture2D)
                                throw new ArgumentException($"{nameof(referenceImageInstanceId)} is not a valid {nameof(Texture2D)}.");
                            settings.ImageReferences = new[] { new ObjectReference { Image = referenceImage } };
                        }

                        var parameters = new GenerationParameters<SpriteSettings>
                        {
                            AssetType = typeof(Texture2D),
                            Prompt = prompt,
                            SavePath = savePath,
                            ModelId = modelId,
                            Settings = settings,
                            PermissionCheckAsync = (path, cost) => context.Permissions.CheckAssetGeneration(path, typeof(Texture2D), cost)
                        };

                        if (!string.IsNullOrEmpty(targetAssetPath))
                        {
                            targetAsset = AssetDatabase.LoadAssetAtPath<Texture2D>(targetAssetPath);
                            if (targetAsset == null)
                                throw new ArgumentException($"Failed to find a valid {nameof(Texture2D)} asset at the specified path: '{targetAssetPath}'.");
                            parameters.TargetAsset = targetAsset;
                        }

                        generationHandle = AssetGenerators.GenerateSpritesheetAsync(parameters, settings);
                        break;
                    }
                }

                // Note: ValidationTask (quote/placeholder) is intentionally NOT timeout-bounded in
                // this PR — the observed hangs were always in generation/download, never validation.
                // Bounding it (needs a non-generic Task overload) is a candidate for the Part 2 follow-up.
                await generationHandle.ValidationTask;
                if (generationHandle.Messages.Any())
                {
                    var errorMessages = string.Join("\n", generationHandle.Messages);
                    throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                }

                var placeholder = generationHandle.Placeholder;
                if (placeholder == null)
                    throw new Exception(Constants.FailedToCreatePlaceholder);

                InterruptedDownloadResumer.TrackDownload(generationHandle);

                var timeout = assetType == AssetTypes.Mesh ? k_MeshGenerateAssetTimeout : k_GenerateAssetTimeout;

                GenerateAssetOutput InProgressOutput()
                {
                    var message = targetAsset != null
                        ? $"Modification for {assetType} '{placeholder.name}' is in progress."
                        : $"Placeholder for {assetType} '{placeholder.name}' created. Generation is in progress.";

                    var placeholderPath = AssetDatabase.GetAssetPath(placeholder);
                    return new GenerateAssetOutput
                    {
                        Message = message,
                        AssetName = placeholder.name,
                        AssetPath = placeholderPath,
                        AssetGuid = AssetDatabase.AssetPathToGUID(placeholderPath),
                        AssetType = assetType,
#if UNITY_6000_5_OR_NEWER
                        FileInstanceID = (long)EntityId.ToULong(placeholder.GetEntityId()),
#else
                        FileInstanceID = placeholder.GetInstanceID(),
#endif
                        SubObjectInstanceID = GetOutputInstanceId(placeholder, assetType)
                    };
                }

                if (!waitForCompletion)
                {
                    await TaskTimeout.WaitWithTimeout(generationHandle.GenerationTask, timeout,
                        $"{assetType} generation timed out after {timeout.TotalMinutes:n0} min.");
                    if (generationHandle.Messages.Any())
                    {
                        var errorMessages = string.Join("\n", generationHandle.Messages);
                        throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                    }

                    return InProgressOutput();
                }

                Object finalAsset;
                try
                {
                    finalAsset = await TaskTimeout.WaitWithTimeout(generationHandle.DownloadTask, timeout,
                        $"{assetType} generation timed out after {timeout.TotalMinutes:n0} min.");
                }
                catch (TimeoutException ex)
                {
                    Debug.LogWarning($"[GenerateAsset] Timeout safety net tripped (UUM-144682): {ex.Message} Generation continues in the background; returning an in-progress placeholder so the chat turn can continue.");
                    return InProgressOutput();
                }

                if (finalAsset == null || generationHandle.Messages.Any())
                {
                    var errorMessages = string.Join("\n", generationHandle.Messages);
                    throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                }

                // Defer ping after import to avoid conflicts with rapid asset switching
                EditorTask.delayCall += () =>
                {
                    if (finalAsset != null)
                        EditorGUIUtility.PingObject(finalAsset);
                };

                var finalMessage = targetAsset != null
                    ? $"Modification for {assetType} '{finalAsset.name}' completed successfully."
                    : $"{assetType} asset '{finalAsset.name}' generated successfully.";

                var finalAssetPath = AssetDatabase.GetAssetPath(finalAsset);
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(assetType.ToString(), "completed", null);
                return new GenerateAssetOutput
                {
                    Message = finalMessage,
                    AssetName = finalAsset.name,
                    AssetPath = finalAssetPath,
                    AssetGuid = AssetDatabase.AssetPathToGUID(finalAssetPath),
                    AssetType = assetType,
#if UNITY_6000_5_OR_NEWER
                    FileInstanceID = (long)EntityId.ToULong(finalAsset.GetEntityId()),
#else
                    FileInstanceID = finalAsset.GetInstanceID(),
#endif
                    SubObjectInstanceID = GetOutputInstanceId(finalAsset, assetType)
                };
            }
            catch (Exception ex)
            {
                if (ex is TimeoutException)
                    Debug.LogWarning($"[GenerateAsset] Timeout safety net tripped (UUM-144682): {ex.Message} Returning a failure so the chat turn can continue instead of hanging.");
                Debug.LogException(ex);
                var failedAssetType = assetType != AssetTypes.None ? assetType.ToString() : command.ToString();
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(failedAssetType, "failed", AIAssistantAnalytics.ClassifyGenerationFailure(ex));
                throw new Exception($"Error during asset operation: {ex.Message}", ex);
            }
        }

        internal static long GetOutputInstanceId(Object mainAsset, AssetTypes assetType)
        {
            if (mainAsset == null)
                return 0;

#if UNITY_6000_5_OR_NEWER
            var instanceId = (long)EntityId.ToULong(mainAsset.GetEntityId());
#else
            var instanceId = mainAsset.GetInstanceID();
#endif
            if (assetType is AssetTypes.Sprite or AssetTypes.Image or AssetTypes.Spritesheet)
            {
                var assetPath = AssetDatabase.GetAssetPath(mainAsset);
                var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath).OfType<Sprite>().ToArray();
                if (sprites.Length > 0)
                {
#if UNITY_6000_5_OR_NEWER
                    instanceId = (long)EntityId.ToULong(sprites[0].GetEntityId());
#else
                    instanceId = sprites[0].GetInstanceID();
#endif
                }
            }

            return instanceId;
        }

        [InitializeOnLoad]
        internal static class InterruptedDownloadResumer
        {
            static readonly string k_TokenFilePath = Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), "Temp", "ai_assistant_active_downloads_token");

            static int s_ActiveDownloads;

            static InterruptedDownloadResumer()
            {
                if (Application.isBatchMode || !File.Exists(k_TokenFilePath))
                    return;

                try
                {
                    var result = ManageInterruptedAssetGenerationsTool.ManageInterruptedAssetGenerations(ManageInterruptedAssetGenerationsCommands.Resume, true);

                    // If we tried to resume but found nothing, the token file was stale. Clean it up.
                    if (result.Generations == null || result.Generations.Length == 0)
                    {
                        RemoveToken();
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                    // If resuming fails, remove the token to prevent a resume loop on next reload.
                    RemoveToken();
                }
            }

            public static void TrackDownload(GenerationHandle<Object> generationHandle)
            {
                IncrementCounter();
                generationHandle.DownloadTask.ContinueWith(_ => DecrementCounter(), TaskScheduler.FromCurrentSynchronizationContext());
            }

            static void IncrementCounter()
            {
                s_ActiveDownloads++;
                if (s_ActiveDownloads > 0)
                {
                    CreateToken();
                }
            }

            static void DecrementCounter()
            {
                s_ActiveDownloads--;
                if (s_ActiveDownloads <= 0)
                {
                    s_ActiveDownloads = 0;
                    RemoveToken();
                }
            }

            [ToolPermissionIgnore]  // To ignore file creation permission check
            static void CreateToken()
            {
                try
                {
                    if (!File.Exists(k_TokenFilePath))
                        using (var _ = File.Create(k_TokenFilePath)) { }
                }
                catch
                {
                    // Error logging is optional and can be noisy.
                }
            }

            [ToolPermissionIgnore]  // To ignore file creation permission check
            static void RemoveToken()
            {
                try
                {
                    if (File.Exists(k_TokenFilePath))
                        File.Delete(k_TokenFilePath);
                }
                catch
                {
                    // Error logging is optional and can be noisy.
                }
            }
        }
    }
}
