using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Truncators
{
    /// <summary>
    /// Placeholder implementation of the truncation.
    /// Does not truncate, only concatenates the primary and secondary sequence of tokens.
    /// </summary>
    public class DefaultTruncator : ITruncator
    {
        /// <summary>
        /// Gets a singleton instance of the default truncator.
        /// </summary>
        public static ITruncator Instance { get; } = new DefaultTruncator();

        /// <inheritdoc />
        public void Truncate(
            IReadOnlyList<Token> inputA,
            IReadOnlyList<Token> inputB,
            int numAddedTokens,
            Output<IEnumerable<Token>> outputA,
            Output<IEnumerable<Token>> outputB)
        {
            if (inputA == null)
                throw new ArgumentNullException(nameof(inputA));

            outputA.Add(inputA);

            if (inputB is not null)
                outputB.Add(inputB);
        }
    }
}
