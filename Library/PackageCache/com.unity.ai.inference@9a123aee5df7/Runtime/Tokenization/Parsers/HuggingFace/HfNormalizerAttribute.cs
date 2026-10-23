using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Identifies the parser for an implementation of <see cref="Unity.InferenceEngine.Tokenization.Normalizers.INormalizer"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HfNormalizerAttribute : HfAttribute
    {
        /// <summary>
        /// The type of decoder.
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// Initializes a new instance of the <see cref="HfNormalizerAttribute"/> type.
        /// </summary>
        /// <param name="type">
        /// The type name of the normalizer.
        /// It corresponds to the name in the Hugging Face JSON configuration.
        /// </param>
        public HfNormalizerAttribute([NotNull] string type) =>
            Type = type ?? throw new ArgumentNullException(nameof(type));
    }
}
