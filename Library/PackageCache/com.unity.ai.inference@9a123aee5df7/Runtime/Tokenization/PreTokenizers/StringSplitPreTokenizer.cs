using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Splits the input based on a string pattern.
    /// </summary>
    public class StringSplitPreTokenizer : IPreTokenizer
    {
        readonly Pool<List<(Range offsets, bool isContent)>> m_ListOfRangesPool =
            new(() => new(), list => list.Clear());

        string m_Pattern;
        ISplitDelimiterBehavior m_Behavior;
        readonly bool m_Invert;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringSplitPreTokenizer"/> type.
        /// </summary>
        /// <param name="pattern">
        /// The pattern on which the input string is split.
        /// </param>
        /// <param name="behavior">
        /// Indicates how to handle splits and patterns.
        /// <see cref="SplitDelimiterBehavior"/>
        /// </param>
        /// <param name="invert">
        /// Whether of not to invert the pattern.
        /// Not yet implemented.
        /// </param>
        public StringSplitPreTokenizer([NotNull] string pattern, SplitDelimiterBehavior behavior,
            bool invert = false)
        {
            if (string.IsNullOrEmpty(pattern))
                throw new ArgumentNullException(nameof(pattern));

            m_Pattern = pattern;
            m_Behavior = behavior.GetImplementation();
            m_Invert = invert;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if(input.IsNull)
                throw new ArgumentNullException(nameof(input));

            using var _ = m_ListOfRangesPool.Get(out var splits);

            var expectedOffset = 0;
            var matchIndex = input.IndexOf(m_Pattern);
            while (matchIndex >= 0)
            {
                var offsets = new Range(matchIndex, matchIndex + m_Pattern.Length);

                var (offset, length) = offsets.GetOffsetAndLength(input.Length);
                if (offset > expectedOffset)
                    splits.Add((expectedOffset .. offset, m_Invert));

                splits.Add((offsets, !m_Invert));
                expectedOffset = offset + length;
                matchIndex = input.IndexOf(m_Pattern, matchIndex + m_Pattern.Length);
            }

            if (expectedOffset < input.Length)
                splits.Add((expectedOffset .. input.Length, m_Invert));

            m_Behavior.Apply(input, splits, output);
        }
    }
}
