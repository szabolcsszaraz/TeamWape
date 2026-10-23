using System;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Default placeholder implementation of a pre-tokenizer.
    /// Does not pre-cut the input.
    /// </summary>
    public class DefaultPreTokenizer : IPreTokenizer
    {
        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.IsNull)
                throw new ArgumentNullException(nameof(input));

            output.Add(input);
        }
    }
}
