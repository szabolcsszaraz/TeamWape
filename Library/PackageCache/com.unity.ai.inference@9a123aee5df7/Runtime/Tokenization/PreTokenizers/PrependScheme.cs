namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Used by Metaspace to determine which of the pretokenized inputs to process.
    /// </summary>
    public enum PrependScheme
    {
        /// <summary>
        /// Processes all the input strings.
        /// </summary>
        Always,

        /// <summary>
        /// Processes no input string.
        /// </summary>
        Never,

        /// <summary>
        /// Processes only the first input string.
        /// </summary>
        First
    }
}
