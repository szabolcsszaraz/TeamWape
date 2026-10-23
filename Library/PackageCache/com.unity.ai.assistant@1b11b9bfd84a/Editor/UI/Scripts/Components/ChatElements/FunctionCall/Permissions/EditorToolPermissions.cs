using System;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class EditorToolPermissions : ToolPermissions
    {
        readonly AssistantUIContext k_Context;

        public EditorToolPermissions(AssistantUIContext context, IToolUiContainer toolUiContainer, IPermissionsPolicyProvider policyProvider) : base(toolUiContainer, policyProvider)
        {
            k_Context = context;
        }

        protected override void OnPermissionRequested(
            ToolExecutionContext.CallInfo callInfo,
            PermissionType permissionType)
        {
            if (k_Context != null)
                AIAssistantAnalytics.ReportUITriggerLocalPermissionRequestedEvent(k_Context.Blackboard.ActiveConversation.Id, callInfo, permissionType);
        }

        protected override void OnPermissionResponse(
            ToolExecutionContext.CallInfo callInfo,
            PermissionUserAnswer answer,
            PermissionType permissionType)
        {
            if (k_Context == null)
            {
                return;
            }

            AIAssistantAnalytics.ReportUITriggerLocalPermissionResponseEvent(k_Context.Blackboard.ActiveConversation.Id, callInfo, answer, permissionType);
        }

        PermissionInteraction CreatePermission(string action, string question, Func<PermissionUserAnswer?> tryAutoResolve)
        {
            return new PermissionInteraction(action, question) { TryAutoResolve = tryAutoResolve };
        }

        PermissionUserAnswer? CheckState(Func<bool> isAllowed, Func<bool> isDenied)
        {
            if (isAllowed()) return PermissionUserAnswer.AllowAlways;
            if (isDenied()) return PermissionUserAnswer.DenyAlways;
            return null;
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateAssetGenerationElement(ToolExecutionContext.CallInfo callInfo, string path, Type type, long cost)
        {
            return CreatePermission(
                $"Generate {type.Name} asset",
                $"Save to {path}?",
                () => CheckState(State.AssetGeneration.IsAllowed, State.AssetGeneration.IsDenied));
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateCodeExecutionElement(ToolExecutionContext.CallInfo callInfo, string code)
        {
            var permission = CreatePermission(
                "Execute code", null,
                () => CheckState(State.CodeExecution.IsAllowed, State.CodeExecution.IsDenied));

            // Surface the actual code in the "View" panel so the user can inspect what will run
            // before approving. Without this the panel shows only the action title (see UUM-143606).
            if (!string.IsNullOrEmpty(code))
                permission.ExpandedContentFactory = () => CreateCodeView(code);

            return permission;
        }

        VisualElement CreateCodeView(string code)
        {
            var container = new ScrollView();
            container.AddToClassList("permission-content-container");
            container.style.maxHeight = 400;

            var codeBlock = new CodeBlockElement();
            codeBlock.Initialize(k_Context);
            codeBlock.SetCode(code);
            codeBlock.ShowSaveButton(false);

            container.Add(codeBlock);
            return container;
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateFileSystemAccessElement(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, string path)
        {
            var action = PathUtils.IsFilePath(path)
                ? operation switch
                {
                    PermissionItemOperation.Read => "Read file from disk",
                    PermissionItemOperation.Create => "Create file",
                    PermissionItemOperation.Delete => "Delete file",
                    PermissionItemOperation.Modify => "Save file",
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
                }
                : operation switch
                {
                    PermissionItemOperation.Read => "Read from disk",
                    PermissionItemOperation.Create => "Create directory",
                    PermissionItemOperation.Delete => "Delete directory",
                    PermissionItemOperation.Modify => "Change directory",
                    _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
                };

            var question = operation switch
            {
                PermissionItemOperation.Read => $"Read from {path}?",
                PermissionItemOperation.Create => $"Write to {path}?",
                PermissionItemOperation.Delete => $"Delete {path}?",
                PermissionItemOperation.Modify => $"Write to {path}?",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };

            return CreatePermission(action, question,
                () => CheckState(
                    () => State.FileSystem.IsAllowed(operation, path),
                    () => State.FileSystem.IsDenied(operation, path)));
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateScreenCaptureElement(ToolExecutionContext.CallInfo callInfo)
        {
            return CreatePermission(
                "Allow screen capture", null,
                () => CheckState(State.ScreenCapture.IsAllowed, State.ScreenCapture.IsDenied));
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateToolExecutionElement(ToolExecutionContext.CallInfo callInfo)
        {
            var functionId = callInfo.FunctionId;
            return CreatePermission(
                "Execute tool", $"Execute {functionId}?",
                () => CheckState(
                    () => State.ToolExecution.IsAllowed(functionId),
                    () => State.ToolExecution.IsDenied(functionId)));
        }

        protected override IInteractionSource<PermissionUserAnswer> CreatePlayModeElement(ToolExecutionContext.CallInfo callInfo, PermissionPlayModeOperation operation)
        {
            var action = operation switch
            {
                PermissionPlayModeOperation.Enter => "Enter Play Mode",
                PermissionPlayModeOperation.Exit => "Exit Play Mode",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };

            return CreatePermission(action, $"{action}?",
                () => CheckState(
                    () => State.PlayMode.IsAllowed(operation),
                    () => State.PlayMode.IsDenied(operation)));
        }

        protected override IInteractionSource<PermissionUserAnswer> CreateUnityObjectAccessElement(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, Type type, UnityEngine.Object target)
        {
            var action = operation switch
            {
                PermissionItemOperation.Read => "Read Object Data",
                PermissionItemOperation.Create => "Create New Object",
                PermissionItemOperation.Delete => "Delete Object",
                PermissionItemOperation.Modify => "Modify Object",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };

            var objectName = target != null
                ? $"{target.name} ({target.GetType().Name})"
                : type?.Name;

            var question = operation switch
            {
                PermissionItemOperation.Read => $"Read from {objectName}?",
                PermissionItemOperation.Create => $"Create {objectName}?",
                PermissionItemOperation.Delete => $"Delete {objectName}?",
                PermissionItemOperation.Modify => $"Modify {objectName}?",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };

            return CreatePermission(action, question,
                () => CheckState(
                    () => State.UnityObject.IsAllowed(operation, type, target),
                    () => State.UnityObject.IsDenied(operation, type, target)));
        }
    }
}
