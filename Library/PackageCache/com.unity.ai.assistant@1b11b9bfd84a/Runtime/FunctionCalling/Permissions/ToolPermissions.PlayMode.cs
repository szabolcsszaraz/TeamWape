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
            public class PlayModeState
            {
                [SerializeField]
                List<PermissionPlayModeOperation> m_AllowedOperations = new();

                [SerializeField]
                List<PermissionPlayModeOperation> m_DeniedOperations = new();

                public void Reset()
                {
                    m_AllowedOperations.Clear();
                    m_DeniedOperations.Clear();
                }

                public void Allow(PermissionPlayModeOperation operation)
                {
                    m_AllowedOperations.Add(operation);
                }

                public bool IsAllowed(PermissionPlayModeOperation operation)
                {
                    return m_AllowedOperations.Contains(operation);
                }

                public void Deny(PermissionPlayModeOperation operation)
                {
                    m_DeniedOperations.Add(operation);
                }

                public bool IsDenied(PermissionPlayModeOperation operation)
                {
                    return m_DeniedOperations.Contains(operation);
                }

                public void AppendTemporaryPermissions(IList<IToolPermissions.TemporaryPermission> allowedStates)
                {
                    foreach (var operation in m_AllowedOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} Play Mode", () => m_AllowedOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }

                    foreach (var operation in m_DeniedOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} Play Mode (Denied)", () => m_DeniedOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }
                }
            }
        }

        public async Task CheckPlayMode(ToolExecutionContext.CallInfo callInfo, PermissionPlayModeOperation operation, CancellationToken cancellationToken = default)
        {
            // Get current tool status
            var currentStatus = GetPlayModePermission(callInfo, operation);

            InternalLog.Log($"[Permission] PlayMode: {callInfo.FunctionId}. PermissionStatus: {currentStatus}");

            // Ask user and update status
            if (currentStatus == PermissionStatus.Pending)
            {
                var userInteraction = CreatePlayModeElement(callInfo, operation);
                OnPermissionRequested(callInfo, PermissionType.PlayMode);
                var userAnswer = await WaitForUser(callInfo, userInteraction, cancellationToken);
                InternalLog.Log($"[Permission] PlayMode: {callInfo.FunctionId}. Answer: {userAnswer}");
                OnPermissionResponse(callInfo, userAnswer, PermissionType.PlayMode);

                switch (userAnswer)
                {
                    case PermissionUserAnswer.AllowOnce:
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.AllowAlways:
                        State.PlayMode.Allow(operation);
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.DenyOnce:
                        currentStatus = PermissionStatus.Denied;
                        break;

                    case PermissionUserAnswer.DenyAlways:
                        State.PlayMode.Deny(operation);
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
                PermissionPlayModeOperation.Enter => $"The user denied the request to enter play mode",
                PermissionPlayModeOperation.Exit => $"The user denied the request to exit play mode",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
            throw new Exception(errorMessage);
        }

        PermissionStatus GetPlayModePermission(ToolExecutionContext.CallInfo callInfo, PermissionPlayModeOperation operation)
        {
            var permissionPolicy = PolicyProvider.GetPlayModePolicy(callInfo.FunctionId, operation);
            return permissionPolicy switch
            {
                IPermissionsPolicyProvider.PermissionPolicy.Allow => PermissionStatus.Approved,
                IPermissionsPolicyProvider.PermissionPolicy.Ask =>
                    State.PlayMode.IsAllowed(operation) ? PermissionStatus.Approved :
                    State.PlayMode.IsDenied(operation) ? PermissionStatus.Denied :
                    PermissionStatus.Pending,
                IPermissionsPolicyProvider.PermissionPolicy.Deny => PermissionStatus.Denied,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected abstract IInteractionSource<PermissionUserAnswer> CreatePlayModeElement(ToolExecutionContext.CallInfo callInfo, PermissionPlayModeOperation operation);
    }
}
