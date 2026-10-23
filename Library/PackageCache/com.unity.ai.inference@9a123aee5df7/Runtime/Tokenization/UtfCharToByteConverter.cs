using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization
{
    /// <inheritdoc />
    class UtfCharToByteConverter : IOneToOneConverter<SubString, IReadOnlyList<byte>>
    {
        /// <summary>
        /// Instance of <see cref="Encoding" /> used to generate the byte array representation.
        /// </summary>
        readonly SEncoding m_Encoder;

        /// <summary>
        /// Initializes a new instance of the <see cref="UtfCharToByteConverter" /> type.
        /// </summary>
        public UtfCharToByteConverter() : this(SEncoding.UTF8)
        { }

        /// <summary>
        /// Initializes a new instance of the <see cref="UtfCharToByteConverter" /> type.
        /// </summary>
        /// <param name="encoder">
        /// The encoding to use to generate the byte array representation of a character.
        /// </param>
        internal UtfCharToByteConverter(SEncoding encoder) => m_Encoder = encoder;

        /// <inheritdoc />
        public IReadOnlyList<byte> Convert(SubString character) =>
            m_Encoder
                .GetBytes(character.Source, character.Offset, character.Length);
    }
}
