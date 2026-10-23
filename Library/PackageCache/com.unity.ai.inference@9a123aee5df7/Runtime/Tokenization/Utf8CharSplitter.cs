namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Separates each UTF-8 character from a <see cref="SubString" /> input.
    /// </summary>
    class Utf8CharSplitter : IOneToManyConverter<SubString, SubString>
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="Utf8CharSplitter" /> type.
        /// </summary>
        public static Utf8CharSplitter Instance { get; } = new();

        public void Convert(SubString input, Output<SubString> output)
        {
            var charOffset = 0;
            while (charOffset < input.Length)
            {
                var charTo = charOffset;
                var c = input[charTo];
                if (char.IsHighSurrogate(c) && charTo + 1 < input.Length
                    && char.IsLowSurrogate(input[charTo + 1]))
                    charTo++;
                charTo++;
                output.Add(input[charOffset .. charTo]);
                charOffset = charTo;
            }
        }
    }
}
