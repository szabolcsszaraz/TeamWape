using System;
using Newtonsoft.Json;

namespace Unity.AI.Assistant.Data
{
    /// <summary>
    /// Represents a single todo/progress item for plan mode execution tracking.
    /// </summary>
    [Serializable]
    class TodoItem
    {
        [JsonProperty("description")]
        public string Description;

        [JsonProperty("status")]
        public string Status; // pending, in_progress, completed, cancelled
    }
}
