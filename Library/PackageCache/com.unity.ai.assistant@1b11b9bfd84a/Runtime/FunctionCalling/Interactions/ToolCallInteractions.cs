using System.Threading;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// User-interaction surface bound to a single tool call. Wraps an
    /// <see cref="IToolInteractions"/> together with the call's <see cref="ToolExecutionContext.CallInfo"/>
    /// and <see cref="System.Threading.CancellationToken"/> so callers do not have to pass them on every request.
    /// </summary>
    public readonly struct ToolCallInteractions
    {
        ToolExecutionContext.CallInfo Call { get; }
        IToolInteractions Interactions { get; }
        CancellationToken CancellationToken { get; }

        /// <summary>
        /// Creates a new <see cref="ToolCallInteractions"/> bound to a specific tool call.
        /// </summary>
        /// <param name="callInfo">The call request data.</param>
        /// <param name="toolInteractions">The underlying interaction provider.</param>
        /// <param name="cancellationToken">A cancellation token tied to the lifetime of the call.</param>
        public ToolCallInteractions(ToolExecutionContext.CallInfo callInfo, IToolInteractions toolInteractions, CancellationToken cancellationToken)
        {
            Call = callInfo;
            Interactions = toolInteractions;
            CancellationToken = cancellationToken;
        }

        /// <summary>
        /// Wait for a user interaction
        /// </summary>
        /// <param name="userInteraction">The user interaction to wait for</param>
        /// <param name="timeoutSeconds">A duration after which the interaction should fail if not completed</param>
        /// <typeparam name="TOutput">The type of interaction output</typeparam>
        /// <returns>An asynchronous task that returns the interaction result.</returns>
        public async Task<TOutput> WaitForUser<TOutput>(IInteractionSource<TOutput> userInteraction, int timeoutSeconds = 600)
            => await Interactions.WaitForUser(Call, userInteraction, timeoutSeconds, CancellationToken);
    }
}
