using System;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.Editor
{
    class SettingsPermissionsPolicyProvider : IPermissionsPolicyProvider
    {
        enum ToolType
        {
            FirstParty,
            ThirdParty
        }

        static string[] s_AllowedLocations = {
            "Unity.AI.Assistant",
            "Unity.AI.Agents",
        };

        bool AutoRun => AssistantEditorPreferences.AutoRun;

        public IPermissionsPolicyProvider.PermissionPolicy GetToolExecutionPolicy(string toolId)
        {
            var toolType = GetToolType(toolId);
            var policy = toolType switch
            {
                ToolType.FirstParty => AssistantEditorPreferences.Permissions.FirstPartyToolPolicy,
                ToolType.ThirdParty => AssistantEditorPreferences.Permissions.ThirdPartyToolPolicy,
                _ => throw new ArgumentOutOfRangeException(nameof(toolType), toolType, null)
            };
            return ApplyAutoRunState(policy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetFileSystemPolicy(string toolId, PermissionItemOperation operation, string path)
        {
            var isProjectPath = PathUtils.IsProjectPath(path);
            var policy = isProjectPath
                ? operation == PermissionItemOperation.Read
                    ? AssistantEditorPreferences.Permissions.ReadProjectPolicy
                    : AssistantEditorPreferences.Permissions.ModifyProjectPolicy
                : operation == PermissionItemOperation.Read
                    ? AssistantEditorPreferences.Permissions.ReadExternalFilesPolicy
                    : IPermissionsPolicyProvider.PermissionPolicy.Deny;
            return ApplyAutoRunState(policy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetUnityObjectPolicy(string toolId, PermissionItemOperation operation, Type type, Object target)
        {
            if (type == null && target == null)
                throw new ArgumentException("Either type or target are required");

            if (target != null && type != null && target.GetType() != type)
                throw new ArgumentException("Type and target object must match, or only provide the target instance.");

            if (operation != PermissionItemOperation.Create && target == null)
                throw new ArgumentException("You must provide a target instance for all operations except creation.");

            var policy = operation == PermissionItemOperation.Read
                ? AssistantEditorPreferences.Permissions.ReadProjectPolicy
                : AssistantEditorPreferences.Permissions.ModifyProjectPolicy;
            return ApplyAutoRunState(policy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetCodeExecutionPolicy(string toolId, string code)
        {
            return ApplyAutoRunState(AssistantEditorPreferences.Permissions.CodeExecutionPolicy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetScreenCapturePolicy(string toolId)
        {
            return ApplyAutoRunState(AssistantEditorPreferences.Permissions.ScreenCapturePolicy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetPlayModePolicy(string toolId, PermissionPlayModeOperation operation)
        {
            return ApplyAutoRunState(AssistantEditorPreferences.Permissions.PlayModePolicy);
        }

        public IPermissionsPolicyProvider.PermissionPolicy GetAssetGenerationPolicy(string toolId, string path, Type type)
        {
            return ApplyAutoRunState(AssistantEditorPreferences.Permissions.AssetGenerationPolicy);
        }

        IPermissionsPolicyProvider.PermissionPolicy ApplyAutoRunState(IPermissionsPolicyProvider.PermissionPolicy policy)
        {
            if (AutoRun && policy == IPermissionsPolicyProvider.PermissionPolicy.Ask)
                return IPermissionsPolicyProvider.PermissionPolicy.Allow;

            return policy;
        }

        static ToolType GetToolType(string toolId)
        {
            if (ToolRegistry.FunctionToolbox.TryGetMethod(toolId, out var tool))
            {
                switch (tool)
                {
                    case IAssemblyFunction laf:
                        var location = laf.Assembly != null ? AssemblyUtils.GetAssemblyPath(laf.Assembly) : null;

                        if (!string.IsNullOrEmpty(location))
                        {
                            foreach (var allowedLocation in s_AllowedLocations)
                            {
                                if (location.Contains(allowedLocation))
                                    return ToolType.FirstParty;
                            }
                        }
                        break;
                }
            }

            return ToolType.ThirdParty;
        }
    }
}
