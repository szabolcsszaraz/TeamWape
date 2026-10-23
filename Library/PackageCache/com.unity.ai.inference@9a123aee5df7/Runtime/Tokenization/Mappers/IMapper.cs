using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Turns an input string into a sequence of token ids.
    /// This is the Hugging Face equivalent of Models.
    /// </summary>
    public interface IMapper
    {
        /// <summary>
        /// Gets the ID of the specified <paramref name="token"/>
        /// </summary>
        /// <param name="token">
        /// The token we want to get the ID of.
        /// </param>
        /// <param name="id">
        /// The ID of the specified <paramref name="token"/>.
        /// </param>
        /// <returns>
        /// Whether the token exists.
        /// </returns>
        bool TokenToId([NotNull] string token, out int id);

        /// <summary>
        /// Gets the token value from the specified <paramref name="id"/>.
        /// </summary>
        /// <param name="id">
        /// The ID of the requested token.
        /// </param>
        /// <returns>
        /// The token value.
        /// </returns>
        [CanBeNull] string IdToToken(int id);

        /// <summary>
        /// Tokenizes a list of string values.
        /// </summary>
        /// <param name="input">
        /// The list of string values to tokenize.
        /// </param>
        /// <param name="output">
        /// The recipient of the converted tokens.
        /// </param>
        void Tokenize([NotNull] IReadOnlyList<SubString> input, Output<Token> output);
    }
}
