using System.Collections.Generic;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// A word-level tokenization mapper that converts between tokens and their corresponding IDs.
    /// </summary>
    public class WordLevelMapper : IMapper
    {
        Dictionary<SubString, int> m_TokenToIdMap = new();
        Dictionary<int, SubString> m_IdToTokenMap = new();
        int m_UnkId;

        /// <summary>
        /// Initializes a new instance of the <see cref="WordLevelMapper"/> class.
        /// </summary>
        /// <param name="vocab">The vocabulary dictionary mapping token strings to their
        /// corresponding IDs.</param>
        /// <param name="unkToken">The unknown token string used when a token is not found in the
        /// vocabulary.</param>
        public WordLevelMapper(Dictionary<string, int> vocab, string unkToken)
        {
            m_IdToTokenMap = vocab.ToDictionary(t => t.Value, t => new SubString(t.Key));
            m_TokenToIdMap = m_IdToTokenMap.ToDictionary(t => t.Value, t => t.Key);
            m_UnkId = m_TokenToIdMap[unkToken];
        }

        /// <inheritdoc />
        public bool TokenToId(string token, out int id) =>
            m_TokenToIdMap.TryGetValue(token, out id);

        /// <inheritdoc />
        public string IdToToken(int id) => m_IdToTokenMap[id];

        /// <inheritdoc />
        public void Tokenize(IReadOnlyList<SubString> input, Output<Token> output)
        {
            for (var i = 0; i < input.Count; i++)
            {
                var id = m_TokenToIdMap.GetValueOrDefault(input[i], m_UnkId);
                output.Add(new(id));
            }
        }
    }
}
