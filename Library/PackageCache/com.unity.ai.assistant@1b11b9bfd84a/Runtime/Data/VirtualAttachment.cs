using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant
{
    /// <summary>
    /// Represents an attachment (can be a game object, texture, etc) that can be sent as context with an assistant prompt.
    /// </summary>
    public class VirtualAttachment
    {
        /// <summary>
        /// Creates a new <see cref="VirtualAttachment"/> instance.
        /// </summary>
        /// <param name="payload">The raw content of the attachment (e.g. text, base-64 encoded image, JSON).</param>
        /// <param name="type">A string identifying the kind of attachment (e.g. "image/png", "text/plain").</param>
        /// <param name="displayName">A human-readable label shown in the UI for this attachment.</param>
        /// <param name="metadata">Optional metadata associated with the attachment.</param>
        public VirtualAttachment(string payload, string type, string displayName, object metadata)
        {
            Payload = payload;
            Type = type;
            DisplayName = displayName;
            Metadata =  metadata;
        }

        /// <summary>
        /// Base64 encoded payload 
        /// </summary>
        public readonly string Payload;
        
        /// <summary>
        /// type of the attachment. (e.g. "image/png", "text/plain").
        /// </summary>
        public string Type;
        
        /// <summary>
        /// The name the attachment will show in the user interfaces.
        /// </summary>
        public string DisplayName;
        
        /// <summary>
        /// Optional metadata associated with the attachment
        /// </summary>
        public object Metadata;

        /// <summary>
        /// check if two virtual attachment objects are equal to each other
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is not VirtualAttachment other)
            {
                return false;
            }

            // Compare by Payload and Type since Payload contains the unique PNG data
            return Payload == other.Payload && Type == other.Type;
        }

        /// <summary>
        /// Get the hashcode of the object. It's combined from Payload and Type.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            // Use Payload and Type for hash code generation
            return System.HashCode.Combine(Payload, Type);
        }
    }
}
