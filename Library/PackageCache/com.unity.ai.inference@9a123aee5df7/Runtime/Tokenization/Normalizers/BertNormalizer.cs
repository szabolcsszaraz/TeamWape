using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Normalizes raw text input for Bert model.
    /// </summary>
    public class BertNormalizer : INormalizer
    {
        /// <summary>
        /// Tells whether <paramref name="c" /> is a CJK Unicode block character.
        /// </summary>
        /// <param name="c">
        /// The character to test.
        /// </param>
        /// <returns>
        /// Whether <paramref name="c" /> is a chinese character.
        /// </returns>
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_(Unicode_block)" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_A" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_B" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_C" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_D" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_E" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_Extension_F" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Compatibility_Ideographs" />
        /// <seealso href="https://en.wikipedia.org/wiki/CJK_Compatibility_Ideographs_Supplement" />
        static bool IsCjk(char c) =>
            Convert.ToUInt32(c) is
                // CJK Unified Ideographs
                >= 0x4e00 and <= 0x9fff or
                // CJK Compatibility Ideographs
                >= 0xf900 and <= 0xfaff or
                // CJK Unified Ideographs Extension A
                >= 0x3400 and <= 0x4dbf or
                // CJK Unified Ideographs Extension B
                >= 0x20000 and <= 0x2a6df or
                // CJK Unified Ideographs Extension C
                >= 0x2a700 and <= 0x2b739 or
                // CJK Unified Ideographs Extension D
                >= 0x2b740 and <= 0x2b81f or
                // CJK Unified Ideographs Extension E
                >= 0x2b820 and <= 0x2ceaf or
                // CJK Compatibility Ideographs Supplement
                >= 0x2f800 and <= 0x2fa1f;

        /// <summary>
        /// Tells whether the given character is a replacement character.
        /// </summary>
        /// <param name="c">
        /// The character to test.
        /// </param>
        /// <returns>
        /// Whether the given character is a replacement character.
        /// </returns>
        static bool IsReplacementChar(char c) => Convert.ToUInt32(c) == 0xfffd;

        static string ToString(List<char> input) => string.Create(input.Count, input, CopyChars);

        static void CopyChars(Span<char> target, List<char> source)
        {
            for (var i = 0; i < target.Length; i++)
                target[i] = source[i];
        }

        /// <summary>
        /// Cleans the <paramref name="input" /> from replacement characters, whitespaces and
        /// control characters.
        /// </summary>
        /// <param name="input">
        /// The sequence of <see cref="char" /> to clean.
        /// </param>
        /// <param name="output">
        /// The target of the cleaned chars.
        /// </param>
        static void ApplyCleanText(List<char> input, List<char> output)
        {
            for (var i = 0; i < input.Count; i++)
            {
                var c = input[i];
                if (IsReplacementChar(c))
                    continue;

                if (char.IsWhiteSpace(c))
                    output.Add(' ');
                else if (!char.IsControl(c))
                    output.Add(c);
            }
        }

        /// <summary>
        /// Surround CJK characters with a single whitespace.
        /// </summary>
        /// <param name="input">
        /// The sequence of <see cref="char" /> in which to search for CJK characters.
        /// </param>
        /// <param name="output">
        /// The target of the updated sequence of <see cref="char" />.
        /// </param>
        static void ApplyHandleCjkChars(List<char> input, List<char> output)
        {
            foreach (var c in input)
            {
                var isCjk = IsCjk(c);
                if (isCjk)
                    output.Add(' ');
                output.Add(c);
                if (isCjk)
                    output.Add(' ');
            }
        }

        /// <summary>
        /// Removes accents using <see cref="NormalizationForm.FormD" /> and ignore
        /// <see cref="UnicodeCategory.NonSpacingMark" /> characters.
        /// </summary>
        /// <param name="input">
        /// The sequence of <see cref="char" /> to update.
        /// </param>
        /// <param name="output">
        /// The target of the updated sequence of <see cref="char" />.
        /// </param>
        static void ApplyStripAccents(List<char> input, List<char> output)
        {
            // Find a non-allocating solution
            var s = ToString(input).Normalize(NormalizationForm.FormD);

            foreach (var c in s)
            {
                if (char.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    output.Add(c);
            }
        }

        /// <summary>
        /// Turns the <see cref="char" /> of the <paramref name="input" /> into their lowercase
        /// version.
        /// </summary>
        /// <param name="input">
        /// The sequence of <see cref="char" /> to update.
        /// </param>
        /// <param name="output">
        /// The target of the updated sequence.
        /// </param>
        static void ApplyLowerCase(List<char> input, List<char> output)
        {
            foreach (var c in input)
                output.Add(char.ToLowerInvariant(c));
        }

        readonly Pool<List<char>> m_ListOfCharPool = new(() => new(), list => list.Clear());

        /// <summary>
        /// If <see langword="true" />, removes control characters and replaces whitespaces by the
        /// classic one.
        /// </summary>
        readonly bool m_CleanText;

        /// <summary>
        /// If <see langword="true" />, puts spaces around each chinese character.
        /// </summary>
        readonly bool m_HandleCjkChars;

        /// <summary>
        /// If <see langword="true" />, strips all accents.
        /// </summary>
        readonly bool m_StripAccents;

        /// <summary>
        /// If <see langword="true" />, converts the input to lowercase.
        /// </summary>
        readonly bool m_LowerCase;


        /// <summary>
        /// Initializes a new instance of the type <see cref="BertNormalizer" />
        /// </summary>
        /// <param name="cleanText">
        /// If <see langword="true" />, removes control characters and replaces whitespaces by the
        /// classic one.
        /// </param>
        /// <param name="handleCjkChars">
        /// If <see langword="true" />, puts spaces around each chinese character.
        /// </param>
        /// <param name="stripAccents">
        /// If <see langword="true" />, strips all accents.
        /// If set to <see langword="null" />, it takes the value of <paramref name="lowerCase" />
        /// (original BERT implementation).
        /// </param>
        /// <param name="lowerCase">
        /// If <see langword="true" />, converts the input to lowercase.
        /// </param>
        public BertNormalizer(
            bool cleanText = true,
            bool handleCjkChars = true,
            bool? stripAccents = null,
            bool lowerCase = true)
        {
            m_CleanText = cleanText;
            m_HandleCjkChars = handleCjkChars;
            m_LowerCase = lowerCase;
            m_StripAccents = stripAccents ?? lowerCase;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input)
        {
            using var buf0Handle = m_ListOfCharPool.Get(out var buff0);
            using var buf1Handle = m_ListOfCharPool.Get(out var buff1);

            var (charsA, charsB) = (buff0, buff1);

            charsA.AddRange(input);

            if (m_CleanText)
            {
                ApplyCleanText(charsA, charsB);
                SwapAndClear(ref charsA, ref charsB);
            }

            if (m_HandleCjkChars)
            {
                ApplyHandleCjkChars(charsA, charsB);
                SwapAndClear(ref charsA, ref charsB);
            }

            if (m_StripAccents)
            {
                ApplyStripAccents(charsA, charsB);
                SwapAndClear(ref charsA, ref charsB);
            }

            if (m_LowerCase)
            {
                ApplyLowerCase(charsA, charsB);
                SwapAndClear(ref charsA, ref charsB);
            }

            return ToString(charsA);

            void SwapAndClear(ref List<char> a, ref List<char> b)
            {
                (a, b) = (b, a);
                b.Clear();
            }
        }
    }
}
