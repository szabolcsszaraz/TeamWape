using Unity.InferenceEngine.Tokenization.Mappers;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Pre-cuts the input <see cref="string" /> into smaller parts.
    /// Those parts will be passed to the <see cref="IMapper" /> for tokenization.
    /// </summary>
    public interface IPreTokenizer
    {
        /// <summary>
        /// Pre-cuts the <paramref name="input" /> into smaller parts.
        /// </summary>
        /// <param name="input">
        /// The source to pre-cut.
        /// </param>
        /// <param name="output">
        /// Target collection of generated pre-tokenized strings.
        /// </param>
        void PreTokenize(SubString input, Output<SubString> output);
    }
}
