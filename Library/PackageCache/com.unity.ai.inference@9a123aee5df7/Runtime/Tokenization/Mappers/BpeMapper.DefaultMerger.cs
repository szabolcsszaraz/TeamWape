using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    partial class BpeMapper
    {
        /// <summary>
        /// The merger type used when no merge rules are given to the <see cref="BpeMapper" />
        /// constructor.
        /// It is a typical passthrough that doesn't modify the input sequence of tokens.
        /// </summary>
        internal class DefaultMerger : IManyToManyConverter<Token, Token>
        {
            public void Convert(
                IReadOnlyList<Token> input,
                Output<Token> output) =>
                output.AddRange(input);
        }
    }
}
