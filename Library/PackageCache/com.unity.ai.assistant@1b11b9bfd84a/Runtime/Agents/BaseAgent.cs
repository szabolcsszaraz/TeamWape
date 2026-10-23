namespace Unity.AI.Assistant.Agents
{
    /// <summary>
    /// Base class for all custom agents
    /// </summary>
    public abstract class BaseAgent<TAgent> : IAgent
        where TAgent : BaseAgent<TAgent>
    {
        /// <inheritdoc/>
        public string UniqueId { get; set; }

        /// <inheritdoc/>
        public string Name { get; set; }

        /// <inheritdoc/>
        public string Description { get; set; }

        /// <summary>
        /// Initializes the agent with optional identity fields.
        /// </summary>
        /// <param name="uniqueId">Unique identifier for this agent configuration.</param>
        /// <param name="name">Display name for the agent.</param>
        /// <param name="description">Description of what this agent does. Used by the multi-agent orchestration system to select the best agent for a given task.</param>
        protected BaseAgent(string uniqueId = "", string name = "", string description = "")
        {
            UniqueId = uniqueId;
            Name = name;
            Description = description;
        }

        /// <summary>
        /// Set the agent's unique identifier.
        /// </summary>
        /// <param name="uniqueId">Unique identifier for this agent configuration.</param>
        /// <returns>This agent instance, for chaining.</returns>
        public TAgent WithId(string uniqueId)
        {
            UniqueId = uniqueId;
            return (TAgent)this;
        }

        /// <summary>
        /// Set the agent's display name.
        /// </summary>
        /// <param name="name">Human-readable name shown in the UI and logs.</param>
        /// <returns>This agent instance, for chaining.</returns>
        public TAgent WithName(string name)
        {
            Name = name;
            return (TAgent)this;
        }

        /// <summary>
        /// Set the agent's description.
        /// </summary>
        /// <param name="description">Description of what this agent does. Used by the multi-agent orchestration system to select the best agent for a given task.</param>
        /// <returns>This agent instance, for chaining.</returns>
        public TAgent WithDescription(string description)
        {
            Description = description;
            return (TAgent)this;
        }
    }
}
