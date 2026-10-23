using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// High-level non-generic interface for Hugging Face tokenizer component builders.
    /// </summary>
    public interface IComponentBuilder
    {
    }

    /// <summary>
    /// Generic Interface for Hugging Face tokenizer component builders.
    /// </summary>
    /// <typeparam name="TComponent">
    /// The type of the component for builder produces.
    /// </typeparam>
    public interface IComponentBuilder<out TComponent> : IComponentBuilder
    {
        /// <summary>
        /// Builds an instance of the specified <typeparamref name="TComponent"/>.
        /// </summary>
        /// <param name="parameters">
        /// JSON-serialized form of the component.
        /// </param>
        /// <param name="parser">
        /// A reference to the parser instance actually building the tokenizer.
        /// In case of sequences (<see cref="Unity.InferenceEngine.Tokenization.Normalizers.SequenceNormalizer"/>,
        /// <see cref="Unity.InferenceEngine.Tokenization.PostProcessors.SequencePostProcessor"/>, …), it allows the implementation to parse
        /// subcomponents.
        /// </param>
        /// <returns>
        /// A new instance of <typeparamref name="TComponent"/>.
        /// </returns>
        public TComponent Build([NotNull] JToken parameters, HuggingFaceParser parser);
    }
}
