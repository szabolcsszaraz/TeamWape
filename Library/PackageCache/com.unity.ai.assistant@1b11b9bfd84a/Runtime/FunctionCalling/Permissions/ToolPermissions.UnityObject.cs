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
            public class UnityObjectState
            {
                const string k_StateName = "Unity Objects";

                [SerializeField]
                List<PermissionItemOperation> m_AllowedOperations = new();

                [SerializeField]
                List<PermissionItemOperation> m_DeniedOperations = new();

                [SerializeField]
                List<UnityEngine.Object> m_IgnoredObjects = new();

                public void Reset()
                {
                    m_AllowedOperations.Clear();
                    m_DeniedOperations.Clear();
                    ResetIgnoredObjects();
                }

                public void ResetIgnoredObjects()
                {
                    m_IgnoredObjects.Clear();
                }

                public void Allow(PermissionItemOperation operation, Type type, UnityEngine.Object target)
                    => m_AllowedOperations.Add(operation);

                public void Deny(PermissionItemOperation operation, Type type, UnityEngine.Object target)
                    => m_DeniedOperations.Add(operation);

                public void Ignore(UnityEngine.Object target) => m_IgnoredObjects.Add(target);

                public bool IsAllowed(PermissionItemOperation operation, Type type, UnityEngine.Object target)
                {
                    if (m_AllowedOperations.Contains(operation))
                        return true;

                    if (target != null)
                    {
                        if (m_IgnoredObjects.Contains(target))
                            return true;

                        if (target is Component component && m_IgnoredObjects.Contains(component.gameObject))
                            return true;
                    }

                    return false;
                }

                public bool IsDenied(PermissionItemOperation operation, Type type, UnityEngine.Object target)
                {
                    return m_DeniedOperations.Contains(operation);
                }

                public void AppendTemporaryPermissions(IList<IToolPermissions.TemporaryPermission> allowedStates)
                {
                    foreach (var operation in m_AllowedOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_StateName}", () => m_AllowedOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }

                    foreach (var operation in m_DeniedOperations)
                    {
                        var permission = new IToolPermissions.TemporaryPermission($"{operation} {k_StateName} (Denied)", () => m_DeniedOperations.Remove(operation));
                        allowedStates.Add(permission);
                    }
                }
            }
        }

        public void IgnoreUnityObject(ToolExecutionContext.CallInfo callInfo, UnityEngine.Object target)
        {
            if (target == null)
                return;

            State.UnityObject.Ignore(target);
        }

        public async Task CheckUnityObjectAccess(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, Type type, UnityEngine.Object target, CancellationToken cancellationToken = default)
        {
            if (type == null && target == null)
                throw new ArgumentException("Either type or target are required");

            if (target != null && type != null && !type.IsAssignableFrom(target.GetType()))
                throw new ArgumentException("Type and target object must match, or only provide the target instance.");

            if (operation != PermissionItemOperation.Create && target == null)
                throw new ArgumentException("You must provide a target instance for all operations except creation.");

            if (target != null)
                type = target.GetType();

            // Get current tool status
            var currentStatus = GetUnityObjectAccessPermission(callInfo, operation, type, target);

            InternalLog.Log($"[Permission] CheckUnityObjectAccess: {callInfo.FunctionId}. PermissionStatus: {currentStatus}");

            // Ask user and update status
            if (currentStatus == PermissionStatus.Pending)
            {
                var userInteraction = CreateUnityObjectAccessElement(callInfo, operation, type, target);
                OnPermissionRequested(callInfo, PermissionType.UnityObject);
                var userAnswer = await WaitForUser(callInfo, userInteraction, cancellationToken);
                InternalLog.Log($"[Permission] CheckUnityObjectAccess: {callInfo.FunctionId}. Answer: {userAnswer}");

                OnPermissionResponse(callInfo, userAnswer, PermissionType.UnityObject);

                switch (userAnswer)
                {
                    case PermissionUserAnswer.AllowOnce:
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.AllowAlways:
                        State.UnityObject.Allow(operation, type, target);
                        currentStatus = PermissionStatus.Approved;
                        break;

                    case PermissionUserAnswer.DenyOnce:
                        currentStatus = PermissionStatus.Denied;
                        break;

                    case PermissionUserAnswer.DenyAlways:
                        State.UnityObject.Deny(operation, type, target);
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
                PermissionItemOperation.Read => $"The user denied the request to read instances",
                PermissionItemOperation.Create => $"The user denied the request to create new instances",
                PermissionItemOperation.Delete => $"The user denied the request to delete instances",
                PermissionItemOperation.Modify => $"The user denied the request to modify instances",
                _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, null)
            };
            throw new Exception(errorMessage);
        }

        PermissionStatus GetUnityObjectAccessPermission(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, Type type, UnityEngine.Object target)
        {
            var permissionPolicy = PolicyProvider.GetUnityObjectPolicy(callInfo.FunctionId, operation, type, target);
            return permissionPolicy switch
            {
                IPermissionsPolicyProvider.PermissionPolicy.Allow => PermissionStatus.Approved,
                IPermissionsPolicyProvider.PermissionPolicy.Ask =>
                    State.UnityObject.IsAllowed(operation, type, target) ? PermissionStatus.Approved :
                    State.UnityObject.IsDenied(operation, type, target) ? PermissionStatus.Denied :
                    PermissionStatus.Pending,
                IPermissionsPolicyProvider.PermissionPolicy.Deny => PermissionStatus.Denied,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        protected abstract IInteractionSource<PermissionUserAnswer> CreateUnityObjectAccessElement(ToolExecutionContext.CallInfo callInfo, PermissionItemOperation operation, Type type, UnityEngine.Object target);
    }
}
