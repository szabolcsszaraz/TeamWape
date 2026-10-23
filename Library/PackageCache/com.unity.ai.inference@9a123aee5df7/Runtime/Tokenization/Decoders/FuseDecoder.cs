using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Fuse Decoder combine the tokens in list into a single large token.
    /// </summary>
    public class FuseDecoder : IDecoder
    {
        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            output.Add(string.Concat(tokens));
        }
    }
}
