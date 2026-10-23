using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Generators.Tools;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class ConvertAssetTool
    {
        static void ThrowIfAssetIsGenerating(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return;
            var interruptedAssetPaths = AssetGenerators.GetAllDownloadAssets()
                .Select(AssetDatabase.GetAssetPath)
                .ToList();
            if (interruptedAssetPaths.Contains(assetPath))
                throw new AssetGenerationBusyException($"Asset {assetPath} is still being generated, check back later.");
        }

        internal const string k_ConvertToMaterialFunctionId = "Unity.AssetGeneration.ConvertToMaterial";

        [AgentTool(
            "Creates a simple Material asset from an existing Texture2D or Cubemap asset (non-generatively). " +
            "This conversion does NOT use any generative models — it simply creates a Material and assigns the provided texture. " +
            "Provide the target asset path pointing to a Texture2D or a Cubemap. Provide the save path for the new .mat (required).",
            k_ConvertToMaterialFunctionId)]
        [AgentToolSettings(mcp: McpAvailability.Available, tags: Constants.AssetGenerationFunctionTag)]
        public static async Task<GenerateAssetOutput> ConvertToMaterial(
            ToolExecutionContext context,
            [ToolParameter(Constants.ReferenceImagePathDescription)]
            string referenceImagePath,
            [ToolParameter(Constants.SavePathDescription)]
            string savePath)
        {
            try
            {
                if (string.IsNullOrEmpty(referenceImagePath))
                    throw new ArgumentException($"'{nameof(referenceImagePath)}' is required and must point to an existing {nameof(Texture2D)} or {nameof(Cubemap)}.");

                ThrowIfAssetIsGenerating(referenceImagePath);

                if (string.IsNullOrEmpty(savePath))
                    throw new ArgumentException($"'{nameof(savePath)}' parameter is required and must include the desired .mat path (e.g. Assets/Materials/MyMaterial.mat).");

                var directory = Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(directory))
                    throw new ArgumentException($"'{nameof(savePath)}' must include a directory. For example 'Assets/Materials/MyNewMaterial.mat'.");

                var settings = new MaterialSettings { ImageReferences = new[] { new ObjectReference { Image = AssetDatabase.LoadAssetAtPath<Object>(referenceImagePath) } }};
                var handle = AssetGenerators.ConvertToMaterialAsync(settings, savePath, null, (path, cost) => context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, path), CancellationToken.None);

                var assetType = AssetTypes.Material;
                var finalAsset = await handle;
                if (finalAsset == null || handle.Messages.Any())
                {
                    var errorMessages = string.Join("\n", handle.Messages);
                    throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                }

                // Defer ping after import to avoid conflicts with rapid asset switching
                EditorTask.delayCall += () =>
                {
                    if (finalAsset != null)
                        EditorGUIUtility.PingObject(finalAsset);
                };

                var finalMessage = $"{assetType} asset '{finalAsset.name}' generated successfully.";
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
                    SubObjectInstanceID = GenerateAssetTool.GetOutputInstanceId(finalAsset, assetType)
                };
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(AssetTypes.Material.ToString(), "failed", AIAssistantAnalytics.ClassifyGenerationFailure(ex));
                throw new Exception($"Error during ConvertToMaterial operation: {ex.Message}", ex);
            }
        }

        internal const string k_ConvertToTerrainLayerFunctionId = "Unity.AssetGeneration.ConvertToTerrainLayer";

        [AgentTool(
            "Creates a simple TerrainLayer asset from an existing Texture2D asset (non-generatively). " +
            "This conversion does NOT use any generative models — it simply creates a TerrainLayer and assigns the provided texture. " +
            "Provide the reference image path pointing to a Texture2D. Provide the save path for the new .terrainlayer (required).",
            k_ConvertToTerrainLayerFunctionId)]
        [AgentToolSettings(mcp: McpAvailability.Available, tags: Constants.AssetGenerationFunctionTag)]
        public static async Task<GenerateAssetOutput> ConvertToTerrainLayer(
            ToolExecutionContext context,
            [ToolParameter(Constants.ReferenceImagePathDescription)]
            string referenceImagePath,
            [ToolParameter(Constants.SavePathDescription)]
            string savePath)
        {
            try
            {
                if (string.IsNullOrEmpty(referenceImagePath))
                    throw new ArgumentException($"'{nameof(referenceImagePath)}' is required and must point to an existing {nameof(Texture2D)}.");

                ThrowIfAssetIsGenerating(referenceImagePath);

                if (string.IsNullOrEmpty(savePath))
                    throw new ArgumentException($"'{nameof(savePath)}' parameter is required and must include the desired .terrainlayer path (e.g. Assets/TerrainLayers/MyLayer.terrainlayer).");

                var directory = Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(directory))
                    throw new ArgumentException($"'{nameof(savePath)}' must include a directory. For example 'Assets/TerrainLayers/MyLayer.terrainlayer'.");

                var settings = new TerrainLayerSettings { ImageReferences = new[] { new ObjectReference { Image = AssetDatabase.LoadAssetAtPath<Object>(referenceImagePath) } }};
                var handle = AssetGenerators.ConvertToTerrainLayerAsync(settings, savePath, null, (path, cost) => context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, path), CancellationToken.None);

                var assetType = AssetTypes.TerrainLayer;
                var finalAsset = await handle;
                if (finalAsset == null || handle.Messages.Any())
                {
                    var errorMessages = string.Join("\n", handle.Messages);
                    throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                }

                // Defer ping after import to avoid conflicts with rapid asset switching
                EditorTask.delayCall += () =>
                {
                    if (finalAsset != null)
                        EditorGUIUtility.PingObject(finalAsset);
                };

                var finalMessage = $"{assetType} asset '{finalAsset.name}' generated successfully.";
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
                    SubObjectInstanceID = GenerateAssetTool.GetOutputInstanceId(finalAsset, assetType)
                };
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(AssetTypes.TerrainLayer.ToString(), "failed", AIAssistantAnalytics.ClassifyGenerationFailure(ex));
                throw new Exception($"Error during ConvertToTerrainLayer operation: {ex.Message}", ex);
            }
        }

        internal const string k_ConvertSpriteSheetToAnimationClipFunctionId = "Unity.AssetGeneration.ConvertSpriteSheetToAnimationClip";

        [AgentTool(
            "Creates an AnimationClip asset from an existing Texture2D sprite sheet asset (non-generatively). " +
            "This conversion does NOT use any generative models — it creates an AnimationClip from the sliced sprites in the provided texture. " +
            "Provide the reference image path pointing to a pre-sliced Texture2D. Provide the save path for the new .anim file (required).",
            k_ConvertSpriteSheetToAnimationClipFunctionId)]
        [AgentToolSettings(mcp: McpAvailability.Available, tags: Constants.AssetGenerationFunctionTag)]
        public static async Task<GenerateAssetOutput> ConvertSpriteSheetToAnimationClip(
            ToolExecutionContext context,
            [ToolParameter(Constants.ReferenceImagePathDescription)]
            string referenceImagePath,
            [ToolParameter(Constants.SavePathDescription)]
            string savePath)
        {
            try
            {
                if (string.IsNullOrEmpty(referenceImagePath))
                    throw new ArgumentException($"'{nameof(referenceImagePath)}' is required and must point to an existing, pre-sliced {nameof(Texture2D)} sprite sheet.");

                ThrowIfAssetIsGenerating(referenceImagePath);

                if (string.IsNullOrEmpty(savePath))
                    throw new ArgumentException($"'{nameof(savePath)}' parameter is required and must include the desired .anim path (e.g. Assets/Animations/MyAnimation.anim).");

                var directory = Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(directory))
                    throw new ArgumentException($"'{nameof(savePath)}' must include a directory. For example 'Assets/Animations/MyAnimation.anim'.");

                var sourceSpriteSheet = AssetDatabase.LoadAssetAtPath<Texture2D>(referenceImagePath);
                if (sourceSpriteSheet == null)
                    throw new FileNotFoundException($"The source sprite sheet could not be found at the provided '{nameof(referenceImagePath)}'.", referenceImagePath);

                var handle = AssetGenerators.ConvertSpriteSheetToAnimationClipAsync(sourceSpriteSheet, savePath, (path, cost) => context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, path), CancellationToken.None);

                var assetType = AssetTypes.SpriteAnimation;
                var finalAsset = await handle;
                if (finalAsset == null || handle.Messages.Any())
                {
                    var errorMessages = string.Join("\n", handle.Messages);
                    throw new Exception($"{assetType} operation failed. Details: {errorMessages}");
                }

                // Defer ping after import to avoid conflicts with rapid asset switching
                EditorTask.delayCall += () =>
                {
                    if (finalAsset != null)
                        EditorGUIUtility.PingObject(finalAsset);
                };

                var finalMessage = $"{assetType} asset '{finalAsset.name}' generated successfully.";
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
                    SubObjectInstanceID = GenerateAssetTool.GetOutputInstanceId(finalAsset, assetType)
                };
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(AssetTypes.SpriteAnimation.ToString(), "failed", AIAssistantAnalytics.ClassifyGenerationFailure(ex));
                throw new Exception($"Error during ConvertSpriteSheetToAnimationClip operation: {ex.Message}", ex);
            }
        }

        internal const string k_CreateAnimatorControllerFromClipFunctionId = "Unity.AssetGeneration.CreateAnimatorControllerFromClip";

        [AgentTool(
            "Creates an AnimatorController asset from an existing AnimationClip asset. " +
            "This tool sets the provided AnimationClip as the default state in the new controller's state machine. " +
            "Provide the animation clip path pointing to an existing .anim asset and the save path for the new .controller file (required).",
            k_CreateAnimatorControllerFromClipFunctionId)]
        [AgentToolSettings(mcp: McpAvailability.Available, tags: Constants.AssetGenerationFunctionTag)]
        public static async Task<GenerateAssetOutput> CreateAnimatorControllerFromClip(
            ToolExecutionContext context,
            [ToolParameter("The project path to the source AnimationClip asset (e.g., 'Assets/Animations/MyClip.anim').")]
            string animationClipPath,
            [ToolParameter("The full project path where the new AnimatorController asset should be saved (e.g., 'Assets/Animations/MyController.controller').")]
            string savePath)
        {
            try
            {
                if (string.IsNullOrEmpty(animationClipPath))
                    throw new ArgumentException($"'{nameof(animationClipPath)}' is required and must point to an existing {nameof(AnimationClip)} asset.");

                ThrowIfAssetIsGenerating(animationClipPath);

                if (string.IsNullOrEmpty(savePath))
                    throw new ArgumentException($"'{nameof(savePath)}' parameter is required and must include the desired .controller path.");

                var sourceClip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationClipPath);
                if (sourceClip == null)
                    throw new FileNotFoundException($"The source {nameof(AnimationClip)} could not be found at the provided '{nameof(animationClipPath)}'.", animationClipPath);

                var directory = Path.GetDirectoryName(savePath);
                if (string.IsNullOrEmpty(directory))
                    throw new ArgumentException($"'{nameof(savePath)}' must include a directory. For example 'Assets/Controllers/MyController.controller'.");

                await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, savePath);

                // Create the Animator Controller at the specified path.
                var controller = AnimatorController.CreateAnimatorControllerAtPath(savePath);
                if (controller == null)
                    throw new Exception($"Failed to create {nameof(AnimatorController)} asset.");

                // Add the source clip as the motion for the default state in the base layer.
                controller.AddMotion(sourceClip);

                // Save the newly created and modified asset.
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssetIfDirty(controller);

                // --- 3. Return Success Output ---
                var finalAsset = AssetDatabase.LoadAssetAtPath<AnimatorController>(savePath);
                // Defer ping after import to avoid conflicts with rapid asset switching
                EditorTask.delayCall += () =>
                {
                    if (finalAsset != null)
                        EditorGUIUtility.PingObject(finalAsset);
                };

                var finalMessage = $"AnimatorController asset '{finalAsset.name}' generated successfully.";
                var finalAssetPath = AssetDatabase.GetAssetPath(finalAsset);
                var output = new GenerateAssetOutput
                {
                    Message = finalMessage,
                    AssetName = finalAsset.name,
                    AssetPath = finalAssetPath,
                    AssetGuid = AssetDatabase.AssetPathToGUID(finalAssetPath),
                    AssetType = AssetTypes.AnimatorController,
#if UNITY_6000_5_OR_NEWER
                    FileInstanceID = (long)EntityId.ToULong(finalAsset.GetEntityId()),
#else
                    FileInstanceID = finalAsset.GetInstanceID(),
#endif
#if UNITY_6000_5_OR_NEWER
                    SubObjectInstanceID = (long)EntityId.ToULong(finalAsset.GetEntityId())
#else
                    SubObjectInstanceID = finalAsset.GetInstanceID()
#endif
                };
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(AssetTypes.AnimatorController.ToString(), "completed", null);
                return output;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                AIAssistantAnalytics.ReportGatewayGenerationResultEvent(AssetTypes.AnimatorController.ToString(), "failed", AIAssistantAnalytics.ClassifyGenerationFailure(ex));
                throw new Exception($"Error during CreateAnimatorControllerFromClip operation: {ex.Message}", ex);
            }
        }
    }
}
