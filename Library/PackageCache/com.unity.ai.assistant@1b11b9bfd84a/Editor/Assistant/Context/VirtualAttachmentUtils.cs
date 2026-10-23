using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.Editor.Context
{
    static class VirtualAttachmentUtils
    {
        public static VirtualContextSelection ToContextSelection(this VirtualAttachment attachment)
        {
            return new VirtualContextSelection(attachment.Payload,
                attachment.DisplayName,
                string.Empty,
                attachment.Type,
                metadata: attachment.Metadata);
        }
        
        public static AssistantContextEntry ToContextEntry(this VirtualAttachment attachment)
        {
            return new AssistantContextEntry
            {
                Value = attachment.Payload,
                ValueType = attachment.Type,
                DisplayValue = attachment.DisplayName,
                EntryType = AssistantContextType.Virtual,
                Metadata = attachment.Metadata,  // Use the original metadata (e.g., ImageContextMetaData)
                ImageMetadata = attachment.Metadata as ImageContextMetaData,
            };
        }

        public static VirtualAttachment ToVirtualAttachment(this AssistantContextEntry entry)
        {
            return new VirtualAttachment(entry.Value, entry.ValueType, entry.DisplayValue, entry.Metadata);
        }
    }
}
