using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Data;

namespace Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch
{
    class ConversationSearchResult
    {
        /// <summary>What <see cref="BlockIndex"/> holds for a match no single block carries, which the
        /// view answers about the message as a whole.</summary>
        public const int NoBlock = -1;

        public readonly AssistantMessageId MessageId;
        public readonly int MatchCount;
        public readonly int MessageIndex;

        /// <summary>Index into the message's block list of the block the matches are in.</summary>
        public readonly int BlockIndex;

        public ConversationSearchResult(
            AssistantMessageId messageId,
            string renderedText,
            string searchString,
            int messageIndex,
            int blockIndex)
        {
            MessageId = messageId;
            MessageIndex = messageIndex;
            BlockIndex = blockIndex;

            MatchCount = Matches(renderedText, searchString).Count();
        }

        /// <summary>
        /// Whether both results name the same match of the same conversation. A search re-runs whenever
        /// the text it can reach changes — which realizing a block does — and rebuilds its result list
        /// from scratch, so a result a caller is holding is never the instance the fresh list carries.
        /// </summary>
        internal bool IsSameMatch(ConversationSearchResult other)
            => other != null
                && MessageId == other.MessageId
                && MessageIndex == other.MessageIndex
                && BlockIndex == other.BlockIndex;

        internal IEnumerable<Tuple<int, int>> Matches(string renderedMessage, string searchString)
        {
            var startIndex = 0;
            while (true)
            {
                var foundIndex =
                    renderedMessage.IndexOf(searchString, startIndex, StringComparison.OrdinalIgnoreCase);

                if (foundIndex == -1)
                    break;

                yield return new Tuple<int, int>(foundIndex, searchString.Length);

                startIndex = foundIndex + 1;
            }
        }
    }
}
