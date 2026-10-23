using System;
using System.Threading.Tasks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// Base class for a user interaction surfaced as a <see cref="VisualElement"/>.
    /// Subclasses render the UI and call <see cref="CompleteInteraction"/> when the
    /// user provides an answer, or <see cref="CancelInteraction"/> to abort.
    /// </summary>
    /// <typeparam name="TOutput">The type of value produced when the user completes the interaction.</typeparam>
    public abstract class BaseInteraction<TOutput> : VisualElement, IInteractionSource<TOutput>
    {
        /// <summary>
        /// Raised when the interaction completes, either with a result or after cancellation.
        /// On cancellation the argument is <c>default(TOutput)</c>.
        /// </summary>
        public event Action<TOutput> OnCompleted;

        /// <summary>
        /// The <see cref="TaskCompletionSource{TResult}"/> backing the asynchronous result of this interaction.
        /// </summary>
        public TaskCompletionSource<TOutput> TaskCompletionSource { get; } = new();

        protected void CompleteInteraction(TOutput output)
        {
            if (!TaskCompletionSource.TrySetResult(output))
                return;
            OnCompleted?.Invoke(output);
        }

        /// <summary>
        /// Cancels the interaction. Sets the task to a cancelled state, invokes
        /// <see cref="OnCanceled"/> and raises <see cref="OnCompleted"/> with the default
        /// value of <typeparamref name="TOutput"/>. Has no effect if the interaction has
        /// already been completed or cancelled.
        /// </summary>
        public void CancelInteraction()
        {
            if (!TaskCompletionSource.TrySetCanceled())
                return;
            try
            {
                OnCanceled();
            }
            finally
            {
                OnCompleted?.Invoke(default);
            }
        }

        protected virtual void OnCanceled()
        {
            // Subclasses may override to react to cancellation.
        }
    }
}
