using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// A composite decoder that applies multiple decoders in sequence to process tokens.
    /// Each decoder in the sequence processes the output from the previous decoder,
    /// creating a pipeline for token transformation.
    /// </summary>
    /// <remarks>
    /// This class implements the Composite pattern to chain multiple decoders together.
    /// The decoders are applied in the order they are provided in the constructor.
    /// Object pooling is used internally to optimize memory allocation for intermediate results.
    /// </remarks>
    /// <example>
    /// <code>
    /// var decoder1 = new SomeDecoder();
    /// var decoder2 = new AnotherDecoder();
    /// var sequenceDecoder = new SequenceDecoder(decoder1, decoder2);
    ///
    /// var tokens = new List&lt;string&gt; { "token1", "token2" };
    /// var output = new List&lt;string&gt;();
    /// sequenceDecoder.Decode(tokens, output.AsOutput());
    /// </code>
    /// </example>
    public class SequenceDecoder : IDecoder
    {
        readonly IDecoder[] m_Decoders;
        readonly Pool<List<string>> m_ListOfStringPool = new(() => new(), list => list.Clear());

        /// <summary>
        /// Initializes a new instance of the <see cref="SequenceDecoder"/> class
        /// with the specified sequence of decoders.
        /// </summary>
        /// <param name="decoders">
        /// A sequence of decoders to be applied in order. Each decoder will process
        /// the output from the previous decoder in the sequence.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="decoders"/> contains any null decoder instances.
        /// </exception>
        /// <remarks>
        /// The decoders will be applied in the exact order they are provided.
        /// A copy of the decoder array is made internally to prevent external modifications.
        /// </remarks>
        public SequenceDecoder(params IDecoder[] decoders)
        {
            if (decoders.Any(decoder => decoder is null))
                throw new ArgumentNullException(nameof(decoders));

            m_Decoders = decoders.ToArray();
        }

        /// <summary>
        /// Decodes the input tokens by applying each decoder in the sequence.
        /// </summary>
        /// <param name="tokens">
        /// The input tokens to be decoded. This collection is processed by the first decoder,
        /// and subsequent decoders process the output from the previous decoder.
        /// </param>
        /// <param name="output">
        /// The output collection where the final decoded results will be added.
        /// This will contain the tokens after being processed by all decoders in sequence.
        /// </param>
        /// <remarks>
        /// <para>
        /// The decoding process works as follows:
        /// <list type="number">
        /// <item>The input tokens are copied to an intermediate buffer</item>
        /// <item>Each decoder in the sequence processes the current intermediate buffer</item>
        /// <item>The output of each decoder becomes the input for the next decoder</item>
        /// <item>The final result is added to the output collection</item>
        /// </list>
        /// </para>
        /// <para>
        /// Two intermediate List&lt;string&gt; instances are used and swapped between iterations
        /// to avoid unnecessary memory allocations. These lists are obtained from an object pool
        /// and automatically returned when the method completes.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// var tokens = new List&lt;string&gt; { "Hello", "World" };
        /// var output = new List&lt;string&gt;();
        /// decoder.Decode(tokens, output.AsOutput());
        /// // output now contains the tokens processed by all decoders in sequence
        /// </code>
        /// </example>
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            using var listAHandle = m_ListOfStringPool.Get(out var listA);
            using var listBHandle = m_ListOfStringPool.Get(out var listB);

            var (intermediateInput, intermediateOutput) = (listA, listB);
            intermediateOutput.AddRange(tokens);

            foreach (var decoder in m_Decoders)
            {
                (intermediateInput, intermediateOutput) = (intermediateOutput, intermediateInput);
                intermediateOutput.Clear();
                decoder.Decode(intermediateInput, intermediateOutput.AsOutput());
            }

            output.AddRange(intermediateOutput);
        }
    }
}
