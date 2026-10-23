using System;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Applies multiple <see cref="INormalizer"/>.
    /// </summary>
    public class SequenceNormalizer : INormalizer
    {
        readonly INormalizer[] m_Normalizers;

        /// <summary>
        /// Initializes a new instance of the <see cref="SequenceNormalizer" /> type.
        /// </summary>
        /// <param name="normalizers">
        /// The <see cref="INormalizer"/> instances to apply in sequence.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when any of the <paramref name="normalizers"/> is null.
        /// </exception>
        public SequenceNormalizer(params INormalizer[] normalizers)
        {
            if (normalizers.Any(n => n == null))
                throw new ArgumentNullException(nameof(normalizers));

            m_Normalizers = normalizers.ToArray();
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input) =>
            m_Normalizers.Aggregate(input, (current, normalizer) => normalizer.Normalize(current));
    }
}
