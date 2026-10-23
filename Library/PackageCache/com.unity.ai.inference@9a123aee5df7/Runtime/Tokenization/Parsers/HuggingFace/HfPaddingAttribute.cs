using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace
{
    /// <summary>
    /// Identifies the parser for an implementation of <see cref="Unity.InferenceEngine.Tokenization.Padding.IPadding"/>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class HfPaddingAttribute : HfAttribute
    {
        /// <summary>
        /// The strategy of padding.
        /// </summary>
        public readonly string Strategy;

        /// <summary>
        /// Initializes a new instance of the <see cref="HfPaddingAttribute"/> type.
        /// </summary>
        /// <param name="strategy">
        /// The strategy of the padding.
        /// It corresponds to the name in the Hugging Face JSON configuration.
        /// </param>
        public HfPaddingAttribute([NotNull] string strategy) =>
            Strategy = strategy ?? throw new ArgumentNullException(nameof(strategy));
    }
}
