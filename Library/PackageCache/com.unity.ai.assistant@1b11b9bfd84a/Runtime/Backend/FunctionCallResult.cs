using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Backend
{
    /// <summary>
    /// Represents the outcome of an LLM tool/function call, including whether the call
    /// has completed, whether it succeeded, and the resulting data or error message.
    /// </summary>
    struct FunctionCallResult
    {
        /// <summary>
        /// Whether the function call has finished processing. Defaults to false for
        /// default-constructed instances, indicating the call is still in progress.
        /// </summary>
        public bool IsDone { get; private set; }

        /// <summary>
        /// Whether the function call completed successfully. Only meaningful when
        /// <see cref="IsDone"/> is true.
        /// </summary>
        public bool HasFunctionCallSucceeded { get; private set; }

        /// <summary>
        /// The result payload on success, or a <see cref="JValue"/> containing the error
        /// message on failure.
        /// </summary>
        public JToken Result { get; private set; }

        /// <summary>
        /// Creates a successful result with the given response payload.
        /// </summary>
        /// <param name="response">The JSON response returned by the function.</param>
        /// <returns>A completed <see cref="FunctionCallResult"/> marked as succeeded.</returns>
        internal static FunctionCallResult SuccessfulResult(JToken response) => new()
        {
            HasFunctionCallSucceeded = true,
            Result = response,
            IsDone = true
        };

        /// <summary>
        /// Creates a failed result with the given error message.
        /// </summary>
        /// <param name="error">A description of why the function call failed.</param>
        /// <returns>A completed <see cref="FunctionCallResult"/> marked as failed.</returns>
        internal static FunctionCallResult FailedResult(string error) => new()
        {
            HasFunctionCallSucceeded = false,
            Result = new JValue(error),
            IsDone = true
        };

        /// <summary>
        /// Deserializes <see cref="Result"/> into the specified type using
        /// <see cref="AssistantJsonHelper"/>.
        /// </summary>
        /// <typeparam name="T">The target type to deserialize the result into.</typeparam>
        /// <returns>The deserialized result as an instance of <typeparamref name="T"/>.</returns>
        public T GetTypedResult<T>() => AssistantJsonHelper.ToObject<T>(Result);
    }
}
