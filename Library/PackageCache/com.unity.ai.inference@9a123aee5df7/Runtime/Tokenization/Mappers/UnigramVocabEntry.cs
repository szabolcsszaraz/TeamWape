namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Represents a vocabulary entry for unigram tokenization, containing a token string and its
    /// associated score.
    /// This structure is used to store token-score pairs in the unigram vocabulary for tokenization
    /// algorithms.
    /// </summary>
    public readonly struct UnigramVocabEntry
    {
        /// <summary>
        /// The token string value for this vocabulary entry.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// The score associated with this token, typically representing its probability or
        /// frequency in the training corpus.
        /// Higher scores generally indicate more common or preferred tokens in the unigram model.
        /// </summary>
        public readonly double Score;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnigramVocabEntry"/> struct with the
        /// specified token value and score.
        /// </summary>
        /// <param name="value">The token string value.</param>
        /// <param name="score">The score associated with the token, typically representing its
        /// probability or frequency.</param>
        public UnigramVocabEntry(string value, double score)
        {
            Value = value;
            Score = score;
        }

        /// <summary>
        /// Deconstructs the vocabulary entry into its constituent parts for tuple deconstruction.
        /// This allows the entry to be used in pattern matching and tuple assignments.
        /// </summary>
        /// <param name="value">When this method returns, contains the token string value.</param>
        /// <param name="score">When this method returns, contains the score associated with the
        /// token.</param>
        /// <example>
        /// <code>
        /// var entry = new UnigramVocabEntry("hello", 0.5);
        /// var (tokenValue, tokenScore) = entry; // Uses Deconstruct method
        /// </code>
        /// </example>
        public void Deconstruct(out string value, out double score)
        {
            value = Value;
            score = Score;
        }
    }
}
