using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils;

namespace Unity.AI.Assistant.Editor.Context
{
    internal class ContextSerializationHelper
    {
        internal static AssistantContextList BuildPromptSelectionContext(IEnumerable<UnityEngine.Object> objectAttachments, IEnumerable<VirtualAttachment> virtualAttachments, IEnumerable<LogData> consoleAttachments)
        {
            // workaround to allow serialization of entire list - not just individual items
            var serializableContext = new AssistantContextList();
            var result = serializableContext.m_ContextList;

            foreach (var attachment in objectAttachments)
            {
                var entry = attachment.GetContextEntry();
                result.Add(entry);
            }

            foreach (var attachment in virtualAttachments)
            {
                result.Add(attachment.ToContextEntry());
            }

            foreach (var entry in consoleAttachments)
            {
                result.Add(new AssistantContextEntry
                {
                    Value = entry.Message,
                    EntryType = AssistantContextType.ConsoleMessage,
                    ValueType = entry.Type.ToString()
                });
            }

            return serializableContext;
        }

        internal static List<AssistantContextEntry> RestoreContextEntries(List<AssistantContextEntry> contextEntries)
        {
            var output = new List<AssistantContextEntry>();
            foreach (var entry in contextEntries)
            {
                var restored = entry;
                if (entry.ImageMetadata != null)
                    restored.Metadata = entry.ImageMetadata;
                output.Add(restored);
            }
            return output;
        }
    }
}
