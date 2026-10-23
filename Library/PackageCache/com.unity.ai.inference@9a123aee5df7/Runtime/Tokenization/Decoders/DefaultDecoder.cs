using System.Collections.Generic;
using UnityEngine.Assertions;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Default decoder.
    /// Does not change the input chunks.
    /// </summary>
    public class DefaultDecoder : IDecoder
    {
        /// <inheritdoc />
        public void Decode(
            IReadOnlyList<string> tokens,
            Output<string> output)
        {
            Assert.IsNotNull(tokens);
            output.Add(string.Join(" ", tokens));
        }
    }
}
