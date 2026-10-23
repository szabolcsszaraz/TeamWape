using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// A pre-tokenizer that splits text on punctuation characters.
    /// </summary>
    /// <remarks>
    /// This pre-tokenizer identifies punctuation characters (both ASCII and Unicode)
    /// and splits the input text accordingly. The behavior of how delimiters are handled
    /// (isolated, removed, merged, or contiguous) is determined by the specified
    /// <see cref="SplitDelimiterBehavior"/>.
    /// </remarks>
    public class PunctuationPreTokenizer : IPreTokenizer
    {
        static bool IsPunctuation(char x) => IsAsciiPunctuation(x) || char.IsPunctuation(x);

        static bool IsAsciiPunctuation(char x)
        {
            return (x >= 33 && x <= 47) ||   // ! " # $ % & ' ( ) * + , - . /
                (x >= 58 && x <= 64) ||   // : ; < = > ? @
                (x >= 91 && x <= 96) ||   // [ \ ] ^ _ `
                (x >= 123 && x <= 126);   // { | } ~
        }

        readonly Pool<List<(Range range, bool isMatch)>> m_ListOfMatchPool =
            new(() => new(), list => list.Clear());

        readonly ISplitDelimiterBehavior m_Behavior;

        /// <summary>
        /// Initializes a new instance of the <see cref="PunctuationPreTokenizer"/> class
        /// with the specified delimiter behavior.
        /// </summary>
        /// <param name="behavior">
        /// The behavior that determines how punctuation delimiters are handled during splitting.
        /// Default is <see cref="SplitDelimiterBehavior.Isolated"/>.
        /// </param>
        public PunctuationPreTokenizer(
            SplitDelimiterBehavior behavior = SplitDelimiterBehavior.Isolated) : this(
            behavior.GetImplementation())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PunctuationPreTokenizer"/> class
        /// with the specified delimiter behavior implementation.
        /// </summary>
        /// <param name="behavior">The behavior implementation for handling split delimiters.</param>
        internal PunctuationPreTokenizer(ISplitDelimiterBehavior behavior)
        {
            m_Behavior = behavior;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.IsEmpty)
                return;

            using var _ = m_ListOfMatchPool.Get(out var matches);

            var (from, to) = (0, 0);

            while (to < input.Length)
            {
                 var c = input[to];
                 if (IsPunctuation(c))
                 {
                     if (from != to)
                     {
                         matches.Add((from..to, false));
                     }

                     matches.Add((to..(to+1), true));
                     from = to + 1;
                 }

                 to++;
            }

            if (from != to)
            {
                matches.Add((from..to, false));
            }

            m_Behavior.Apply(input, matches, output);
        }
    }
}
