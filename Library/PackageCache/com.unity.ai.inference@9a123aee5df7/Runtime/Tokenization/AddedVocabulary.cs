using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization
{

    partial class AddedVocabulary
    {

        static bool IsUnicodeWordChar(char c)
        {
            var category = char.GetUnicodeCategory(c);
            return
                category == UnicodeCategory.UppercaseLetter ||
                category == UnicodeCategory.LowercaseLetter ||
                category == UnicodeCategory.TitlecaseLetter ||
                category == UnicodeCategory.ModifierLetter ||
                category == UnicodeCategory.OtherLetter ||
                category == UnicodeCategory.DecimalDigitNumber ||
                category == UnicodeCategory.NonSpacingMark ||
                category == UnicodeCategory.SpacingCombiningMark ||
                category == UnicodeCategory.ConnectorPunctuation;
        }

        readonly IDictionary<SubString, TokenConfiguration> m_ByValue;
        readonly IDictionary<int, TokenConfiguration> m_ById;

        //readonly TokenConfiguration[] m_Classics;
        //readonly TokenConfiguration[] m_Specials;

        readonly SearchTree m_Search;
        readonly SearchTree m_NormalizedSearch;

        readonly Pool<List<SearchTree.Match>> m_ListOfMatchPool =
            new(() => new(), list => list.Clear());

        bool m_EncodeSpecialTokens;

        public AddedVocabulary([CanBeNull] IEnumerable<TokenConfiguration> configurations,
            INormalizer normalizer, bool encodeSpecialTokens)
        {
            var configs = configurations?.ToArray() ?? Array.Empty<TokenConfiguration>();

            m_Search = new(configs.Where(t => !t.Normalized).Select(t => (t.Value, t.Id)));

            m_NormalizedSearch =
                new(configs.Where(t => t.Normalized)
                    .Select(t => (normalizer.Normalize(t.Value).ToString(), t.Id)));

            m_ByValue = configs.ToDictionary(t => new SubString(t.Value));
            m_ById = configs.ToDictionary(t => t.Id);

            //m_Classics = configs.Where(tc => !tc.Special).ToArray();
            //m_Specials = configs.Where(tc => tc.Special).ToArray();

            m_EncodeSpecialTokens = encodeSpecialTokens;
        }

        public bool TryGetConfiguration(int id, out TokenConfiguration configuration) =>
            m_ById.TryGetValue(id, out configuration);

        public bool TryGetConfiguration(SubString value, out TokenConfiguration configuration) =>
            m_ByValue.TryGetValue(value, out configuration);

        public void Split(SubString source, bool normalized,
            Output<(int? id, Range offsets)> output) =>
            Split(source, normalized ? m_NormalizedSearch : m_Search, output);

        void Split(SubString source, SearchTree search,
            Output<(int? id, Range offsets)> output)
        {
            if (string.IsNullOrEmpty(source))
                throw new ArgumentNullException(nameof(source), "Source cannot be null or empty");

            using var resultHandle = m_ListOfMatchPool.Get(out var matches);
            var count = search.Search(source.AsSpan(), matches);
            if (count == 0)
            {
                output.Add((null, Range.All));
                return;
            }

            var startOffset = 0;

            foreach (var (id, matchOffsets) in matches)
            {
                var token = m_ById[id];

                if (m_EncodeSpecialTokens && token.Special)
                    continue;

                var (matchStart, matchLength) = matchOffsets.GetOffsetAndLength(source.Length);
                var matchEnd = matchStart + matchLength;

                if (token.WholeWord)
                {
                    var startSpace = matchStart == 0 || !IsUnicodeWordChar(source[matchStart - 1]);
                    var endSpace = matchEnd == source.Length
                        || !IsUnicodeWordChar(source[matchEnd]);

                    if (!startSpace || !endSpace)
                        continue;
                }

                if (token.Strip.Match(Direction.Left))
                {
                    while (matchStart - 1 > 0 && char.IsWhiteSpace(source[matchStart - 1]))
                        matchStart--;

                    matchStart = Math.Max(startOffset, matchStart);
                }

                if (token.Strip.Match(Direction.Right))
                {
                    while (matchEnd < source.Length && char.IsWhiteSpace(source[matchEnd]))
                        matchEnd++;
                }

                if (matchStart > startOffset)
                    output.Add((null, new(startOffset, matchStart)));

                output.Add((token.Id, new(matchStart, matchEnd)));
                startOffset = matchEnd;
            }

            if (startOffset < source.Length)
                output.Add((null, startOffset..));
        }

        public bool IsSpecial(string token) =>
            m_ByValue.TryGetValue(token, out var addedToken) && addedToken.Special;
    }
}
