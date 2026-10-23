using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    using static Utility;

    /// <summary>
    /// Interlaces the primary and secondary sequences of tokens.
    /// </summary>
    public class DefaultPostProcessor : IPostProcessor
    {
        readonly Pool<List<Token>> m_TokenPool;
        readonly Pool<List<List<Token>>> m_SequencePool;

        /// <summary>
        /// Initializes a new instance of the <see cref="DefaultPostProcessor"/> type.
        /// </summary>
        public DefaultPostProcessor() => (m_TokenPool, m_SequencePool) = InitSequencePool();

        /// <inheritdoc />
        public void PostProcess(
            IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool _,
            Output<IEnumerable<IEnumerable<Token>>> output)
        {
            if (sequenceA is null)
                throw new System.ArgumentNullException(nameof(sequenceA));

            AddSequence(sequenceA, 0, output);
            if (sequenceB != null)
                AddSequence(sequenceB, 1, output);

            return;

            void AddSequence(
                [NotNull] IReadOnlyList<IReadOnlyList<Token>> pSequence,
                int typeId,
                Output<IEnumerable<IEnumerable<Token>>> pOutput)
            {
                Assert.IsNotNull(pSequence);

                using var sequenceHandle = m_SequencePool.Get(out var sequence);
                for (var seqI = 0; seqI < pSequence.Count; seqI++)
                {
                    var seqTokens = pSequence[seqI];
                    var tokens = m_TokenPool.Get();
                    for (var tI = 0; tI < seqTokens.Count; tI++)
                        tokens.Add(seqTokens[tI].SetTypeId(typeId));

                    sequence.Add(tokens);
                }

                pOutput.Add(sequence);
            }
        }

        /// <inheritdoc />
        public int GetNumAddedTokens(bool _) => 0;
    }
}
