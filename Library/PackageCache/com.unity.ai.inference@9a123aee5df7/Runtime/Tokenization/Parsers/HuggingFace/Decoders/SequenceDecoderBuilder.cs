using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Decoders;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Decoders
{
    /// <summary>
    /// A builder class that constructs sequence decoders for HuggingFace tokenization.
    /// This builder creates a composite decoder that applies multiple decoders in sequence.
    /// </summary>
    /// <remarks>
    /// The SequenceDecoderBuilder is responsible for parsing HuggingFace tokenizer configuration
    /// and creating a <see cref="SequenceDecoder"/> that chains multiple decoding operations
    /// together.
    /// Each decoder in the sequence is applied in order to transform tokenized text back to its
    /// original form.
    /// </remarks>
    [HfDecoder("Sequence")]
    public class SequenceDecoderBuilder : IComponentBuilder<IDecoder>
    {
        /// <summary>
        /// The JSON key used to identify the decoders array in the HuggingFace configuration.
        /// </summary>
        const string k_DecodersKey = "decoders";

        /// <summary>
        /// Builds a sequence decoder from the provided HuggingFace configuration parameters.
        /// </summary>
        /// <param name="parameters">
        /// A JSON token containing the decoder configuration.
        /// Must include a <c>decoders</c> array with configuration objects for each decoder to be
        /// included in the sequence.
        /// </param>
        /// <param name="parser">
        /// The HuggingFace parser instance used to build individual decoder components
        /// from their respective configuration objects.
        /// </param>
        /// <returns>
        /// A new <see cref="SequenceDecoder"/> instance that applies all configured decoders
        /// in the order they appear in the configuration array.
        /// </returns>
        /// <exception cref="System.ArgumentException">
        /// Thrown when the parameters do not contain a valid "decoders" array.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown when any of the individual decoder configurations cannot be built by the parser.
        /// </exception>
        public IDecoder Build(JToken parameters, HuggingFaceParser parser)
        {
            var decodersData = parameters.GetArray(k_DecodersKey);

            var decoders = new List<IDecoder>();
            foreach (var decoderData in decodersData)
            {
                var decoder = parser.BuildDecoder(decoderData);
                decoders.Add(decoder);
            }
            return new SequenceDecoder(decoders.ToArray());
        }
    }
}
