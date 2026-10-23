using System;

namespace Unity.AI.Assistant.Skills
{
    /// <summary>
    /// A skill resource that holds content already in memory.
    /// Useful for pre-loaded content, testing, or dynamically generated resources.
    /// </summary>
    class MemorySkillResource : ISkillResource
    {
        readonly string m_Content;

        /// <summary>
        /// Creates a new memory-based skill resource.
        /// </summary>
        /// <param name="content">The resource content</param>
        /// <exception cref="ArgumentNullException">Thrown if content is null</exception>
        public MemorySkillResource(string content)
        {
            m_Content = content ?? throw new ArgumentNullException(nameof(content), "Resource content cannot be null");
        }

        public int Length => m_Content.Length;

        public string GetContent(int maxChars = int.MaxValue)
        {
            return m_Content.Length > maxChars ? m_Content.Substring(0, maxChars + 1) : m_Content;
        }
    }
}
