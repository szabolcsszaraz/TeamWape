using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Unity.InferenceEngine.Tokenization.Decoders
{
    /// <summary>
    /// Converts tokens looking like "&lt;0x61>" to character, and attempts to
    /// concatenate them into a string.
    /// If the tokens cannot be decoded, '�' is used instead for each inconvertible byte token.
    /// </summary>
    public class ByteFallbackDecoder : IDecoder
    {
        static unsafe void ConvertByteToToken(List<byte> previousByteTokens, List<string> returnTokens)
        {
            try
            {
                Span<byte> span = stackalloc byte[previousByteTokens.Count];
                for (var i = 0; i < previousByteTokens.Count; i++)
                    span[i] = previousByteTokens[i];

                var str = System.Text.Encoding.UTF8.GetString(span);
                if (str.Contains("\ufffd"))
                {
                    for (var i = 0; i < previousByteTokens.Count; i++)
                        returnTokens.Add("\ufffd");
                }
                else returnTokens.Add(str);
            }
            catch (DecoderFallbackException)
            {
                for (var i = 0; i < previousByteTokens.Count; i++)
                {
                    returnTokens.Add("�");
                }
            }
        }

        readonly Pool<List<byte>> m_ByteListPool = new(() => new(), list => list.Clear());
        readonly Pool<List<string>> m_ListOfStringPool = new(() => new(), list => list.Clear());

        /// <inheritdoc />
        public void Decode(IReadOnlyList<string> tokens, Output<string> output)
        {
            if (tokens == null)
                throw new ArgumentNullException(nameof(tokens));

            using var byteTokenHandle = m_ByteListPool.Get(out var previousByteTokens);

            for (int i = 0, _ = tokens.Count; i < _; i++)
            {
                var token = tokens[i];
                if (token.Length == 6 && token.StartsWith("<0x") && token.EndsWith(">"))
                {
                    // Convert the hex string to a byte. If it fails, clear the previous byte tokens
                    // and add a '�' character.
                    if (byte.TryParse(
                        token.AsSpan(3, 2), NumberStyles.HexNumber, null, out var bytes))
                        previousByteTokens.Add(bytes);
                    else
                        output.Add("\ufffd");
                }
                else
                {
                    if (previousByteTokens.Count > 0)
                    {
                        using (m_ListOfStringPool.Get(out var converted))
                        {
                            ConvertByteToToken(previousByteTokens, converted);
                            output.AddRange(converted);
                        }
                        previousByteTokens.Clear();
                    }

                    output.Add(token);
                }
            }

            if (previousByteTokens is {Count: > 0})
            {
                using var _ = m_ListOfStringPool.Get(out var converted);
                ConvertByteToToken(previousByteTokens, converted);
                output.AddRange(converted);
            }
        }
    }
}
