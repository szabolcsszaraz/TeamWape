using System.Collections.Generic;

namespace Unity.AI.Assistant.Agent.Dynamic.Extension.Editor
{
    /// <exclude />
    public static class CommandScriptPostBox
    {
        static readonly Dictionary<string, object> k_CommandScripts = new();

        /// <exclude />
        public static void Post(string key, object commandScript)
        {
            k_CommandScripts[key] = commandScript;
        }

        /// <exclude />
        public static object Pull(string key)
        {
            if (!k_CommandScripts.Remove(key, out var commandScript))
                return null;

            return commandScript;
        }
    }
}
