using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Replaces a specified pattern by another string.
    /// </summary>
    public class ReplaceNormalizer : INormalizer
    {
        /// <summary>
        /// The pattern to look for in the input string.
        /// </summary>
        readonly string m_Pattern;

        /// <summary>
        /// The string to replace the pattern with.
        /// </summary>
        readonly string m_Replacement;

        /// <summary>
        /// Initializes a new instance of the <see cref="ReplaceNormalizer" /> type.
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
        public ReplaceNormalizer([NotNull] string pattern, [CanBeNull] string replacement)
        {
            if(string.IsNullOrEmpty(pattern))
               throw new ArgumentNullException(nameof(pattern));

            m_Pattern = pattern;
            m_Replacement = replacement ?? string.Empty;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input) =>
            input.IndexOf(m_Pattern) >= 0
                ? input.ToString().Replace(m_Pattern, m_Replacement)
                : input;
    }
}
