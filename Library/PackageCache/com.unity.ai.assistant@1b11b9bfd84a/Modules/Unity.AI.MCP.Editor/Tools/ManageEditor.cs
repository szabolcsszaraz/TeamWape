using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement; // Required for PrefabStage
using UnityEditorInternal; // Required for tag management
using UnityEngine;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.ToolRegistry; // For Response class
using Unity.AI.MCP.Editor.Tools.Parameters;

namespace Unity.AI.MCP.Editor.Tools
{
    /// <summary>
    /// Handles operations related to controlling and querying the Unity Editor state,
    /// including managing Tags and Layers.
    /// </summary>
    public static class ManageEditor
    {
        /// <summary>
        /// Tool description for MCP tool registration, explaining the Unity.ManageEditor tool's capabilities
        /// </summary>
        public const string Title = "Control the Unity Editor";

        public const string Description = @"Controls and queries the Unity editor's state and settings.

Args:
    Action: Operation (e.g., 'Play', 'Pause', 'GetState', 'SetActiveTool', 'AddTag').
    WaitForCompletion: Optional. If True, waits for certain actions.
    Action-specific arguments (e.g., ToolName, TagName, LayerName).

Returns:
    Dictionary with operation results ('success', 'message', 'data').";
        // Constant for starting user layer index
        const int FirstUserLayerIndex = 8;

        // Constant for total layer count
        const int TotalLayerCount = 32;

        /// <summary>
        /// Returns the output schema for this tool.
        /// </summary>
        /// <returns>The JSON schema object describing the tool's output structure.</returns>
        [McpOutputSchema("Unity.ManageEditor")]
        public static object GetOutputSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    success = new { type = "boolean", description = "Whether the operation succeeded" },
                    message = new { type = "string", description = "Human-readable message about the operation" },
                    data = new
                    {
                        type = "object",
                        description = "Editor-specific operation data",
                        properties = new
                        {
                            // Editor state properties
                            isPlaying = new { type = "boolean", description = "Whether the editor is in play mode" },
                            isPaused = new { type = "boolean", description = "Whether the game is paused" },
                            isCompiling = new { type = "boolean", description = "Whether the editor is compiling" },
                            isUpdating = new { type = "boolean", description = "Whether the editor is updating" },
                            applicationPath = new { type = "string", description = "Path to Unity application" },
                            applicationContentsPath = new { type = "string", description = "Path to Unity application contents" },
                            timeSinceStartup = new { type = "number", description = "Time since Unity startup" },

                            // Project root
                            projectRoot = new { type = "string", description = "Full path to the project root directory" },

                            // Windows array
                            windows = new
                            {
                                type = "array",
                                description = "List of open editor windows",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        title = new { type = "string", description = "Window title" },
                                        typeName = new { type = "string", description = "Full type name of the window" },
                                        isFocused = new { type = "boolean", description = "Whether the window is currently focused" },
                                        instanceID = new { type = "integer", description = "Unity instance ID of the window" },
                                        position = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                x = new { type = "number", description = "X coordinate" },
                                                y = new { type = "number", description = "Y coordinate" },
                                                width = new { type = "number", description = "Width" },
                                                height = new { type = "number", description = "Height" }
                                            }
                                        }
                                    }
                                }
                            },

                            // Active tool
                            activeTool = new { type = "string", description = "Name of the active tool" },
                            isCustom = new { type = "boolean", description = "Whether a custom tool is active" },
                            pivotMode = new { type = "string", description = "Pivot mode setting" },
                            pivotRotation = new { type = "string", description = "Pivot rotation setting" },
                            handleRotation = new { type = "array", items = new { type = "number" }, description = "Handle rotation as euler angles" },
                            handlePosition = new { type = "array", items = new { type = "number" }, description = "Handle position" },

                            // Selection
                            activeObject = new { type = "string", description = "Name of active selected object" },
                            activeGameObject = new { type = "string", description = "Name of active selected GameObject" },
                            activeTransform = new { type = "string", description = "Name of active selected Transform" },
                            activeInstanceID = new { type = "integer", description = "Instance ID of active selection" },
                            count = new { type = "integer", description = "Total count of selected objects" },
                            objects = new
                            {
                                type = "array",
                                description = "List of all selected objects",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string", description = "Object name" },
                                        type = new { type = "string", description = "Full type name" },
                                        instanceID = new { type = "integer", description = "Unity instance ID" }
                                    }
                                }
                            },
                            gameObjects = new
                            {
                                type = "array",
                                description = "List of all selected GameObjects",
                                items = new
                                {
                                    type = "object",
                                    properties = new
                                    {
                                        name = new { type = "string", description = "GameObject name" },
                                        instanceID = new { type = "integer", description = "Unity instance ID" }
                                    }
                                }
                            },
                            assetGUIDs = new { type = "array", items = new { type = "string" }, description = "Asset GUIDs of selected assets in Project view" },

                            // Tags and layers
                            tags = new { type = "array", items = new { type = "string" }, description = "List of tags" },
                            layers = new
                            {
                                type = "object",
                                description = "Dictionary of layer indices and names",
                                additionalProperties = new { type = "string" }
                            },

                            // Prefab stage info
                            isOpen = new { type = "boolean", description = "Whether prefab stage is currently open" },
                            assetPath = new { type = "string", description = "Asset path of the prefab being edited" },
                            prefabRootName = new { type = "string", description = "Name of the prefab root GameObject" },
                            mode = new { type = "string", description = "Prefab stage mode (InContext or InIsolation)" },
                            isDirty = new { type = "boolean", description = "Whether the prefab has unsaved changes" }
                        }
                    }
                },
                required = new[] { "success", "message" }
            };
        }




        /// <summary>
        /// Main handler for editor management actions.
        /// </summary>
        /// <param name="parameters">The parameters specifying the action and related settings.</param>
        /// <returns>A response object containing success status, message, and optional data.</returns>
        [McpTool("Unity.ManageEditor", Description, Title, Groups = new string[] { "core", "editor" })]
        public static object HandleCommand(ManageEditorParams parameters)
        {
            var @params = parameters;

            // Parameters for specific actions
            string tagName = @params.TagName;
            string layerName = @params.LayerName;
            bool waitForCompletion = @params.WaitForCompletion ?? false;

            // Route action
            switch (@params.Action)
            {
                // Play Mode Control
                case EditorAction.Play:
                    try
                    {
                        if (!EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = true;
                            return Response.Success("Entered play mode.");
                        }
                        return Response.Success("Already in play mode.");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error entering play mode: {e.Message}");
                    }
                case EditorAction.Pause:
                    try
                    {
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPaused = !EditorApplication.isPaused;
                            return Response.Success(
                                EditorApplication.isPaused ? "Game paused." : "Game resumed."
                            );
                        }
                        return Response.Error("Cannot pause/resume: Not in play mode.");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error pausing/resuming game: {e.Message}");
                    }
                case EditorAction.Stop:
                    try
                    {
                        if (EditorApplication.isPlaying)
                        {
                            EditorApplication.isPlaying = false;
                            return Response.Success("Exited play mode.");
                        }
                        return Response.Success("Already stopped (not in play mode).");
                    }
                    catch (Exception e)
                    {
                        return Response.Error($"Error stopping play mode: {e.Message}");
                    }

                // Editor State/Info
                case EditorAction.GetState:
                    return GetEditorState();
                case EditorAction.GetProjectRoot:
                    return GetProjectRoot();
                case EditorAction.GetWindows:
                    return GetEditorWindows();
                case EditorAction.GetActiveTool:
                    return GetActiveTool();
                case EditorAction.GetSelection:
                    return GetSelection();
                case EditorAction.GetPrefabStage:
                    return GetPrefabStageInfo();
                case EditorAction.SetActiveTool:
                    string toolName = @params.ToolName;
                    if (string.IsNullOrEmpty(toolName))
                        return Response.Error("'ToolName' parameter required for SetActiveTool.");
                    return SetActiveTool(toolName);

                // Tag Management
                case EditorAction.AddTag:
                    if (string.IsNullOrEmpty(tagName))
                        return Response.Error("'tagName' parameter required for add_tag.");
                    return AddTag(tagName);
                case EditorAction.RemoveTag:
                    if (string.IsNullOrEmpty(tagName))
                        return Response.Error("'tagName' parameter required for remove_tag.");
                    return RemoveTag(tagName);
                case EditorAction.GetTags:
                    return GetTags(); // Helper to list current tags

                // Layer Management
                case EditorAction.AddLayer:
                    if (string.IsNullOrEmpty(layerName))
                        return Response.Error("'layerName' parameter required for add_layer.");
                    return AddLayer(layerName);
                case EditorAction.RemoveLayer:
                    if (string.IsNullOrEmpty(layerName))
                        return Response.Error("'layerName' parameter required for remove_layer.");
                    return RemoveLayer(layerName);
                case EditorAction.GetLayers:
                    return GetLayers(); // Helper to list current layers

                // --- Settings (Example) ---
                // case "set_resolution":
                //     int? width = @params["width"]?.ToObject<int?>();
                //     int? height = @params["height"]?.ToObject<int?>();
                //     if (!width.HasValue || !height.HasValue) return Response.Error("'width' and 'height' parameters required.");
                //     return SetGameViewResolution(width.Value, height.Value);
                // case "set_quality":
                //     // Handle string name or int index
                //     return SetQualityLevel(@params["qualityLevel"]);

                default:
                    return Response.Error(
                        $"Unknown action: '{@params.Action}'. Supported actions include Play, Pause, Stop, GetState, GetProjectRoot, GetWindows, GetActiveTool, GetSelection, GetPrefabStage, SetActiveTool, AddTag, RemoveTag, GetTags, AddLayer, RemoveLayer, GetLayers."
                    );
            }
        }

        // --- Editor State/Info Methods ---
        static object GetEditorState()
        {
            try
            {
                var state = new EditorStateData
                {
                    IsPlaying = EditorApplication.isPlaying,
                    IsPaused = EditorApplication.isPaused,
                    IsCompiling = EditorApplication.isCompiling,
                    IsUpdating = EditorApplication.isUpdating,
                    ApplicationPath = EditorApplication.applicationPath,
                    ApplicationContentsPath = EditorApplication.applicationContentsPath,
                    TimeSinceStartup = EditorApplication.timeSinceStartup,
                };

                return Response.Success("Retrieved editor state.", state);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor state: {e.Message}");
            }
        }

        static object GetProjectRoot()
        {
            try
            {
                // Application.dataPath points to <Project>/Assets
                string assetsPath = Application.dataPath.Replace('\\', '/');
                string projectRoot = Directory.GetParent(assetsPath)?.FullName.Replace('\\', '/');
                if (string.IsNullOrEmpty(projectRoot))
                {
                    return Response.Error("Could not determine project root from Application.dataPath");
                }

                var data = new { projectRoot };

                return Response.Success("Project root resolved.", data);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting project root: {e.Message}");
            }
        }

        static object GetEditorWindows()
        {
            try
            {
                var openWindows = new List<EditorWindowInfo>();

                // Find currently open instances
                // Resources.FindObjectsOfTypeAll seems more reliable than GetWindow for finding *all* open windows
                EditorWindow[] allWindows = Resources.FindObjectsOfTypeAll<EditorWindow>();

                foreach (EditorWindow window in allWindows)
                {
                    if (window == null)
                        continue; // Skip potentially destroyed windows

                    try
                    {
                        openWindows.Add(
                            new EditorWindowInfo
                            {
                                Title = window.titleContent.text,
                                TypeName = window.GetType().FullName,
                                IsFocused = EditorWindow.focusedWindow == window,
                                Position = new WindowPosition
                                {
                                    X = window.position.x,
                                    Y = window.position.y,
                                    Width = window.position.width,
                                    Height = window.position.height,
                                },
#if UNITY_6000_5_OR_NEWER
                                InstanceID = (long)EntityId.ToULong(window.GetEntityId()),
#else
                                InstanceID = window.GetInstanceID(),
#endif
                            }
                        );
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning(
                            $"Could not get info for window {window.GetType().Name}: {ex.Message}"
                        );
                    }
                }

                return Response.Success("Retrieved list of open editor windows.", openWindows);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting editor windows: {e.Message}");
            }
        }

        static object GetActiveTool()
        {
            try
            {
                Tool currentTool = UnityEditor.Tools.current;
                string toolName = currentTool.ToString(); // Enum to string
                bool customToolActive = UnityEditor.Tools.current == Tool.Custom; // Check if a custom tool is active
                string activeToolName = customToolActive
                    ? EditorTools.GetActiveToolName()
                    : toolName; // Get custom name if needed

                var toolInfo = new ActiveToolData
                {
                    ActiveTool = activeToolName,
                    IsCustom = customToolActive,
                    PivotMode = UnityEditor.Tools.pivotMode.ToString(),
                    PivotRotation = UnityEditor.Tools.pivotRotation.ToString(),
                    HandleRotation = new float[] { UnityEditor.Tools.handleRotation.eulerAngles.x, UnityEditor.Tools.handleRotation.eulerAngles.y, UnityEditor.Tools.handleRotation.eulerAngles.z },
                    HandlePosition = new float[] { UnityEditor.Tools.handlePosition.x, UnityEditor.Tools.handlePosition.y, UnityEditor.Tools.handlePosition.z },
                };

                return Response.Success("Retrieved active tool information.", toolInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting active tool: {e.Message}");
            }
        }

        static object SetActiveTool(string toolName)
        {
            try
            {
                Tool targetTool;
                if (Enum.TryParse<Tool>(toolName, true, out targetTool)) // Case-insensitive parse
                {
                    // Check if it's a valid built-in tool
                    if (targetTool != Tool.None && targetTool <= Tool.Custom) // Tool.Custom is the last standard tool
                    {
                        UnityEditor.Tools.current = targetTool;
                        return Response.Success($"Set active tool to '{targetTool}'.");
                    }
                    else
                    {
                        return Response.Error(
                            $"Cannot directly set tool to '{toolName}'. It might be None, Custom, or invalid."
                        );
                    }
                }
                else
                {
                    // Potentially try activating a custom tool by name here if needed
                    // This often requires specific editor scripting knowledge for that tool.
                    return Response.Error(
                        $"Could not parse '{toolName}' as a standard Unity Tool (View, Move, Rotate, Scale, Rect, Transform, Custom)."
                    );
                }
            }
            catch (Exception e)
            {
                return Response.Error($"Error setting active tool: {e.Message}");
            }
        }

        static object GetSelection()
        {
            try
            {
                var selectionInfo = new SelectionData
                {
                    ActiveObject = Selection.activeObject?.name,
                    ActiveGameObject = Selection.activeGameObject?.name,
                    ActiveTransform = Selection.activeTransform?.name,
                    ActiveInstanceID = UnityApiAdapter.GetActiveSelectionId(),
                    Count = Selection.count,
                    Objects = Selection
                        .objects.Select(obj => new SelectionObjectInfo
                        {
                            Name = obj?.name,
                            Type = obj?.GetType().FullName,
#if UNITY_6000_5_OR_NEWER
                            InstanceID = obj != null ? (long?)((long)EntityId.ToULong(obj.GetEntityId())) : null,
#else
                            InstanceID = obj?.GetInstanceID(),
#endif
                        })
                        .ToList(),
                    GameObjects = Selection
                        .gameObjects.Select(go => new GameObjectSelectionInfo
                        {
                            Name = go?.name,
#if UNITY_6000_5_OR_NEWER
                            InstanceID = go != null ? (long?)((long)EntityId.ToULong(go.GetEntityId())) : null,
#else
                            InstanceID = go?.GetInstanceID(),
#endif
                        })
                        .ToList(),
                    AssetGUIDs = Selection.assetGUIDs, // GUIDs for selected assets in Project view
                };

                return Response.Success("Retrieved current selection details.", selectionInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting selection: {e.Message}");
            }
        }

        static object GetPrefabStageInfo()
        {
            try
            {
                PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
                if (stage == null)
                {
                    var closedStageInfo = new PrefabStageData
                    {
                        IsOpen = false,
                        AssetPath = null,
                        PrefabRootName = null,
                        Mode = null,
                        IsDirty = false
                    };
                    return Response.Success("No prefab stage is currently open.", closedStageInfo);
                }

                var stageInfo = new PrefabStageData
                {
                    IsOpen = true,
                    AssetPath = stage.assetPath,
                    PrefabRootName = stage.prefabContentsRoot?.name,
                    Mode = stage.mode.ToString(),
                    IsDirty = stage.scene.isDirty
                };

                return Response.Success("Prefab stage info retrieved.", stageInfo);
            }
            catch (Exception e)
            {
                return Response.Error($"Error getting prefab stage info: {e.Message}");
            }
        }

        // --- Tag Management Methods ---

        static object AddTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");

            // Check if tag already exists
            if (InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' already exists.");
            }

            try
            {
                // Add the tag using the internal utility
                InternalEditorUtility.AddTag(tagName);
                // Force save assets to ensure the change persists in the TagManager asset
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' added successfully.");
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add tag '{tagName}': {e.Message}");
            }
        }

        static object RemoveTag(string tagName)
        {
            if (string.IsNullOrWhiteSpace(tagName))
                return Response.Error("Tag name cannot be empty or whitespace.");
            if (tagName.Equals("Untagged", StringComparison.OrdinalIgnoreCase))
                return Response.Error("Cannot remove the built-in 'Untagged' tag.");

            // Check if tag exists before attempting removal
            if (!InternalEditorUtility.tags.Contains(tagName))
            {
                return Response.Error($"Tag '{tagName}' does not exist.");
            }

            try
            {
                // Remove the tag using the internal utility
                InternalEditorUtility.RemoveTag(tagName);
                // Force save assets
                AssetDatabase.SaveAssets();
                return Response.Success($"Tag '{tagName}' removed successfully.");
            }
            catch (Exception e)
            {
                // Catch potential issues if the tag is somehow in use or removal fails
                return Response.Error($"Failed to remove tag '{tagName}': {e.Message}");
            }
        }

        static object GetTags()
        {
            try
            {
                string[] tags = InternalEditorUtility.tags;
                return Response.Success("Retrieved current tags.", tags);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve tags: {e.Message}");
            }
        }

        // --- Layer Management Methods ---

        static object AddLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Check if layer name already exists (case-insensitive check recommended)
            for (int i = 0; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    return Response.Error($"Layer '{layerName}' already exists at index {i}.");
                }
            }

            // Find the first empty user layer slot (indices 8 to 31)
            int firstEmptyUserLayer = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++)
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                if (layerSP != null && string.IsNullOrEmpty(layerSP.stringValue))
                {
                    firstEmptyUserLayer = i;
                    break;
                }
            }

            if (firstEmptyUserLayer == -1)
            {
                return Response.Error("No empty User Layer slots available (8-31 are full).");
            }

            // Assign the name to the found slot
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    firstEmptyUserLayer
                );
                targetLayerSP.stringValue = layerName;
                // Apply the changes to the TagManager asset
                tagManager.ApplyModifiedProperties();
                // Save assets to make sure it's written to disk
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' added successfully to slot {firstEmptyUserLayer}."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to add layer '{layerName}': {e.Message}");
            }
        }

        static object RemoveLayer(string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName))
                return Response.Error("Layer name cannot be empty or whitespace.");

            // Access the TagManager asset
            SerializedObject tagManager = GetTagManager();
            if (tagManager == null)
                return Response.Error("Could not access TagManager asset.");

            SerializedProperty layersProp = tagManager.FindProperty("layers");
            if (layersProp == null || !layersProp.isArray)
                return Response.Error("Could not find 'layers' property in TagManager.");

            // Find the layer by name (must be user layer)
            int layerIndexToRemove = -1;
            for (int i = FirstUserLayerIndex; i < TotalLayerCount; i++) // Start from user layers
            {
                SerializedProperty layerSP = layersProp.GetArrayElementAtIndex(i);
                // Case-insensitive comparison is safer
                if (
                    layerSP != null
                    && layerName.Equals(layerSP.stringValue, StringComparison.OrdinalIgnoreCase)
                )
                {
                    layerIndexToRemove = i;
                    break;
                }
            }

            if (layerIndexToRemove == -1)
            {
                return Response.Error($"User layer '{layerName}' not found.");
            }

            // Clear the name for that index
            try
            {
                SerializedProperty targetLayerSP = layersProp.GetArrayElementAtIndex(
                    layerIndexToRemove
                );
                targetLayerSP.stringValue = string.Empty; // Set to empty string to remove
                // Apply the changes
                tagManager.ApplyModifiedProperties();
                // Save assets
                AssetDatabase.SaveAssets();
                return Response.Success(
                    $"Layer '{layerName}' (slot {layerIndexToRemove}) removed successfully."
                );
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to remove layer '{layerName}': {e.Message}");
            }
        }

        static object GetLayers()
        {
            try
            {
                var layers = new Dictionary<int, string>();
                for (int i = 0; i < TotalLayerCount; i++)
                {
                    string layerName = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(layerName)) // Only include layers that have names
                    {
                        layers.Add(i, layerName);
                    }
                }

                return Response.Success("Retrieved current named layers.", layers);
            }
            catch (Exception e)
            {
                return Response.Error($"Failed to retrieve layers: {e.Message}");
            }
        }

        // --- Helper Methods ---

        /// <summary>
        /// Gets the SerializedObject for the TagManager asset.
        /// </summary>
        static SerializedObject GetTagManager()
        {
            try
            {
                // Load the TagManager asset from the ProjectSettings folder
                UnityEngine.Object[] tagManagerAssets = AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/TagManager.asset"
                );
                if (tagManagerAssets == null || tagManagerAssets.Length == 0)
                {
                    Debug.LogError("[ManageEditor] TagManager.asset not found in ProjectSettings.");
                    return null;
                }
                // The first object in the asset file should be the TagManager
                return new SerializedObject(tagManagerAssets[0]);
            }
            catch (Exception e)
            {
                Debug.LogError($"[ManageEditor] Error accessing TagManager.asset: {e.Message}");
                return null;
            }
        }

        // --- Example Implementations for Settings ---
        /*
        private static object SetGameViewResolution(int width, int height) { ... }
        private static object SetQualityLevel(JToken qualityLevelToken) { ... }
        */
    }

    // Helper class to get custom tool names (remains the same)
    static class EditorTools
    {
        public static string GetActiveToolName()
        {
            // This is a placeholder. Real implementation depends on how custom tools
            // are registered and tracked in the specific Unity project setup.
            // It might involve checking static variables, calling methods on specific tool managers, etc.
            if (UnityEditor.Tools.current == Tool.Custom)
            {
                // Example: Check a known custom tool manager
                // if (MyCustomToolManager.IsActive) return MyCustomToolManager.ActiveToolName;
                return "Unknown Custom Tool";
            }
            return UnityEditor.Tools.current.ToString();
        }
    }
}

