using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.Padding
{
    /// <summary>
    /// Pads the sequences of tokens by adding tokens to the left.
    /// </summary>
    public class LeftPadding : DirectionalPaddingBase
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="LeftPadding" /> type.
        /// </summary>
        /// <param name="paddingSizeProvider">
        /// When applying the padding, this object provides the final size of the padded sequence.
        /// </param>
        /// <param name="padToken">
        /// The token to use to pad a sequence of tokens.
        /// </param>
        public LeftPadding(
            [NotNull] IPaddingSizeProvider paddingSizeProvider,
            Token padToken) : base(paddingSizeProvider, padToken)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LeftPadding" /> type.
        /// </summary>
        /// <param name="paddingSizeProvider">
        /// When applying the padding, this object provides the final size of the padded sequence.
        /// </param>
        /// <param name="padToken">
        /// The token to use to pad a sequence of tokens.
        /// </param>
        /// <param name="padToMultipleOf">
        /// Sets the pad length to the upper multiple of this value.
        /// </param>
        public LeftPadding(
            [NotNull] IPaddingSizeProvider paddingSizeProvider,
            Token padToken, int padToMultipleOf = 1) : base(paddingSizeProvider, padToken, padToMultipleOf)
        {
        }

        /// <inheritdoc />
        protected override void PadSequence(IReadOnlyList<Token> tokens, int padSize,
            Output<Token> output)
        {
            Assert.IsNotNull(tokens);

            for (int i = 0, limit = padSize - tokens.Count; i < limit; i++)
                output.Add(PadToken.SetAttention(false).SetSpecial(true));

            for (int i = 0, _ = tokens.Count; i < _; i++)
                output.Add(tokens[i].SetAttention(true));
        }
    }
}
