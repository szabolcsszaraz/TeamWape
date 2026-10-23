using System;
using System.Threading;
using System.Threading.Tasks;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// Permission checker bound to a single tool call. Wraps an <see cref="IToolPermissions"/>
    /// together with the call's <see cref="ToolExecutionContext.CallInfo"/> and
    /// <see cref="System.Threading.CancellationToken"/> so tool authors do not have to pass them on
    /// every check. Each check returns a task that completes when the permission is granted
    /// and throws otherwise.
    /// </summary>
    public readonly struct ToolCallPermissions
    {
        ToolExecutionContext.CallInfo Call { get; }
        IToolPermissions Permissions { get; }
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Creates a new <see cref="ToolCallPermissions"/> bound to a specific tool call.
        /// </summary>
        /// <param name="callInfo">The call request data.</param>
        /// <param name="permissions">The underlying permission provider.</param>
        /// <param name="cancellationToken">A cancellation token tied to the lifetime of the call.</param>
        public ToolCallPermissions(ToolExecutionContext.CallInfo callInfo, IToolPermissions permissions, CancellationToken cancellationToken)
        {
            Call = callInfo;
            Permissions = permissions;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Checks if the tool can be executed
        /// </summary>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        internal Task CheckCanExecute() => Permissions.CheckToolExecution(Call, CancellationToken);

        /// <summary>
        /// Checks if the tool has file system permissions
        /// </summary>
        /// <param name="operation">The type of file system operation</param>
        /// <param name="path">A path to check permissions for.</param>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        public Task CheckFileSystemAccess(PermissionItemOperation operation, string path)
            => Permissions.CheckFileSystemAccess(Call, operation, path, CancellationToken);

        /// <summary>
        /// Checks if the tool has UnityEngine.Object permissions
        /// </summary>
        /// <param name="operation">The operation to perform</param>
        /// <param name="type">The type of the object impacted by the operation, optional when a target object is provided</param>
        /// <param name="target">The object targeted by the operation, when applicable</param>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        public Task CheckUnityObjectAccess(PermissionItemOperation operation, Type type, Object target)
            => Permissions.CheckUnityObjectAccess(Call, operation, type, target, CancellationToken);

        /// <summary>
        /// Ignore all permission checks performed on the given object
        /// </summary>
        /// <param name="target">The object instance to ignore</param>
        public void IgnoreUnityObject(Object target) => Permissions.IgnoreUnityObject(Call, target);

        /// <summary>
        /// Checks if the tool has code execution permission
        /// </summary>
        /// <param name="code">The code to be executed.</param>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        internal Task CheckCodeExecution(string code) => Permissions.CheckCodeExecution(Call, code, CancellationToken);

        /// <summary>
        /// Checks if the tool has screen capture permissions
        /// </summary>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        internal Task CheckScreenCapture() => Permissions.CheckScreenCapture(Call, CancellationToken);

        /// <summary>
        /// Checks if the tool has Play Mode permissions
        /// </summary>
        /// <param name="operation">The operation to perform</param>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        public Task CheckPlayMode(PermissionPlayModeOperation operation)
            => Permissions.CheckPlayMode(Call, operation, CancellationToken);

        /// <summary>
        /// Checks if the tool can use asset generation
        /// </summary>
        /// <param name="path">The path at which the asset will be created</param>
        /// <param name="type">The type of asset that will be created</param>
        /// <returns>An asynchronous task that will complete if permissions are granted and throw otherwise</returns>
        internal Task CheckAssetGeneration(string path, Type type, long cost) => Permissions.CheckAssetGeneration(Call, path, type, cost, CancellationToken);
    }
}
