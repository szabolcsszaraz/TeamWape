using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Convert <see cref="SubString" /> input to a read only sequence of <see cref="byte" />s.
    /// </summary>
    class SubStringToByteConverter : IOneToManyConverter<SubString, byte>
    {
        /// <summary>
        /// Encodes the input <see cref="SubString" /> into a sequence of <see cref="byte" />s.
        /// </summary>
        readonly SEncoding m_Encoding;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubStringToByteConverter" /> type.
        /// </summary>
        public SubStringToByteConverter()
        {
            m_Encoding = SEncoding.UTF8;
        }

        /// <summary>
        /// Gets a singleton instance of the <see cref="SubStringToByteConverter" /> type.
        /// </summary>
        public static SubStringToByteConverter Instance { get; } = new();

        public void Convert(SubString input, Output<byte> output)
        {
            var (source, offsets) = input;
            var (offset, length) = offsets.GetOffsetAndLength(source.Length);
            var bytes = m_Encoding.GetBytes(source, offset, length);
            output.AddRange(bytes);
        }
    }
}
