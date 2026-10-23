using System;

namespace Unity.AI.Assistant.FunctionCalling
{
    /// <summary>
    ///     Marks a static method as an agent tool function for AI Assistant.
    ///     Each method parameter must have a <see cref="ToolParameterAttribute"/> attribute.
    ///     The first parameter of a tool can optionally be a ToolExecutionContext type to receive additional context.
    ///     This context is only available locally and is never sent to the LLM.
    ///     Among other things, it can be used to handle tool permissions and user interactions.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class AgentToolAttribute : Attribute
    {
        /// <summary> Unique id of the tool. </summary>
        public readonly string Id;

        /// <summary> A description of the functionality provided by the tool method. </summary>
        public readonly string Description;

        /// <summary>Marks a static method as an agent tool function for AI Assistant.</summary>
        /// <param name="description">A description of the functionality provided by the tool method.</param>
        /// <param name="id">Unique id of the tool.</param>
        /// <exception cref="ArgumentException">
        ///     Thrown if description is null or empty. A description must be provided for the LLM to understand how to use the tool.
        /// </exception>
        public AgentToolAttribute(string description, string id)
        {
            if (string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("Cannot be empty", nameof(description));

            Id = id;
            Description = description;
        }
    }
}
