using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.PostProcessors
{
    /// <summary>
    /// Applies multiple <see cref="IPostProcessor"/>.
    /// </summary>
    public class SequencePostProcessor : IPostProcessor
    {
        readonly Pool<List<List<Token>>> m_ListOfListOfTokenPool =
            new(() => new(), list => list.Clear());

        readonly Pool<List<Token>> m_ListOfTokenPool = new(() => new(), list => list.Clear());
        readonly Pool<PostProcessOutput> m_PostProcessOutputPool;

        readonly IPostProcessor[] m_Processors;

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencePostProcessor"/> type.
        /// </summary>
        /// <param name="processors">
        /// The <see cref="IPostProcessor"/> instances to call in sequence.
        /// </param>
        public SequencePostProcessor(params IPostProcessor[] processors)
        {
            if (processors.Any(n => n == null))
                throw new ArgumentNullException(nameof(processors));

            m_PostProcessOutputPool = new(
                () => new(m_ListOfListOfTokenPool, m_ListOfTokenPool),
                output => output.Reset());

            m_Processors = processors.ToArray();
        }

        /// <inheritdoc />
        public int GetNumAddedTokens(bool isPair)
        {
            var count = 0;
            foreach (var processor in m_Processors)
                count += processor.GetNumAddedTokens(isPair);
            return count;
        }

        /// <inheritdoc />
        public void PostProcess(
            IReadOnlyList<IReadOnlyList<Token>> sequenceA,
            IReadOnlyList<IReadOnlyList<Token>> sequenceB,
            bool addSpecialTokens,
            Output<IEnumerable<IEnumerable<Token>>> output)
        {
            using var handleA = m_PostProcessOutputPool.Get(out var ppA);
            using var handleB = m_PostProcessOutputPool.Get(out var ppB);

            var ppInput = ppA;
            var ppOutput = ppB;

            ppOutput.Add(new [] {sequenceA});
            if(sequenceB!= null)
                ppOutput.Add(new [] {sequenceB});

            foreach (var processor in m_Processors)
            {
                (ppInput, ppOutput) = (ppOutput, ppInput);
                ppOutput.Reset();
                processor.PostProcess(ppInput[0], ppInput.Count >= 2 ? ppInput[1] : null,
                    addSpecialTokens, ppOutput.AsOutput());
            }

            foreach (var sequence in ppOutput)
                output.Add(sequence);
        }
    }
}
