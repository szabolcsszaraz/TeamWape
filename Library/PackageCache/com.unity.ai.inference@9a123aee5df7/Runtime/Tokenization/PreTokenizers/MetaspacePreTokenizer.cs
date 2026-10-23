using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that replaces spaces with a special character (metaspace) and optionally splits
    /// the input text at these metaspace boundaries. This is commonly used in SentencePiece-based tokenizers.
    /// </summary>
    /// <remarks>
    /// The metaspace character (default: U+2581 '▁') is used to preserve information about whitespace
    /// in the original text while treating it as a regular character during tokenization. This allows
    /// the tokenizer to distinguish between "hello world" and "helloworld".
    /// </remarks>
    public class MetaspacePreTokenizer : IPreTokenizer
    {
        readonly string m_Replacement;
        readonly PrependScheme m_PrependScheme;
        readonly bool m_Split;

        readonly Pool<List<(Range range, bool isMatch)>> m_ListOfMatchPool =
            new(() => new(), list => list.Clear());

        /// <summary>
        /// Initializes a new instance of the <see cref="MetaspacePreTokenizer"/> class.
        /// </summary>
        /// <param name="replacement">
        /// The character to use as a replacement for spaces. Default is U+2581 ('▁'),
        /// the lower one eighth block Unicode character commonly used in SentencePiece.
        /// </param>
        /// <param name="prependScheme">
        /// The scheme for prepending the replacement character to the input.
        /// Default is <see cref="PrependScheme.Always"/>.
        /// </param>
        /// <param name="split">
        /// If <c>true</c>, splits the input text at metaspace character boundaries.
        /// If <c>false</c>, returns the entire processed text as a single token. Default is <c>true</c>.
        /// </param>
        public MetaspacePreTokenizer(char replacement = '\u2581',
            PrependScheme prependScheme = PrependScheme.Always, bool split = true)
        {
            m_Replacement = replacement.ToString();
            m_PrependScheme = prependScheme;
            m_Split = split;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.Length == 0)
                return;

            var replacedBuilder = new StringBuilder(input).Replace(" ", m_Replacement);

            switch (m_PrependScheme)
            {
                case PrependScheme.Always:
                    if(!input.StartsWith(m_Replacement) && !input.StartsWith(" "))
                        replacedBuilder.Insert(0, m_Replacement);
                    break;
                // case PrependScheme.First:
                //     // TODO: support offsets
                //     break;
            }

            var replaced = replacedBuilder.ToString();
            if (m_Split)
            {
                using var _ = m_ListOfMatchPool.Get(out var matches);
                Split(replaced, matches);
                SplitDelimiterBehavior.MergedWithNext.GetImplementation().Apply(replaced, matches, output);
            }
            else
            {
                output.Add(replaced);
            }
        }

        void Split(SubString source, List<(Range range, bool isMatch)> output)
        {
            var from = 0;
            var to = source.IndexOf(m_Replacement, from, StringComparison.InvariantCulture);
            while (to >= 0)
            {
                if (from < to)
                    output.Add((from..to, false));

                output.Add((to..(to + 1), true));
                from = to + 1;
                to = source.IndexOf(m_Replacement, from, StringComparison.InvariantCulture);
            }

            if (from < source.Length)
                output.Add((from.., false));
        }
    }
}
