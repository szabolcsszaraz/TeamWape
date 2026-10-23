using System;
using System.Threading.Tasks;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// A source of asynchronous user interaction. Implementations expose a task that
    /// completes when the user provides a value, and an event that is raised at the
    /// same time.
    /// </summary>
    /// <typeparam name="TOutput">The type of value produced by the interaction.</typeparam>
    public interface IInteractionSource<TOutput>
    {
        /// <summary>
        /// Raised when the interaction completes, either with a result or after cancellation.
        /// On cancellation the argument is <c>default(TOutput)</c>.
        /// </summary>
        public event Action<TOutput> OnCompleted;

        /// <summary>
        /// The <see cref="TaskCompletionSource{TResult}"/> backing the asynchronous result
        /// of this interaction. Consumers await <see cref="TaskCompletionSource{TResult}.Task"/>
        /// to obtain the user's response.
        /// </summary>
        TaskCompletionSource<TOutput> TaskCompletionSource { get; }

        /// <summary>
        /// Cancels the interaction. The backing task transitions to a cancelled state and
        /// <see cref="OnCompleted"/> is raised with the default value of <typeparamref name="TOutput"/>.
        /// </summary>
        public void CancelInteraction();
    }
}
