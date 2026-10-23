using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    using static Utility;

    /// <summary>
    /// ByteLevel post processor only concatenates the pair sequences.
    /// The former implementation from Hugging Face trims offsets of tokenized strings, but this
    /// implementation does support offsets.
    /// </summary>
    public class ByteLevelPostProcessor : IPostProcessor
    {
        readonly Pool<List<Token>> m_TokenPool;
        readonly Pool<List<List<Token>>> m_SequencePool;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteLevelPostProcessor"/> type.
        /// </summary>
        /// <param name="trimOffsets">
        /// Whether to trim the whitespaces from the produced offsets.
        /// Not yet implemented.
        /// </param>
        public ByteLevelPostProcessor(bool trimOffsets = false)
        {
            (m_TokenPool, m_SequencePool) = InitSequencePool();
        }

        /// <inheritdoc />
        public void PostProcess(
            IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool _,
            Output<IEnumerable<IEnumerable<Token>>> output)
        {
            if (sequenceA is null)
                throw new ArgumentNullException(nameof(sequenceA));

            AddSequence(sequenceA, 0, output);
            if (sequenceB != null)
                AddSequence(sequenceB, 1, output);

            return;

            void AddSequence(
                [NotNull]IReadOnlyList<IReadOnlyList<Token>> pSequence,
                int typeId,
                Output<IEnumerable<IEnumerable<Token>>> pOutput)
            {
                Assert.IsNotNull(pSequence);

                using var sequenceHandle = m_SequencePool.Get(out var sequence);
                for (var sI = 0; sI < pSequence.Count; sI++)
                {
                    var seqTokens = pSequence[sI];

                    if (seqTokens is null)
                        throw new ArgumentNullException(nameof(pSequence));

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
