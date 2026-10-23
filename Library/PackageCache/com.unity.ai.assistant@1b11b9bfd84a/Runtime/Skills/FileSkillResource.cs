using System;
using System.IO;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Skills
{
    /// <summary>
    /// A skill resource that loads content from a file on the filesystem.
    /// The content is loaded on-demand when GetContent() is called.
    /// </summary>
    class FileSkillResource : ISkillResource
    {
        readonly string m_FilePath;
        int? m_CachedSize;

        /// <summary>
        /// Creates a new file-based skill resource.
        /// </summary>
        /// <param name="filePath">Absolute path to the file</param>
        public FileSkillResource(string filePath)
        {
            m_FilePath = filePath;
        }

        public int Size
        {
            get
            {
                if (m_CachedSize.HasValue)
                    return m_CachedSize.Value;

                try
                {
                    if (string.IsNullOrEmpty(m_FilePath) || !File.Exists(m_FilePath))
                        m_CachedSize = 0;
                    else
                        m_CachedSize = (int)new FileInfo(m_FilePath).Length;
                }
                catch
                {
                    m_CachedSize = 0;
                }
                return m_CachedSize.Value;
            }
        }

        public int Length => Size;

        /// <summary>
        /// Loads and returns the content of the file, bounded to one char past
        /// <paramref name="maxChars"/> so callers can detect and report the overflow without an
        /// oversized file being materialized on the Editor main thread just to be truncated.
        /// </summary>
        /// <returns>The file content as a string</returns>
        /// <exception cref="IOException">Thrown if the file is missing or cannot be read</exception>
        /// <exception cref="InvalidOperationException">Thrown if the file is not valid UTF-8 text</exception>
        public string GetContent(int maxChars = int.MaxValue)
        {
            if (!File.Exists(m_FilePath))
                throw new FileNotFoundException($"Skill resource file doesn't exist at path: {m_FilePath}");

            var fileInfo = new FileInfo(m_FilePath);
            if (fileInfo.Length == 0)
            {
                throw new FileLoadException($"Skill resource file is empty: {m_FilePath}");
            }

            // Resources share SKILL.md's UTF-8 contract, so they use the same validate-then-strict-read
            // helper. The older byte-range heuristic classified any file whose first 4 KB was more than
            // 5% non-ASCII as binary, which rejected ordinary markdown carrying em dashes, curly quotes,
            // arrows or CJK text. Unlike SKILL.md a leading BOM is tolerated here: resources are
            // arbitrary author-supplied files, and the helper strips the BOM on the way through.
            return TextFileUtils.ReadUtf8TextFile(m_FilePath, allowBom: true, maxChars);
        }
    }
}
