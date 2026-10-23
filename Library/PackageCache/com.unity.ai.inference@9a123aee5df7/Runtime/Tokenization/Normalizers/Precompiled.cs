using System;
using System.Collections.Generic;
using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Normalizer that uses a precompiled trie and a byte blob to transform
    /// input text into a normalized form, typically for tokenization.
    /// </summary>
    public partial class PrecompiledNormalizer : INormalizer
    {
        static bool SequenceEquals<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b)
            where T : struct, IEquatable<T>
        {
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }

        static bool SequenceEquals<T>(ReadOnlySpan<T> a, IReadOnlyList<T> b)
            where T : struct, IEquatable<T>
        {
            if (a.Length != b.Count)
                return false;
            for (var i = 0; i < a.Length; i++)
                if (!a[i].Equals(b[i]))
                    return false;
            return true;
        }

        readonly Pool<List<Range>> m_ListOfRangePool = new(() => new(), list => list.Clear());
        readonly Pool<List<char>> m_ListOfCharPool = new(() => new(), list => list.Clear());

        readonly byte[] m_NormalizedBytes;
        readonly DoubleArray m_Trie;

        /// <summary>
        /// Initializes a new instance of the <see cref="PrecompiledNormalizer"/> class.
        /// </summary>
        /// <param name="trieBlob">
        /// Serialized representation of the double-array trie defining the normalization mappings.
        /// </param>
        /// <param name="normalizedBytes">
        /// Read-only span over a byte blob that contains UTF-8 encoded, null-terminated normalized
        /// strings referenced by the trie.
        /// </param>
        public PrecompiledNormalizer(IReadOnlyList<ulong> trieBlob,
            ReadOnlySpan<byte> normalizedBytes)
        {
            m_NormalizedBytes = normalizedBytes.ToArray();
            m_Trie = new(trieBlob);
        }

        int Transform(ReadOnlySpan<char> input, List<char> output)
        {
            var l = SEncoding.UTF8.GetByteCount(input);
            using var _ = DisposablePointer.AllocSpan<byte>(l, out var raw);
            SEncoding.UTF8.GetBytes(input, raw);
            ReadOnlySpan<byte> bytes = raw;

            var commonPrefixFound = m_Trie.CommonPrefixSearch(bytes, out var indexLong);
            if (!commonPrefixFound)
                return -1;

            var index = (int) indexLong;
            var index2 = index;

            if (index < 0 || index >= m_NormalizedBytes.Length)
                return -1;

            while (index2 < m_NormalizedBytes.Length)
            {
                if (m_NormalizedBytes[index2] == 0)
                    break;
                index2++;
            }

            var slice = m_NormalizedBytes.AsSpan(index, index2 - index);
            if (SequenceEquals(slice, bytes))
            {
                foreach (var c in input)
                    output.Add(c);
                return input.Length;
            }

            var outputLength = SEncoding.UTF8.GetCharCount(slice);
            using var charSpanHandle = DisposablePointer.AllocSpan<char>(outputLength, out var charSpan);
            SEncoding.UTF8.GetChars(slice, charSpan);
            foreach (var c in charSpan)
                output.Add(c);
            return outputLength;
        }

        /// <inheritdoc/>
        public SubString Normalize(SubString original)
        {
            if (original.IsNull)
                throw new ArgumentNullException(nameof(original));

            var originalSpan = original.AsSpan();

            using var graphemeRangesHandle = m_ListOfRangePool.Get(out var graphemeRanges);
            TextElementUtility.GetGraphemeRanges(originalSpan, graphemeRanges);

            using var bufHandle = m_ListOfCharPool.Get(out var buf);

            for (var i = 0; i < graphemeRanges.Count; i++)
            {
                var graphemeRange = graphemeRanges[i];
                var grapheme = originalSpan[graphemeRange];

                if (grapheme.Length < 6)
                {
                    var normalizedGrapheme = buf;
                    var transformedCount = Transform(grapheme, normalizedGrapheme);
                    if (transformedCount >= 0)
                        continue;
                }

                using var runeRangesHandle = m_ListOfRangePool.Get(out var runeRanges);
                TextElementUtility.GetRuneRanges(grapheme, runeRanges);

                for (var index = 0; index < runeRanges.Count; index++)
                {
                    var runeRange = runeRanges[index];

                    var rune = grapheme[runeRange];
                    var normalizedRune = buf;
                    var transformedCount = Transform(rune, normalizedRune);

                    if (transformedCount < 0)
                    {
                        foreach (var c in rune)
                            normalizedRune.Add(c);
                    }
                }
            }

            if (SequenceEquals(originalSpan, buf))
                return original;

            return string.Create(buf.Count, buf, (dst, src) =>
            {
                for (var j = 0; j < src.Count; j++)
                    dst[j] =  src[j];
            });
        }
    }
}
