using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;
using UnityEngine;

namespace Unity.AI.Assistant.FunctionCalling
{
    partial class ToolPermissions
    {
        protected partial class PermissionsState
        {
            [Serializable]
            public class FileSystemState
            {
                const string k_ProjectFilesName = "Project Files";
                const string k_ExternalFilesName = "External Files";

                [SerializeField]
                List<PermissionItemOperation> m_AllowedProjectOperations = new();

                [SerializeField]
                List<PermissionItemOperation> m_AllowedExternalOperations = new();

                [SerializeField]
                List<PermissionItemOperation> m_DeniedProjectOperations = new();

                [SerializeField]
                List<PermissionItemOperation> m_DeniedExternalOperations = new();

                public void Reset()
                {
                    m_AllowedProjectOperations.Clear();
                    m_AllowedExternalOperations.Clear();
                    m_DeniedProjectOperations.Clear();
                    m_DeniedExternalOperations.Clear();
                }

                public void Allow(PermissionItemOperation operation, string path)
                {
                    var isProjectPath = PathUtils.IsProjectPath(path);
                    if (isProjectPath)
                        m_AllowedProjectOperations.Add(operation);
                    else
                        m_AllowedExternalOperations.Add(operation);
                }

                public bool IsAllowed(PermissionItemOperation operation, string path)
                {
                    var isProjectPath = PathUtils.IsProjectPath(path);
                    return isProjectPath
                        ? m_AllowedProjectOperations.Contains(operation)
                        : m_AllowedExternalOperations.Contains(operation);
                }

                public void Deny(PermissionItemOperation operation, string path)
                {
                    var isProjectPath = PathUtils.IsProjectPath(path);
                    if (isProjectPath)
                        m_DeniedProjectOperations.Add(operation);
                    else
                        m_DeniedExternalOperations.Add(operation);
                }

                public bool IsDenied(PermissionItemOperation operation, string path)
                {
                    var isProjectPath = PathUtils.IsProjectPath(path);
                    return isProjectPath
                        ? m_DeniedProjectOperations.Contains(operation)
                        : m_DeniedExternalOperations.Contains(operation);
                }

                public void AppendTemporaryPermissions(IList<IToolPermissions.TemporaryPermission> allowedStates)
                {
                    foreach (var operation in m_AllowedProjectOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_ProjectFilesName}", () => m_AllowedProjectOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }

                    foreach (var operation in m_AllowedExternalOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_ExternalFilesName}", () => m_AllowedExternalOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }

                    foreach (var operation in m_DeniedProjectOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_ProjectFilesName} (Denied)", () => m_DeniedProjectOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }

                    foreach (var operation in m_DeniedExternalOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_ExternalFilesName} (Denied)", () => m_DeniedExternalOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }
                }
            }
        }

        public async Task CheckFileSystemAccess(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, string path, CancellationToken cancellationToken = default)
        {
            // Get current tool status
            var currentStatus = GetFileSystemAccessPermission(callInfo, operation, path);

            InternalLog.Log($"[Permission] CheckFileSystemAccess: {callInfo.FunctionId}. PermissionStatus: {currentStatus}");

            // Ask user and update status
            if (currentStatus == PermissionStatus.Pending)
            {
                var userInteraction = CreateFileSystemAccessElement(callInfo, operation, path);
                OnPermissionRequested(callInfo, PermissionType.FileSystem);
                var userAnswer = await WaitForUser(callInfo, userInteraction, cancellationToken);
                InternalLog.Log($"[Permission] CheckFileSystemAccess: {callInfo.FunctionId}. Answer: {userAnswer}");

                OnPermissionResponse(callInfo, userAnswer, PermissionType.FileSystem);

                switch (userAnswer)
                {
                    case PermissionUserAnswer.AllowOnce:
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.AllowAlways:
                        State.FileSystem.Allow(operation, path);
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.DenyOnce:
                        currentStatus = PermissionStatus.Denied;
                        break;

                    case PermissionUserAnswer.DenyAlways:
                        State.FileSystem.Deny(operation, path);
                        currentStatus = PermissionStatus.Denied;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            // Permission approved, nothing more to do
            if (currentStatus == PermissionStatus.Approved)
                return;

            // Permission denied
            var errorMessage = operation switch
            {
                PermissionItemOperation.Read => $"The user denied the request to read path: {path}",
                PermissionItemOperation.Create => $"The user denied the request to create path: {path}",
                PermissionItemOperation.Delete => $"The user denied the request to delete path: {path}",
                PermissionItemOperation.Modify => $"The user denied the request to write at path: {path}",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
            throw new Exception(errorMessage);
        }

        PermissionStatus GetFileSystemAccessPermission(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, string path)
        {
            var permissionPolicy = PolicyProvider.GetFileSystemPolicy(callInfo.FunctionId, operation, path);
            return permissionPolicy switch
            {
                IPermissionsPolicyProvider.PermissionPolicy.Allow => PermissionStatus.Approved,
                IPermissionsPolicyProvider.PermissionPolicy.Ask =>
                    State.FileSystem.IsAllowed(operation, path) ? PermissionStatus.Approved :
                    State.FileSystem.IsDenied(operation, path) ? PermissionStatus.Denied :
                    PermissionStatus.Pending,
                IPermissionsPolicyProvider.PermissionPolicy.Deny => PermissionStatus.Denied,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected abstract IInteractionSource<PermissionUserAnswer> CreateFileSystemAccessElement(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, string path);
    }
}
