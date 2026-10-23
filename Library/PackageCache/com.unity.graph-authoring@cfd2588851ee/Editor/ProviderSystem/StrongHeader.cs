using System.Collections.Generic;
using UnityEditor;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    // Interprets domain-agnostic IDefinition hint data into domain-specific properties.
    // Subclasses read hints directly from the IDefinition via HintUtils.GetHint<T>().
    abstract class StrongHeader<T> where T : IDefinition
    {
        internal bool isValid { get; private set; }

        List<(string message, MessageType severity)> m_msgs;
        internal IEnumerable<(string message, MessageType severity)> Messages => m_msgs;

        protected void AddMessage(string message, MessageType severity = MessageType.Warning)
            => m_msgs.Add((message, severity));

        // Runs once per Process call, whenever the ShaderObject or provider is updated.
        abstract protected void OnProcess(T obj, IProvider provider);

        internal void Process(T obj, IProvider provider)
        {
            isValid = obj != null && obj.IsValid && provider != null && provider.IsValid;

            if (!isValid)
                return;

            m_msgs = new();
            foreach (var hint in ((IDefinition)obj).Hints)
                if (!string.IsNullOrWhiteSpace(hint.Message))
                    m_msgs.Add((($"{hint.Key}: {hint.Message}"), hint.MessageSeverity));

            OnProcess(obj, provider);
        }
    }
}
