using System;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using Unity.AI.Assistant.Editor.Backend.Socket.Utilities;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Editor.Backend.Socket.Tools
{
    static class RemoveComponentTool
    {
        internal const string k_FunctionId = "Unity.GameObject.RemoveComponent";

        [AgentTool(
            "Remove a component from a GameObject in the Unity scene.",
            k_FunctionId)]
        [AgentToolSettings(
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_GameObjectTag)]
        public static async Task RemoveComponent(
            ToolExecutionContext context,
            [ToolParameter("Component instance ID (e.g. 67890) of the component to remove from its GameObject.")]
            long componentInstanceId)
        {
            var targetComponent = GameObjectToolsHelper.FindComponent(componentInstanceId);
            if (targetComponent == null)
                throw new InvalidOperationException(GameObjectToolsHelper.FormatComponentNotFoundMessage(componentInstanceId));

            var targetGo = targetComponent.gameObject;
            var componentType = targetComponent.GetType();
            var componentName = componentType.Name;

            if (componentType == typeof(Transform) || componentType == typeof(RectTransform))
            {
                throw new InvalidOperationException(
                    GameObjectToolsHelper.FormatCannotRemoveComponentMessage(componentName, targetGo.name));
            }

            await context.Permissions.CheckUnityObjectAccess(PermissionItemOperation.Delete, componentType, targetComponent);
            Undo.DestroyObjectImmediate(targetComponent);

            var componentStillExists = targetGo.GetComponent(componentType) != null;
            if (componentStillExists)
            {
                throw new InvalidOperationException(
                    GameObjectToolsHelper.FormatCannotRemoveComponentMessage(componentName, targetGo.name));
            }

            EditorUtility.SetDirty(targetGo);
        }
    }
}
