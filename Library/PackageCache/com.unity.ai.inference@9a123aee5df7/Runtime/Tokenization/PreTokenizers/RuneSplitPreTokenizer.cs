namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Splits the input by the runes.
    /// </summary>
    public class RuneSplitPreTokenizer : IPreTokenizer
    {
        readonly SplitDelimiterBehavior m_Behavior;
        readonly bool m_Invert;

        /// <summary>
        /// Initializes a new instance of the <see cref="RuneSplitPreTokenizer"/> type.
        /// </summary>
        /// <param name="behavior">What to do with substrings matching a rune.</param>
        /// <param name="invert">Inverts the pattern matching.</param>
        public RuneSplitPreTokenizer(SplitDelimiterBehavior behavior,
            bool invert = false)
        {
            m_Behavior = behavior;
            m_Invert = invert;
        }

        /// <inheritdoc/>
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (m_Invert && m_Behavior is SplitDelimiterBehavior.Removed)
                return;

            input.GetRunes(output);
        }
    }
}
