using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Applies a sequence of pre tokenizers.
    /// </summary>
    public class SequencePreTokenizer : IPreTokenizer
    {
        readonly Pool<List<SubString>> m_ListOfSubStringPool =
            new(() => new(), list => list.Clear());

        readonly IPreTokenizer[] m_PreTokenizers;

        /// <summary>
        /// Initializes a new instance of the <see cref="SequencePreTokenizer"/> type.
        /// </summary>
        /// <param name="preTokenizers">
        /// Sequence of pre-tokenizers to apply.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="preTokenizers"/> cannot be null.
        /// </exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="preTokenizers"/> cannot be empty.
        /// </exception>
        public SequencePreTokenizer(
            [NotNull] params IPreTokenizer[] preTokenizers)
        {
            if (preTokenizers == null)
                throw new ArgumentNullException(nameof(preTokenizers));

            if (preTokenizers.Length == 0)
                throw new ArgumentException(
                    "At least one preTokenizer is required", nameof(preTokenizers));

            if (preTokenizers.Any(t => t is null))
                throw new ArgumentNullException(
                    nameof(preTokenizers), $"None of the {nameof(preTokenizers)} can be null.");

            m_PreTokenizers = preTokenizers.ToArray();
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if(input.IsNull)
                throw new ArgumentNullException(nameof(input));

            using var listAHandle = m_ListOfSubStringPool.Get(out var listA);
            using var listBHandle = m_ListOfSubStringPool.Get(out var listB);

            var (preTokInput, preTokOutput) = (listA, listB);
            preTokOutput.Add(input);

            foreach (var preTokenizer in m_PreTokenizers)
            {
                (preTokInput, preTokOutput) = (preTokOutput, preTokInput);

                for (int pI = 0, pLimit = preTokInput.Count; pI < pLimit; pI++)
                {
                    var preTok = preTokInput[pI];
                    preTokenizer.PreTokenize(preTok, preTokOutput.AsOutput());
                }

                preTokInput.Clear();
            }

            output.AddRange(preTokOutput);
        }
    }
}
