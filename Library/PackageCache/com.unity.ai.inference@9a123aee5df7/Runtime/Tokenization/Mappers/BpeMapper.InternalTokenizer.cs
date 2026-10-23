using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    partial class BpeMapper
    {
        /// <summary>
        /// Default char->token converter for <see cref="BpeMapper" />.
        /// </summary>
        internal class InternalTokenizer : IOneToManyConverter<SubString, Token>
        {
            readonly Pool<List<SubString>> m_ListOfSubStringPool = new(() => new(), list => list.Clear());

            /// <summary>
            /// The helper instance responsible for turning a single <see cref="byte" /> into a
            /// token value.
            /// </summary>
            IOneToOneConverter<byte, string> m_ByteToToken;

            /// <summary>
            /// The helper instance responsible for turning a <see cref="char" /> into a
            /// <see cref="byte" /> array.
            /// </summary>
            IOneToOneConverter<SubString, IReadOnlyList<byte>> m_CharToByte;

            /// <summary>
            /// Splits the input string into a sequence of unicode characters.
            /// </summary>
            IOneToManyConverter<SubString, SubString> m_StringToUtfChars;

            /// <summary>
            /// The helper instance responsible for turning a single <see cref="char" /> into a
            /// token value, considering its position in the word.
            /// </summary>
            IOneToOneConverter<(SubString character, bool first, bool last), string> m_CharToToken;

            (int id, string token, bool fuse, bool byteFallback) m_Unknown;

            IReadOnlyDictionary<string, int> m_Vocabulary;

            public InternalTokenizer(
                IReadOnlyDictionary<string, int> vocabulary,
                BpeMapperOptions options)
            {
                Init(
                    new OneToOneCachedConverter<SubString, IReadOnlyList<byte>>(
                        new UtfCharToByteConverter()), ByteToTokenConverter.Instance,
                    Utf8CharSplitter.Instance,
                    new Utf8CharToTokenConverter(options.SubWordPrefix, options.WordSuffix),
                    vocabulary, options.UnknownToken, options.FuseUnknown!.Value,
                    options.ByteFallback!.Value);
            }

            internal InternalTokenizer(
                IOneToOneConverter<SubString, IReadOnlyList<byte>> charToByte,
                IOneToOneConverter<byte, string> byteToToken,
                IOneToManyConverter<SubString, SubString> stringToUtf8Char,
                IOneToOneConverter<(SubString, bool, bool), string> charToToken,
                IReadOnlyDictionary<string ,int> vocabulary,
                string unknownToken, bool fuseUnknown, bool byteFallback)
            {
                Init(charToByte, byteToToken, stringToUtf8Char, charToToken, vocabulary,
                    unknownToken, fuseUnknown, byteFallback);
            }

            /// <summary>
            /// Gets the sequence of token ids for each <c>char</c> of the
            /// <paramref name="input" />.
            /// </summary>
            /// <param name="input">
            /// The <c>char</c> sequence.
            /// </param>
            /// <param name="output">
            /// </param>
            /// <returns>
            /// The sequence of token ids.
            /// </returns>
            public void Convert(SubString input, Output<Token> output)
            {
                var previousIsUnk = false;

                using var _ = m_ListOfSubStringPool.Get(out var utfChars);
                m_StringToUtfChars.Convert(input, utfChars.AsOutput());

                for (var i = 0; i < utfChars.Count; i++)
                {
                    var @char = utfChars[i];

                    var repr = m_CharToToken.Convert((@char, i == 0, i == utfChars.Count - 1));

                    // token representation found
                    if (m_Vocabulary.TryGetValue(repr, out var id))
                    {
                        output.Add(new Token(id, repr));
                        previousIsUnk = false;
                    }

                    // token representation not found, but byte fallback allowed
                    else if (m_Unknown.byteFallback)
                    {
                        previousIsUnk = false;
                        var bytes = m_CharToByte.Convert(@char);
                        foreach (var b in bytes)
                            if (m_Vocabulary.TryGetValue(m_ByteToToken.Convert(b), out id))
                                output.Add(new Token(id));
                            // else should with warn?
                    }

                    // unknown
                    else if (!m_Unknown.fuse || !previousIsUnk)
                    {
                        if (m_Unknown.token is not null)
                            output.Add(new Token(m_Unknown.id, m_Unknown.token));

                        previousIsUnk = true;
                    }
                }
            }

            void Init(
                IOneToOneConverter<SubString, IReadOnlyList<byte>> charToByte,
                IOneToOneConverter<byte, string> byteToToken,
                IOneToManyConverter<SubString, SubString> stringToUtf8Chars,
                IOneToOneConverter<(SubString, bool, bool), string> charToToken,
                IReadOnlyDictionary<string, int> vocabulary,
                string unknownToken, bool fuseUnknown, bool byteFallback)
            {
                m_CharToByte = charToByte;
                m_ByteToToken = byteToToken;
                m_StringToUtfChars = stringToUtf8Chars;
                m_CharToToken = charToToken;

                m_Vocabulary = vocabulary;
                m_Unknown = (unknownToken is not null ? m_Vocabulary[unknownToken] : 0,
                    unknownToken, fuseUnknown, byteFallback);
            }
        }
    }
}
