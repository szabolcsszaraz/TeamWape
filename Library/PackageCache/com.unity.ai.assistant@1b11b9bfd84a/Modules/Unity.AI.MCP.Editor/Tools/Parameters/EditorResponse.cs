using System;
using System.Collections.Generic;
using Unity.AI.MCP.Editor.ToolRegistry;

namespace Unity.AI.MCP.Editor.Tools.Parameters
{
    /// <summary>
    /// Editor state information data structure.
    /// </summary>
    public record EditorStateData
    {
        /// <summary>
        /// Gets or sets whether the editor is in play mode.
        /// </summary>
        [McpDescription("Whether the editor is in play mode")]
        public bool IsPlaying { get; set; }

        /// <summary>
        /// Gets or sets whether the game is paused.
        /// </summary>
        [McpDescription("Whether the game is paused")]
        public bool IsPaused { get; set; }

        /// <summary>
        /// Gets or sets whether the editor is compiling.
        /// </summary>
        [McpDescription("Whether the editor is compiling")]
        public bool IsCompiling { get; set; }

        /// <summary>
        /// Gets or sets whether the editor is updating.
        /// </summary>
        [McpDescription("Whether the editor is updating")]
        public bool IsUpdating { get; set; }

        /// <summary>
        /// Gets or sets the path to Unity application.
        /// </summary>
        [McpDescription("Path to Unity application")]
        public string ApplicationPath { get; set; }

        /// <summary>
        /// Gets or sets the path to Unity application contents.
        /// </summary>
        [McpDescription("Path to Unity application contents")]
        public string ApplicationContentsPath { get; set; }

        /// <summary>
        /// Gets or sets the time since Unity startup in seconds.
        /// </summary>
        [McpDescription("Time since Unity startup")]
        public double TimeSinceStartup { get; set; }
    }


    /// <summary>
    /// Editor window information data structure.
    /// </summary>
    public record EditorWindowInfo
    {
        /// <summary>
        /// Gets or sets the window title.
        /// </summary>
        [McpDescription("Window title")]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the full type name of the window.
        /// </summary>
        [McpDescription("Full type name of the window")]
        public string TypeName { get; set; }

        /// <summary>
        /// Gets or sets whether the window is currently focused.
        /// </summary>
        [McpDescription("Whether the window is currently focused")]
        public bool IsFocused { get; set; }

        /// <summary>
        /// Gets or sets the window position and size.
        /// </summary>
        [McpDescription("Window position and size")]
        public WindowPosition Position { get; set; }

        /// <summary>
        /// Gets or sets the Unity instance ID of the window.
        /// </summary>
        [McpDescription("Unity instance ID of the window")]
        public long InstanceID { get; set; }
    }

    /// <summary>
    /// Window position and size data structure.
    /// </summary>
    public record WindowPosition
    {
        /// <summary>
        /// Gets or sets the X coordinate of the window.
        /// </summary>
        [McpDescription("X coordinate")]
        public float X { get; set; }

        /// <summary>
        /// Gets or sets the Y coordinate of the window.
        /// </summary>
        [McpDescription("Y coordinate")]
        public float Y { get; set; }

        /// <summary>
        /// Gets or sets the width of the window.
        /// </summary>
        [McpDescription("Width")]
        public float Width { get; set; }

        /// <summary>
        /// Gets or sets the height of the window.
        /// </summary>
        [McpDescription("Height")]
        public float Height { get; set; }
    }

    /// <summary>
    /// Active tool information data structure.
    /// </summary>
    public record ActiveToolData
    {
        /// <summary>
        /// Gets or sets the name of the active tool.
        /// </summary>
        [McpDescription("Name of the active tool")]
        public string ActiveTool { get; set; }

        /// <summary>
        /// Gets or sets whether a custom tool is active.
        /// </summary>
        [McpDescription("Whether a custom tool is active")]
        public bool IsCustom { get; set; }

        /// <summary>
        /// Gets or sets the pivot mode setting.
        /// </summary>
        [McpDescription("Pivot mode setting")]
        public string PivotMode { get; set; }

        /// <summary>
        /// Gets or sets the pivot rotation setting.
        /// </summary>
        [McpDescription("Pivot rotation setting")]
        public string PivotRotation { get; set; }

        /// <summary>
        /// Gets or sets the handle rotation as euler angles.
        /// </summary>
        [McpDescription("Handle rotation as euler angles")]
        public float[] HandleRotation { get; set; }

        /// <summary>
        /// Gets or sets the handle position.
        /// </summary>
        [McpDescription("Handle position")]
        public float[] HandlePosition { get; set; }
    }

    /// <summary>
    /// Selection object information data structure.
    /// </summary>
    public record SelectionObjectInfo
    {
        /// <summary>
        /// Gets or sets the object name.
        /// </summary>
        [McpDescription("Object name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the full type name.
        /// </summary>
        [McpDescription("Full type name")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets the Unity instance ID.
        /// </summary>
        [McpDescription("Unity instance ID")]
        public long? InstanceID { get; set; }
    }

    /// <summary>
    /// GameObject selection information data structure.
    /// </summary>
    public record GameObjectSelectionInfo
    {
        /// <summary>
        /// Gets or sets the GameObject name.
        /// </summary>
        [McpDescription("GameObject name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Unity instance ID.
        /// </summary>
        [McpDescription("Unity instance ID")]
        public long? InstanceID { get; set; }
    }

    /// <summary>
    /// Selection information data structure.
    /// </summary>
    public record SelectionData
    {
        /// <summary>
        /// Gets or sets the name of active selected object.
        /// </summary>
        [McpDescription("Name of active selected object")]
        public string ActiveObject { get; set; }

        /// <summary>
        /// Gets or sets the name of active selected GameObject.
        /// </summary>
        [McpDescription("Name of active selected GameObject")]
        public string ActiveGameObject { get; set; }

        /// <summary>
        /// Gets or sets the name of active selected Transform.
        /// </summary>
        [McpDescription("Name of active selected Transform")]
        public string ActiveTransform { get; set; }

        /// <summary>
        /// Gets or sets the instance ID of active selection.
        /// </summary>
        [McpDescription("Instance ID of active selection")]
        public long ActiveInstanceID { get; set; }

        /// <summary>
        /// Gets or sets the total count of selected objects.
        /// </summary>
        [McpDescription("Total count of selected objects")]
        public int Count { get; set; }

        /// <summary>
        /// Gets or sets the list of all selected objects.
        /// </summary>
        [McpDescription("List of all selected objects")]
        public List<SelectionObjectInfo> Objects { get; set; }

        /// <summary>
        /// Gets or sets the list of all selected GameObjects.
        /// </summary>
        [McpDescription("List of all selected GameObjects")]
        public List<GameObjectSelectionInfo> GameObjects { get; set; }

        /// <summary>
        /// Gets or sets the asset GUIDs of selected assets in Project view.
        /// </summary>
        [McpDescription("Asset GUIDs of selected assets in Project view")]
        public string[] AssetGUIDs { get; set; }
    }

    /// <summary>
    /// Prefab stage information data structure.
    /// </summary>
    public record PrefabStageData
    {
        /// <summary>
        /// Gets or sets whether prefab stage is currently open.
        /// </summary>
        [McpDescription("Whether prefab stage is currently open")]
        public bool IsOpen { get; set; }

        /// <summary>
        /// Gets or sets the asset path of the prefab being edited.
        /// </summary>
        [McpDescription("Asset path of the prefab being edited")]
        public string AssetPath { get; set; }

        /// <summary>
        /// Gets or sets the name of the prefab root GameObject.
        /// </summary>
        [McpDescription("Name of the prefab root GameObject")]
        public string PrefabRootName { get; set; }

        /// <summary>
        /// Gets or sets the prefab stage mode (InContext or InIsolation).
        /// </summary>
        [McpDescription("Prefab stage mode (InContext or InIsolation)")]
        public string Mode { get; set; }

        /// <summary>
        /// Gets or sets whether the prefab has unsaved changes.
        /// </summary>
        [McpDescription("Whether the prefab has unsaved changes")]
        public bool IsDirty { get; set; }
    }

}