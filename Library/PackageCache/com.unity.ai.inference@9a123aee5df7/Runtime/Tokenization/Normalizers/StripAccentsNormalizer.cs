using System.Globalization;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// A text normalizer that removes Unicode combining mark characters from input strings.
    /// Combining marks include diacritical marks, accents, and other modifying characters
    /// that typically combine with base characters.
    /// </summary>
    /// <remarks>
    /// This normalizer is useful in tokenization pipelines where diacritical marks and accents
    /// need to be removed for text processing, such as standardizing text for comparison,
    /// simplifying text for machine learning models, or converting accented characters to their
    /// base forms.
    /// </remarks>
    public class StripAccentsNormalizer : INormalizer
    {
        static bool IsCombiningMark(char c)
        {
            var category = char.GetUnicodeCategory(c);
            return category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.SpacingCombiningMark ||
                category == UnicodeCategory.EnclosingMark;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input)
        {
            var charCount = input.Count( c=> !IsCombiningMark(c));
            if (charCount == 0)
                return string.Empty;

            if(charCount == input.Length)
                return input;

            return string.Create(charCount, input, (span, source) =>
            {
                var index = 0;
                for (var i = 0; i < source.Length; i++)
                {
                    var c = source[i];
                    if (!IsCombiningMark(c))
                    {
                        span[index++] = c;
                    }
                }
            });
        }
    }
}
