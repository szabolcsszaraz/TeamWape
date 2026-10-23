namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Returns a copy of the input converted to lowercase using the casing rules of the
    /// invariant culture.
    /// </summary>
    public class LowercaseNormalizer : INormalizer
    {
        /// <inheritdoc />
        public SubString Normalize(SubString input)
        {
            return string.Create(input.Length, input, (span, s) =>
            {
                for (var i = 0; i < s.Length; i++)
                {
                    span[i] = char.ToLowerInvariant(s[i]);
                }
            });
        }
    }
}
