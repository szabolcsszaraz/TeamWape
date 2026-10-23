namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Applies transformations to the input string before pre-tokenization.
    /// </summary>
    public interface INormalizer
    {
        /// <summary>
        /// Applies transformations to the input string before pre-tokenization.
        /// </summary>
        /// <param name="input">
        /// The string to transform.
        /// </param>
        /// <returns>
        /// The resulting string.
        /// </returns>
        SubString Normalize(SubString input);
    }
}
