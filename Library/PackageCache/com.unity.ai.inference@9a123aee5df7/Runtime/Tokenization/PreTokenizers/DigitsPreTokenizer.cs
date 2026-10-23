namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that splits input text at digit boundaries.
    /// This class separates numeric digits from non-numeric characters during the pre-tokenization
    /// phase.
    /// </summary>
    /// <remarks>
    /// The tokenizer can operate in two modes:
    /// <list type="bullet">
    ///     <item>
    ///         <description>
    ///             Grouped mode: Consecutive digits are kept together as a single token (e.g.,
    ///             "abc123def" → ["abc", "123", "def"]).
    ///         </description>
    ///     </item>
    ///     <item>
    ///         <description>
    ///             Individual mode: Each digit is separated into its own token (e.g.,
    ///             "abc123def" → ["abc", "1", "2", "3", "def"]).
    ///         </description>
    ///     </item>
    /// </list>
    /// </remarks>
    public class DigitsPreTokenizer : IPreTokenizer
    {
        readonly bool m_IndividualDigits;

        /// <summary>
        /// Initializes a new instance of the <see cref="DigitsPreTokenizer"/> class.
        /// </summary>
        /// <param name="individualDigits">
        /// If <c>true</c>, each digit is split into its own token;
        /// if <c>false</c>, consecutive digits are grouped together as a single token.
        /// Default is <c>false</c>.
        /// </param>
        public DigitsPreTokenizer(bool individualDigits = false) =>
            m_IndividualDigits = individualDigits;

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.IsEmpty)
                return;

            var from = 0;

            while (from < input.Length)
            {
                var c = input[from];
                var to = from + 1;

                if (char.IsNumber(c))
                {
                    if (!m_IndividualDigits)
                    {
                        while (to < input.Length && char.IsNumber(input[to]))
                            to++;
                    }
                }
                else
                {
                    while (to < input.Length && !char.IsNumber(input[to]))
                        to++;
                }
                output.Add(input[from..to]);
                from = to;
            }
        }
    }
}
