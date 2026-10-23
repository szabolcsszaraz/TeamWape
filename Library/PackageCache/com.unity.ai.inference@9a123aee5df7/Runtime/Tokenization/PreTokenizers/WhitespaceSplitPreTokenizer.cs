namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that splits input text on whitespace characters.
    /// </summary>
    /// <remarks>
    /// This pre-tokenizer divides the input string into sub-strings by splitting at whitespace
    /// boundaries.
    /// Whitespace characters are not included in the output tokens.
    /// Consecutive whitespace characters are treated as delimiters and empty strings are not added
    /// to the output.
    /// </remarks>
    public class WhitespaceSplitPreTokenizer : IPreTokenizer
    {
        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            var (from, to) = (0, 0);
            while (to < input.Length)
            {
                var c = input[to];
                if (char.IsWhiteSpace(c))
                {
                    if (to > from)
                        output.Add(input[from..to]);
                    from = to + 1;
                }
                to++;
            }

            if (to > from)
                output.Add(input[from..to]);
        }
    }
}
