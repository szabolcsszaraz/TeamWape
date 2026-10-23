using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that splits text based on a specified character delimiter.
    /// </summary>
    public class CharSplitPreTokenizer : IPreTokenizer
    {
        readonly Pool<List<(Range offsets, bool isContent)>> m_ListOfRangesPool =
            new(() => new(), list => list.Clear());

        readonly char m_Delimiter;
        readonly ISplitDelimiterBehavior m_Behavior;
        readonly bool m_Invert;

        /// <summary>
        /// Initializes a new instance of the <see cref="CharSplitPreTokenizer"/> class.
        /// </summary>
        /// <param name="delimiter">The character to use as a delimiter when splitting text.</param>
        /// <param name="behavior">How the pre-tokenizer handles the matching substrings.</param>
        /// <param name="invert">Inverts the pattern matching.</param>
        public CharSplitPreTokenizer(char delimiter, SplitDelimiterBehavior behavior = SplitDelimiterBehavior.Removed, bool invert = false)
        {
            m_Delimiter = delimiter;
            m_Behavior = behavior.GetImplementation();
            m_Invert = invert;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if(input.IsNull)
                throw new ArgumentNullException(nameof(input));

            if (input.Length == 0)
                return;

            using var _ = m_ListOfRangesPool.Get(out var splits);

            var (from, to) = (0, 0);
            while (to < input.Length)
            {
                if (input[to] == m_Delimiter)
                {
                    if (from < to)
                        splits.Add((from..to, m_Invert));

                    splits.Add((to..(to + 1), !m_Invert));
                    from = to + 1;
                }
                to++;
            }

            if (from < to)
                splits.Add((from..to, m_Invert));

            m_Behavior.Apply(input, splits, output);
        }
    }
}
