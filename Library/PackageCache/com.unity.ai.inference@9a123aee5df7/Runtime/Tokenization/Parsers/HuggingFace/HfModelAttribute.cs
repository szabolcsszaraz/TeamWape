using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Identifies the parser for an implementation of <see cref="Unity.InferenceEngine.Tokenization.Mappers.IMapper"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HfModelAttribute : HfAttribute
    {
        /// <summary>
        /// The type of decoder.
        /// </summary>
        public readonly string Type;

        /// <summary>
        /// Initializes a new instance of the <see cref="HfModelAttribute"/> type.
        /// </summary>
        /// <param name="type">
        /// The type name of the model (mapper).
        /// It corresponds to the name in the Hugging Face JSON configuration.
        /// </param>
        public HfModelAttribute([NotNull] string type) =>
            Type = type ?? throw new ArgumentNullException(nameof(type));
    }
}
