using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    public partial class Tokenizer
    {
        internal class SequenceMerger
        {
            readonly Pool<List<Token>> m_TokensPool;
            readonly Pool<List<IReadOnlyList<Token>>> m_SequencePool;
            readonly Pool<List<List<IReadOnlyList<Token>>>> m_MergePool;

            public SequenceMerger(Pool<List<Token>> tokensPool)
            {
                m_TokensPool = tokensPool;

                m_SequencePool = new(() => new(), sequencePool => sequencePool.Clear());


                m_MergePool = new(
                    () => new(), merge =>
                    {
                        foreach (var sequence in merge)
                            m_SequencePool.Release(sequence);

                        merge.Clear();
                    });
            }

            public void Merge(
                IReadOnlyList<IReadOnlyList<IReadOnlyList<Token>>> sequences,
                Output<IEnumerable<Token>> output)
            {
                var merged = m_MergePool.Get();

                foreach (var sequence in sequences)
                {
                    var newMerged = MergeSequence(sequence, merged);
                    m_MergePool.Release(merged);
                    merged = newMerged;
                }

                Apply(merged, output);

                m_MergePool.Release(merged);
            }

            List<List<IReadOnlyList<Token>>> MergeSequence(
                [NotNull] IReadOnlyList<IReadOnlyList<Token>> sequence,
                [NotNull] List<List<IReadOnlyList<Token>>> merged)
            {
                if (sequence.Count == 0)
                    throw new ArgumentNullException(nameof(sequence));

                var output = m_MergePool.Get();

                // merging first tokens of the sequence into with the first tokens of the merged
                // sequences.
                {
                    var newSequence = m_SequencePool.Get();
                    if (merged.Count >= 1)
                        newSequence.AddRange(merged[0]); // merge main tokens
                    newSequence.Add(sequence[0]);
                    output.Add(newSequence);
                }

                // skip main tokens, only merging overflowing
                foreach (var mergedSequence in merged.Skip(1))
                {
                    foreach (var tokens in sequence)
                    {
                        var newSeq = m_SequencePool.Get();
                        newSeq.AddRange(mergedSequence);
                        newSeq.Add(tokens);
                        output.Add(newSeq);
                    }
                }

                // skip main tokens, only merging overflowing
                foreach (var tokens in sequence.Skip(1))
                {
                    var newSequence = m_SequencePool.Get();
                    if (merged.Count >= 1)
                        newSequence.AddRange(merged[0]);
                    newSequence.Add(tokens);
                    output.Add(newSequence);
                }

                return output;
            }

            void Apply(List<List<IReadOnlyList<Token>>> merged, Output<IEnumerable<Token>> output)
            {
                using var _ = m_TokensPool.Get(out var mergedTokens);
                foreach (var sequence in merged)
                {
                    foreach (var tokens in sequence)
                        mergedTokens.AddRange(tokens);

                    output.Add(mergedTokens);
                    mergedTokens.Clear();
                }
            }
        }
    }
}
