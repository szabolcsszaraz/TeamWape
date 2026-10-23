using System;
using System.Collections.Generic;
using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Converts byte-level characters to unicode characters, then concat them into a single
    /// <see cref="string" />.
    /// </summary>
    public class ByteLevelDecoder : IDecoder
    {
        readonly SEncoding m_Encoding;
        readonly Pool<List<byte>> m_ListOfBytePool = new(() => new(), list => list.Clear());

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteLevelDecoder" /> type.
        /// </summary>
        public ByteLevelDecoder() : this(SEncoding.UTF8)
        {
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="ByteLevelDecoder" /> type.
        /// </summary>
        /// <remarks>
        ///     This constructor is exposed for testing purpose.
        /// </remarks>
        internal ByteLevelDecoder(SEncoding encoding) => m_Encoding = encoding;

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            using var byteReprHandle = m_ListOfBytePool.Get(out var byteRepr);
            using var accHandle = m_ListOfBytePool.Get(out var acc);

            for (int i = 0, _ = tokens.Count; i < _; i++)
            {
                var chunk = tokens[i];
                if(chunk is null)
                    throw new ArgumentNullException(nameof(tokens), "Cannot contain null token");

                var success = true;
                foreach (var character in chunk)
                {
                    var found = ByteLevelHelper.CharsBytes.TryGetValue(character, out var @byte);
                    if (found)
                    {
                        acc.Add(@byte);
                    }
                    else
                    {
                        success = false;
                        break;
                    }
                }

                if (success)
                    byteRepr.AddRange(acc);
                else
                    ToBytes(chunk, byteRepr);

                acc.Clear();
            }

            output.Add(FromBytes(byteRepr));
        }

        unsafe string FromBytes(IReadOnlyList<byte> bytes)
        {
            if(bytes.Count == 0)
                return string.Empty;

            var byteArray = stackalloc byte[bytes.Count];
            for (var i = 0; i < bytes.Count; i++)
                byteArray[i] = bytes[i];

            return m_Encoding.GetString(byteArray, bytes.Count);
        }

        unsafe void ToBytes(string input, ICollection<byte> output)
        {
            var byteCount = m_Encoding.GetByteCount(input);
            var bytes = stackalloc byte[byteCount];
            fixed (char* pChar = input)
            {
                m_Encoding.GetBytes(pChar, input.Length, bytes, byteCount);
            }

            for (var i = 0; i < byteCount; i++)
                output.Add(bytes[i]);
        }
    }
}
