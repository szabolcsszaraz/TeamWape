using System;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Replaces a specified pattern by another string.
    /// </summary>
    public class RegexReplaceNormalizer : INormalizer
    {
        /// <summary>
        /// The pattern to look for in the input string.
        /// </summary>
        readonly Regex m_Pattern;

        /// <summary>
        /// The string to replace the pattern with.
        /// </summary>
        readonly string m_Replacement;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexReplaceNormalizer" /> type.
        /// </summary>
        /// <param name="pattern">
        /// The pattern to look for in the input string.
        /// </param>
        /// <param name="replacement">
        /// The string to replace the pattern with.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="pattern" /> cannot be null or empty.
        /// </exception>
        public RegexReplaceNormalizer([NotNull] Regex pattern, [CanBeNull] string replacement)
        {
            m_Pattern = pattern ?? throw new ArgumentNullException(nameof(pattern));
            m_Replacement = replacement ?? string.Empty;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input) => m_Pattern.Replace(input, m_Replacement);
    }
}
