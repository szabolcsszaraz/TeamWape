using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;

namespace Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch
{
    class ConversationSearcher
    {
        const string k_CodeFence = "```";

        readonly AssistantUIContext m_Context;

        readonly AssistantSearchMessageConverter m_MessageConverter;

        internal readonly List<ConversationSearchResult> SearchResults = new();
        string m_SearchString;

        public string SearchString
        {
            get => m_SearchString;
            set
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    value = string.Empty;
                }

                m_SearchString = value;
            }
        }

        internal int TotalResultCount => SearchResults.Sum(r => r.MatchCount);

        internal ConversationSearcher(AssistantSearchMessageConverter messageConverter, AssistantUIContext context)
        {
            m_MessageConverter = messageConverter;
            m_Context = context;
        }

        internal bool SearchActiveConversation()
        {
            SearchResults.Clear();

            var conversation = m_Context.Blackboard.ActiveConversation;

            if (conversation == null || string.IsNullOrEmpty(SearchString))
            {
                return false;
            }

            // A reverted message is not in the displayed list, so it has no ordinal of its own there and
            // every message after it would be named one item too far along.
            var displayedIndex = 0;

            foreach (var msg in conversation.Messages)
            {
                if (msg.RevertedTimeStamp != 0)
                    continue;

                var messageIndex = displayedIndex++;

                if (msg.Role != MessageModelRole.User && msg.Role != MessageModelRole.Assistant)
                    continue;

                // Text the elements draw around the message rather than out of it, which the converter
                // otherwise guesses at from the code fences of whatever block it is handed. Registered,
                // it is the message's own and is searched once, below.
                var additionalText = m_MessageConverter.GetAdditionalMessageText(msg.Id);
                var codeBlockIndex = ConversationSearchResult.NoBlock;

                for (var blockIndex = 0; msg.Blocks != null && blockIndex < msg.Blocks.Count; blockIndex++)
                {
                    var content = GetSearchableContent(msg.Blocks[blockIndex]);

                    if (string.IsNullOrEmpty(content))
                        continue;

                    if (codeBlockIndex == ConversationSearchResult.NoBlock && content.Contains(k_CodeFence, StringComparison.Ordinal))
                        codeBlockIndex = blockIndex;

                    // Each block renders in its own element, so a match has to fit inside one block: no
                    // label can ever show a hit that spans a block boundary.
                    var renderedBlockContent = additionalText == null
                        ? m_MessageConverter.GetRenderedMessage(content)
                        : m_MessageConverter.GetRenderedText(content);

                    if (renderedBlockContent.Contains(SearchString, StringComparison.OrdinalIgnoreCase))
                    {
                        SearchResults.Add(new ConversationSearchResult(
                            msg.Id,
                            renderedBlockContent,
                            SearchString,
                            messageIndex,
                            blockIndex));
                    }
                }

                AddAdditionalTextResult(msg.Id, additionalText, messageIndex, codeBlockIndex);
            }

            return true;
        }

        /// <summary>
        /// Reports the registered text as one result of its own, after the blocks of the message it
        /// belongs to. The registry is per message while the loop is per block, so re-reading it inside
        /// the loop would count the same text once per block; anchored to the block whose code the text
        /// is drawn above, a jump to it still reaches the element that shows it.
        /// </summary>
        void AddAdditionalTextResult(AssistantMessageId messageId, string additionalText, int messageIndex, int blockIndex)
        {
            if (string.IsNullOrEmpty(additionalText))
                return;

            var renderedText = m_MessageConverter.GetRenderedText(additionalText);

            if (!renderedText.Contains(SearchString, StringComparison.OrdinalIgnoreCase))
                return;

            SearchResults.Add(new ConversationSearchResult(
                messageId,
                renderedText,
                SearchString,
                messageIndex,
                blockIndex));
        }

        /// <summary>The text of the block the reader is walked along, or null for a block that holds no
        /// position of theirs to be walked to.</summary>
        static string GetSearchableContent(IMessageBlockModel block)
        {
            switch (block)
            {
                case AnswerBlockModel responseBlock:
                    return responseBlock.Content;
                case PromptBlockModel promptBlock:
                    return promptBlock.Content;
                case ThoughtBlockModel thoughtBlock:
                    return thoughtBlock.Content;
                case FunctionCallBlockModel functionCallBlock:
                    functionCallBlock.Call.GetCodeEditParameters(out _, out var code, out var replacedCode);

                    // Given something to draw the saved code against, the element renders the two
                    // interleaved: the replaced lines are text the searcher never read, and the saved
                    // ones are no longer where an offset into them says. A result here would be a
                    // position of the reader's that no occurrence on screen can be found for, which
                    // buys them a step of their walk that marks nothing and moves nowhere.
                    return string.IsNullOrEmpty(replacedCode) ? code : null;
                default:
                    return null;
            }
        }

        public void Clear()
        {
            SearchResults.Clear();
        }
    }
}
