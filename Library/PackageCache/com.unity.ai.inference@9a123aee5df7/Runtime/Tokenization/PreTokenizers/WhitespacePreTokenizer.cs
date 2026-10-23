using System.Runtime.CompilerServices;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that splits text into word tokens and non-word, non-whitespace tokens.
    /// This implementation matches the behavior of the regular expression pattern "\w+|[^\w\s]+".
    /// </summary>
    /// <remarks>
    /// <para>
    /// The tokenizer operates in two modes:
    /// <list type="bullet">
    /// <item><description>Word mode: Captures sequences of word characters (letters, digits, and
    /// underscores)</description></item>
    /// <item><description>Symbol mode: Captures sequences of non-word, non-whitespace characters
    /// (punctuation, special characters)</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// Whitespace characters are skipped and not included in the output tokens.
    /// </para>
    /// </remarks>
    /// <example>
    /// Input: "Hello, World! Test-123"
    /// Output: ["Hello", ",", "World", "!", "Test", "-", "123"]
    /// </example>
    public class WhitespacePreTokenizer : IPreTokenizer
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            var i = 0;

            while (i < input.Length)
            {
                var c = input[i];

                // Skip whitespace
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                // Match word characters (\w+)
                if (IsWordChar(c))
                {
                    var start = i;
                    while (i < input.Length && IsWordChar(input[i]))
                        i++;
                    output.Add(input[start.. i]);
                }
                // Match non-word, non-whitespace characters ([^\w\s]+)
                else
                {
                    var start = i;
                    while (i < input.Length && !IsWordChar(input[i])
                           && !char.IsWhiteSpace(input[i]))
                        i++;
                    output.Add(input[start .. i]);
                }
            }
        }
    }
}
