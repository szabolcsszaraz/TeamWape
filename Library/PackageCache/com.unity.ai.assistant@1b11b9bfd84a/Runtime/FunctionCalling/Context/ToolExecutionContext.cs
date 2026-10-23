using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    /// The execution context of the tool
    /// </summary>
    public readonly struct ToolExecutionContext
    {
        /// <summary>
        /// Information on the function call request
        /// </summary>
        public struct CallInfo
        {
            /// <summary>
            /// The tool ID
            /// </summary>
            public string FunctionId { get; }

            /// <summary>
            /// Call ID
            /// Note that this Call ID is not the same as the LLM tool call ID !
            /// </summary>
            public Guid CallId { get; }

            /// <summary>
            /// The raw parameters of the tool
            /// </summary>
            internal JObject Parameters { get; }

            /// <summary>
            /// Creates a <see cref="CallInfo"/> from a raw JSON parameters string.
            /// </summary>
            /// <param name="functionId">The tool ID</param>
            /// <param name="callId">The call ID</param>
            /// <param name="parameters">The raw parameters of the tool, as a JSON string</param>
            public CallInfo(string functionId, Guid callId, string parameters)
            {
                FunctionId = functionId;
                CallId =  callId;
                Parameters = string.IsNullOrWhiteSpace(parameters) ? null : JObject.Parse(parameters);
            }

            /// <summary>
            /// Creates a <see cref="CallInfo"/> from a pre-parsed <see cref="JObject"/>. Internal-only to avoid leaking
            /// the Newtonsoft.Json dependency through the public API surface.
            /// </summary>
            /// <param name="functionId">The tool ID</param>
            /// <param name="callId">The call ID</param>
            /// <param name="parameters">The raw parameters of the tool, as a parsed <see cref="JObject"/></param>
            internal CallInfo(string functionId, Guid callId, JObject parameters)
            {
                FunctionId = functionId;
                CallId =  callId;
                Parameters = parameters;
            }
        }

        /// <summary>
        /// The conversation context
        /// </summary>
        public ConversationContext Conversation { get; }

        /// <summary>
        /// The tool call request parameters
        /// </summary>
        public CallInfo Call { get; }

        /// <summary>
        /// The permissions of this tool in the current context
        /// </summary>
        public ToolCallPermissions Permissions { get; }

        /// <summary>
        /// The user interactions of this tool in the current context
        /// </summary>
        public ToolCallInteractions Interactions { get; }

        /// <summary>
        /// Creates a new <see cref="ToolExecutionContext"/>.
        /// </summary>
        /// <param name="conversationContext">The conversation in which the tool is being invoked.</param>
        /// <param name="callInfo">The call request data (tool ID, call ID and parameters).</param>
        /// <param name="toolPermissions">The permission checker bound to this call.</param>
        /// <param name="toolInteractions">The user interaction surface bound to this call.</param>
        public ToolExecutionContext(ConversationContext conversationContext, CallInfo callInfo, ToolCallPermissions toolPermissions, ToolCallInteractions toolInteractions)
        {
            Conversation = conversationContext;
            Call = callInfo;
            Permissions = toolPermissions;
            Interactions = toolInteractions;
        }
    }
}
