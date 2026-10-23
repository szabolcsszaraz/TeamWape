using System;
using System.Collections.Generic;
using System.Globalization;

namespace Unity.InferenceEngine.Tokenization
{
    static class TextElementUtility
    {
        public static int GetUtfLength(ReadOnlySpan<char> span)
        {
            var count = 0;
            for (int i = 0, limit = span.Length; i < limit; i++)
            {
                if (char.IsHighSurrogate(span[i]) && i + 1 < limit
                    && char.IsLowSurrogate(span[i + 1]))
                    i++;

                count++;
            }

            return count;
        }

        public static bool IsCombiningOrModifier(UnicodeCategory uc, int codePoint) =>
            uc switch
            {
                UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
                    UnicodeCategory.EnclosingMark or UnicodeCategory.ModifierLetter or
                    UnicodeCategory.ModifierSymbol => true,
                _ => codePoint is >= 0xFE00 and <= 0xFE0F
            };

        public static int GetGraphemeLength(ReadOnlySpan<char> span, int offset)
        {
            // Start with the base code point
            var codePoint = char.IsHighSurrogate(span[offset]) && offset + 1 < span.Length &&
                char.IsLowSurrogate(span[offset + 1])
                    ? char.ConvertToUtf32(span[offset], span[offset + 1])
                    : span[offset];

            var i = offset + (codePoint > 0xFFFF ? 2 : 1);

            // Consume combining marks, variation selectors, etc.
            while (i < span.Length)
            {
                int cp;
                if (char.IsHighSurrogate(span[i]) && i + 1 < span.Length &&
                    char.IsLowSurrogate(span[i + 1]))
                {
                    cp = char.ConvertToUtf32(span[i], span[i + 1]);
                }
                else
                {
                    cp = span[i];
                }

                var uc = CharUnicodeInfo.GetUnicodeCategory(cp);
                if (!IsCombiningOrModifier(uc, cp))
                    break;

                i += cp > 0xFFFF ? 2 : 1;
            }

            return i - offset;
        }

        public static void GetGraphemeRanges(ReadOnlySpan<char> span, List<Range> output)
        {
            var index = 0;

            while (index < span.Length)
            {
                var len = GetGraphemeLength(span, index);
                output.Add(index .. (index+len));
                index += len;
            }
        }

        public static void GetRuneRanges(ReadOnlySpan<char> s, List<Range> output)
        {
            if (s == null) throw new ArgumentNullException(nameof(s));

            for (var i = 0; i < s.Length; i++)
            {
                if (char.IsHighSurrogate(s[i]) && i + 1 < s.Length && char.IsLowSurrogate(s[i + 1]))
                {
                    output.Add(i..(i + 2));
                    i++;
                }
                else
                {
                    output.Add(i..(i + 1));
                }
            }
        }
    }
}
