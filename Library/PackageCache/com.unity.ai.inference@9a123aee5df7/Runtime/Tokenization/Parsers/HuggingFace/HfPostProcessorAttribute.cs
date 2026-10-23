using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Identifies the parser for an implementation of <see cref="Unity.InferenceEngine.Tokenization.PostProcessors.IPostProcessor"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HfPostProcessorAttribute : HfAttribute
    {
        /// <summary>
        /// The type of decoder.
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// Initializes a new instance of the <see cref="HfPostProcessorAttribute"/> type.
        /// </summary>
        /// <param name="type">
        /// The type name of the post processor.
        /// It corresponds to the name in the Hugging Face JSON configuration.
        /// </param>
        public HfPostProcessorAttribute([NotNull] string type) =>
            Type = type ?? throw new ArgumentNullException(nameof(type));
    }
}
