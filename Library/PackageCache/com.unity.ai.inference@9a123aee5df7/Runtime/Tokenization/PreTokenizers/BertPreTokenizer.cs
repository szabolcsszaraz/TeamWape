using System;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Splits on spaces and punctuation, removing spaces, and keeping each punctuation as
    /// separated chunk.
    /// </summary>
    public class BertPreTokenizer : IPreTokenizer
    {
        static bool IsAsciiPunctuation(char c) =>
            c <= 0x7F && "!\"#$%&'()*+,-./:;<=>?@[\\]^_`{|}~".IndexOf(c) >= 0;

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.IsNull)
                throw new ArgumentNullException(nameof(input));

            var (source, offsets) = input;
            var (offset, length) = offsets.GetOffsetAndLength(source.Length);
            var limit = offset + length;
            while (offset < limit)
            {
                // consume white spaces
                while (char.IsWhiteSpace(source[offset]))
                {
                    offset++;
                    if (offset == limit)
                        return;
                }

                // c is non-space character
                var c = source[offset];

                if (char.IsPunctuation(c) || IsAsciiPunctuation(c))
                {
                    output.Add(new(source, offset .. (offset + 1)));
                    offset++;
                }

                // alphanumeric character
                else
                {
                    for (var i = offset + 1; i <= limit; i++)
                    {
                        if (i == limit)
                        {
                            output.Add(new(source, offset .. limit));
                            offset = limit;
                            break;
                        }

                        c = source[i];
                        if (i == limit || char.IsPunctuation(c) || char.IsWhiteSpace(c) || IsAsciiPunctuation(c))
                        {
                            output.Add(new(source, offset .. i));
                            offset = i;
                            break;
                        }
                    }
                }
            }
        }
    }
}
