using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Implements a unigram-based tokenization mapper that converts text into tokens using a
    /// vocabulary-based approach.
    /// This mapper supports byte-level fallback for handling out-of-vocabulary characters.
    /// </summary>
    public partial class UnigramMapper : IMapper
    {

        const double K_UnkPenalty = 10;

        static readonly string[] k_ByteFallback;

        static UnigramMapper()
        {
            k_ByteFallback = Enumerable.Range(0, byte.MaxValue + 1).Select(i => $"<0x{i:X2}>")
                .ToArray();
        }

        readonly Pool<List<VocabEntry>> m_ListOfVocabEntryPool =
            new(() => new(), list => list.Clear());

        readonly Pool<List<ByteString>> m_ListOfByteStringPool =
            new(() => new(), list => list.Clear());

        readonly VocabEntry[] m_Vocab;

        readonly Dictionary<SubString, VocabEntry> m_TokenToEntry;

        readonly Dictionary<SubString, ByteString[]> m_Cache = new();

        readonly PrefixTrie m_PrefixTrie;

        readonly double m_MinScore;

        readonly VocabEntry m_Unk;

        readonly bool m_ByteFallback;

        /// <summary>
        /// Initializes a new instance of the UnigramMapper with the specified vocabulary and
        /// unknown token configuration.
        /// </summary>
        /// <param name="vocab">The vocabulary containing token entries with their scores.</param>
        /// <param name="unkId">The ID of the unknown token in the vocabulary.</param>
        /// <param name="byteFallback">Whether to enable byte-level fallback for out-of-vocabulary
        /// characters.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="vocab"/> is null.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="unkId"/> is
        /// outside the vocabulary range.</exception>
        public UnigramMapper([NotNull] IReadOnlyList<UnigramVocabEntry> vocab, int unkId = -1,
            bool byteFallback = false)
        {
            if (vocab == null)
                throw new ArgumentNullException(nameof(vocab));

            if (unkId >= vocab.Count)
                throw new ArgumentOutOfRangeException(nameof(unkId), unkId,
                    $"Must be in the range of the {nameof(vocab)} size");

            m_MinScore = double.MaxValue;

            m_Vocab = new VocabEntry[vocab.Count];
            for (var id = 0; id < vocab.Count; id++)
            {
                var (value, score) = vocab[id];
                m_Vocab[id] = new(id, value, score);

                if (score < m_MinScore)
                    m_MinScore = score;
            }

            m_Unk = unkId >= 0 ? m_Vocab[unkId] : null;

            m_ByteFallback = byteFallback;

            m_TokenToEntry = m_Vocab.ToDictionary(e => (SubString) e.Value, e => e);

            // Initializes the trie
            m_PrefixTrie = new();
            foreach (var entry in m_Vocab)
                m_PrefixTrie.Push(entry);
        }

        /// <inheritdoc />
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="token"/> is null.
        /// </exception>
        public bool TokenToId(string token, out int id) => token == null
            ? throw new ArgumentNullException(nameof(token))
            : TokenToId((SubString) token, out id);

        /// <inheritdoc />
        /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="id"/> is
        /// outside the vocabulary range.</exception>
        public string IdToToken(int id)
        {
            return id < 0 || id >= m_Vocab.Length
                ? throw new ArgumentOutOfRangeException(nameof(id),
                    "Must be in the range of the vocab size")
                : m_Vocab[id].Value;
        }

        /// <inheritdoc />
        public void Tokenize(IReadOnlyList<SubString> input, Output<Token> output)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            for (var i = 0; i < input.Count; i++)
                Tokenize(input[i], output);
        }

        bool TokenToId(SubString token, out int id)
        {
            if (m_TokenToEntry.TryGetValue(token, out var entry))
            {
                id = entry.Id;
                return true;
            }
            id = 0;
            return false;
        }

        void Tokenize(SubString input, Output<Token> output)
        {
            // leverage caching
            var cacheFound = m_Cache.TryGetValue(input, out var byteStrings);
            if (!cacheFound)
            {
                using var handle = m_ListOfByteStringPool.Get(out var encoded);
                Encode(input, encoded);
                byteStrings = encoded.ToArray();
                m_Cache.Add(input.Apply(), byteStrings);
            }

            var offset = 0;

            foreach (var byteString in byteStrings)
            {
                var byteLength = byteString.Bytes.Length;
                var offsets = offset .. (offset + byteLength);
                var idFound = TokenToId(byteString.Value, out var id);

                if (idFound)
                {
                    output.Add(new(id, byteString.Value, offsets: offsets));
                    offset += byteLength;
                    continue;
                }

                // id not found

                if (m_ByteFallback)
                {
                    var someIdsFound = false;
                    foreach (var @byte in byteString.Bytes)
                    {
                        var byteRepr = k_ByteFallback[@byte];
                        var entryFound =
                            m_TokenToEntry.TryGetValue(byteRepr, out var byteEntry);
                        someIdsFound |= entryFound;
                        if (entryFound)
                        {
                            var token = new Token(byteEntry.Id, byteEntry.Value,
                                offsets: offsets);
                            output.Add(token);
                        }
                    }
                    if (someIdsFound)
                    {
                        offset += byteLength;
                        continue;
                    }
                }

                // byteFallback is false or no fallback found

                if (m_Unk is null)
                    throw new InvalidOperationException("Cannot fallback to unk token as it has not been defined.");

                id = m_Unk.Id;
                output.Add(new(id, byteString.Value, offsets: offsets));
                offset += byteLength;
            }
        }

        void Encode(SubString input, List<ByteString> output)
        {
            if (input.IsEmpty)
                return;

            var inputByteLength = SEncoding.UTF8.GetByteCount(input);
            var unkScore = m_MinScore - K_UnkPenalty;

            Range offset = default;
            using var resultHandle = m_ListOfByteStringPool.Get(out var results);

            using var pBestPathEndsAtHandle = DisposablePointer.AllocSpan<BestPathNode>(inputByteLength + 1, out var bestPathEndsAt);
            using var pInputBytesHandle = DisposablePointer.AllocSpan<byte>(inputByteLength, out var inputBytes);

            for (var i = 0; i < bestPathEndsAt.Length; i++)
                bestPathEndsAt[i] = default;

            var startsAtByte = 0;

            SEncoding.UTF8.GetBytes(input.AsSpan(), inputBytes);

            using var prefixesHandle = m_ListOfVocabEntryPool.Get(out var prefixes);

            var subString = input;
            while (startsAtByte < inputByteLength)
            {
                var currentBestPathScore = bestPathEndsAt![startsAtByte].BestPathScore;
                var hasSingleNode = false;
                var firstUtfChar = subString.UtfSub(..1);
                var firstCharUtfLength = firstUtfChar.Utf8Length;

                m_PrefixTrie.CommonPrefixSearch(inputBytes[startsAtByte ..], prefixes);

                for (var i = 0; i < prefixes.Count; i++)
                {
                    var (prefixId, _, prefixScore, prefixBytes) = prefixes[i];
                    var endsAtByte = startsAtByte + prefixBytes.Length;

                    ref var targetNode = ref bestPathEndsAt[endsAtByte];

                    var candidateBestPathScore = prefixScore + currentBestPathScore;

                    if (!targetNode.StartsAt.HasValue
                        || candidateBestPathScore > targetNode.BestPathScore)
                    {
                        targetNode.BestPathScore = candidateBestPathScore;
                        targetNode.StartsAt = startsAtByte;
                        targetNode.Id = prefixId;
                    }

                    if (!hasSingleNode && prefixBytes.Length == firstCharUtfLength)
                        hasSingleNode = true;
                }

                if (!hasSingleNode)
                {
                    ref var targetNode = ref bestPathEndsAt[startsAtByte + firstCharUtfLength];

                    var candidateBestPathScore = unkScore + currentBestPathScore;

                    if (!targetNode.StartsAt.HasValue
                        || candidateBestPathScore > targetNode.BestPathScore)
                    {
                        if(m_Unk is null)
                            throw new InvalidOperationException("Cannot fallback to unk token as it has not been defined.");

                        targetNode.BestPathScore = candidateBestPathScore;
                        targetNode.StartsAt = startsAtByte;
                        targetNode.Id = m_Unk.Id;
                    }
                }

                prefixes.Clear();
                startsAtByte += firstCharUtfLength;
                subString = subString.UtfSub(1..);
            }


            var endsAt = inputByteLength;
            while (endsAt > 0)
            {
                ref var node = ref bestPathEndsAt[endsAt];

                var nodeStartsAt = node.StartsAt!.Value;

                if (m_Unk is not null && node.Id == m_Unk.Id)
                {
                    offset = offset.End.Value == 0
                        ? nodeStartsAt .. endsAt
                        : nodeStartsAt .. offset.End;
                }
                else
                {
                    Fuse(inputBytes, ref offset, results);
                    var entry = m_Vocab[node.Id];
                    results.Add(new(entry.Value, entry.Bytes));
                }

                endsAt = nodeStartsAt;
            }

            Fuse(inputBytes, ref offset, results);

            results.Reverse();
            output.AddRange(results);

            return;

            void Fuse(ReadOnlySpan<byte> pSource, ref Range pOffset, List<ByteString> pOutput)
            {
                if (pOffset.End.Value == 0)
                    return;

                var unknownBytes = pSource[pOffset];
                var unknownString = SEncoding.UTF8.GetString(unknownBytes);
                pOutput.Add(new(unknownString, unknownBytes.ToArray()));

                pOffset = default;
            }
        }
    }
}
