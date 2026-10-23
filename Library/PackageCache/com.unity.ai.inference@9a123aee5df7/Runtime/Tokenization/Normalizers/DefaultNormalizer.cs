namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Does not apply any transformation.
    /// </summary>
    public class DefaultNormalizer : INormalizer
    {
        /// <inheritdoc />
        public SubString Normalize(SubString input) => input;
    }
}
