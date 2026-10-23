using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Splits the input based on a regular expression.
    /// </summary>
    public class RegexSplitPreTokenizer : IPreTokenizer
    {
        readonly Pool<List<(Range offsets, bool isMatch)>> m_ListOfRangesPool =
            new(() => new(), list => list.Clear());

        Regex m_Regex;
        ISplitDelimiterBehavior m_Behavior;
        bool m_Invert;

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexSplitPreTokenizer"/> type.
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
        public RegexSplitPreTokenizer([NotNull] string pattern, SplitDelimiterBehavior behavior,
            bool invert = false)
        {
            if (pattern is null)
                throw new ArgumentNullException(nameof(pattern));

            var regex = new Regex(pattern);

            var behaviorImpl = behavior.GetImplementation();

            Init(regex, behaviorImpl, invert);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="RegexSplitPreTokenizer"/> type.
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
        public RegexSplitPreTokenizer([NotNull] Regex pattern, SplitDelimiterBehavior behavior,
            bool invert = false)
        {
            if (pattern == null)
                throw new ArgumentNullException(nameof(pattern));

            var behaviorImpl = behavior.GetImplementation();

            Init(pattern, behaviorImpl, invert);
        }

        void Init(Regex pattern, ISplitDelimiterBehavior behavior, bool invert)
        {
            m_Regex = pattern;
            m_Behavior = behavior;
            m_Invert = invert;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if(input.IsNull)
                throw new ArgumentNullException(nameof(input));

            using var _ = m_ListOfRangesPool.Get(out var splits);

            var copy = input.ToString();
            var matches = m_Regex.Matches(copy);

            var expectedOffset = 0;
            for (var i = 0; i < matches.Count; i++)
            {
                var g = matches[i].Groups[0];
                var offsets = new Range(g.Index, g.Index + g.Length);

                var (offset, length) = offsets.GetOffsetAndLength(input.Length);
                if (offset > expectedOffset)
                    splits.Add((expectedOffset .. offset, m_Invert));

                splits.Add((offsets, !m_Invert));
                expectedOffset = offset + length;
            }

            if (expectedOffset < input.Length)
                splits.Add((expectedOffset .. input.Length, m_Invert));

            m_Behavior.Apply(input, splits, output);
        }
    }
}
