using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    using static Utility;

    /// <summary>
    /// Adds the special tokens needed by a Roberta model.
    /// Surrounds the single sequence with CLS and SEP tokens.
    /// Surrounds the second sequence of a pair and SEP tokens.
    /// </summary>
    public class RobertaPostProcessor : IPostProcessor
    {
        readonly Pool<List<Token>> m_TokenPool;
        readonly Pool<List<List<Token>>> m_SequencePool;

        readonly Token[][] m_SepSequence;
        readonly Token[][] m_ClsSequence;

        /// <summary>
        /// Initializes a new instance of the <see cref="RobertaPostProcessor" /> type.
        /// </summary>
        /// <param name="sep">
        /// The SEP token definition.
        /// </param>
        /// <param name="cls">
        /// The CLS token definition.
        /// </param>
        /// <param name="addPrefixSpace">
        /// Matches the add prefix space options of the pre-tokenization component.
        /// It defines the way the offsets are trimmed out.
        /// Not yet implemented.
        /// </param>
        /// <param name="trimOffsets">
        /// Whether to trim the whitespaces from the produced offsets.
        /// Not yet implemented.
        /// </param>
        public RobertaPostProcessor(Token sep, Token cls, bool addPrefixSpace = true,
            bool trimOffsets = true)
        {
            m_SepSequence = new[] {new[] {sep.SetSpecial(true).SetAttention(true).SetTypeId(0)}};
            m_ClsSequence = new[] {new[] {cls.SetSpecial(true).SetAttention(true).SetTypeId(0)}};
            (m_TokenPool, m_SequencePool) = InitSequencePool();
        }

        /// <inheritdoc />
        public void PostProcess(
            IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool addSpecialTokens,
            Output<IEnumerable<IEnumerable<Token>>> output)
        {
            if (sequenceA == null)
                throw new ArgumentNullException(nameof(sequenceA));

            AddSequence(sequenceA, addSpecialTokens, output, true);

            if (sequenceB is not null)
                AddSequence(sequenceB, addSpecialTokens, output);

            return;

            void AddSequence(
                IReadOnlyList<IReadOnlyList<Token>> pSequence,
                bool pAddSpecialTokens,
                Output<IEnumerable<IEnumerable<Token>>> pOutput,
                bool first = false)
            {
                if (pAddSpecialTokens)
                    pOutput.Add(first ? m_ClsSequence : m_SepSequence);

                using var _ = m_SequencePool.Get(out var sequence);
                for (var seqI = 0; seqI < pSequence.Count; seqI++)
                {
                    var seqTokens = pSequence[seqI];
                    var tokens = m_TokenPool.Get();
                    for (var tI = 0; tI < seqTokens.Count; tI++)
                    {
                        var token = seqTokens[tI];

                        if (pAddSpecialTokens)
                            token = token.SetSpecial(false).SetAttention(true);

                        if (seqI == 0 || pAddSpecialTokens)
                            token = token.SetTypeId(0);

                        tokens.Add(token);
                    }

                    sequence.Add(tokens);
                }

                pOutput.Add(sequence);

                if (pAddSpecialTokens)
                    pOutput.Add(m_SepSequence);
            }
        }

        /// <inheritdoc />
        public int GetNumAddedTokens(bool isPair) => isPair ? 4 : 2;
    }
}
