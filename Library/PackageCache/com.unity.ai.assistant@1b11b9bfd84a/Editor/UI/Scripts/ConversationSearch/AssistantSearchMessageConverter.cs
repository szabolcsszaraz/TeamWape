using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch
{
    /// <summary>
    /// Handles conversion of messages for search results to remove tags, etc. that should not be searchable.
    /// </summary>
    class AssistantSearchMessageConverter
    {
        static readonly Regex k_StripRegexTickedBlocks = new(
            @"```([^\n]*)\n([\s\S]*?)```",
            RegexOptions.Compiled | RegexOptions.Multiline);

        static readonly Regex k_StripRegexBackTicks = new(
            "`+",
            RegexOptions.Compiled | RegexOptions.Multiline);

        static readonly Regex k_StripRegexBlocks = new(
            ":::.*?:::",
            RegexOptions.Compiled | RegexOptions.Singleline);

        // A link, or an image, and the words it is drawn as: the target between the parentheses reaches
        // the label as the attribute of a tag, which is not text any reader can be shown or any
        // highlight can be put on. Recognised by what a target begins with, so that a pair of brackets
        // beside a call in code is left where it is.
        static readonly Regex k_StripRegexLinkTarget = new(
            @"!?\[([^\]\n]*)\]\((?:https?://|mailto:|[#./])[^)\s]*(?:\s+""[^""]*"")?\)",
            RegexOptions.Compiled);

        static readonly Dictionary<AssistantMessageId, List<Tuple<VisualElement, string>>> k_AdditionalTextForMessages =
            new();

        internal string GetRenderedMessage(string message, AssistantMessageId? messageId = null)
        {
            // Append additional text registered for this message,
            // the position doesn't matter, we just need it to be part of the search string:
            var additionalText = messageId.HasValue ? GetAdditionalMessageText(messageId.Value) : null;

            if (additionalText != null)
            {
                message += additionalText;
            }

            return StripUnsearchableMarkup(message);
        }

        /// <summary>
        /// Renders text that is not a message's own content — a piece of chrome an element draws around
        /// it — so that it can be searched beside the blocks without the code fences of a block adding
        /// anything of their own.
        /// </summary>
        internal string GetRenderedText(string text) => StripUnsearchableMarkup(text);

        /// <summary>
        /// Everything registered with <see cref="RegisterAdditionalMessageText"/> for the message, or
        /// null when nothing is: the UI shows this text although no block carries it, so a search that
        /// ignores it misses words the reader can see.
        /// </summary>
        internal string GetAdditionalMessageText(AssistantMessageId messageId)
            => k_AdditionalTextForMessages.TryGetValue(messageId, out var text)
                ? string.Join("", text.Select(t => t.Item2).Where(t => !string.IsNullOrEmpty(t)))
                : null;

        static string StripUnsearchableMarkup(string message)
        {
            var lastLength = message.Length;
            var noRichText = message;

            // Repeat until no more changes are made, to ensure tags within tags are also removed:
            do
            {
                lastLength = noRichText.Length;

                // Remove rich text tags
                noRichText = MarkupUtil.k_RichTextTagRegex.Replace(noRichText, string.Empty);

                // Replace triple backtick blocks with their inner lines (excluding the first line)
                noRichText = k_StripRegexTickedBlocks.Replace(noRichText, m =>
                {
                    var inner = m.Groups[2].Value;
                    return inner;
                });

                // Remove single backticks
                noRichText = k_StripRegexBackTicks.Replace(noRichText, string.Empty);

                // Remove ::: blocks
                noRichText = k_StripRegexBlocks.Replace(noRichText, string.Empty);

                // Leave a link as the words it is drawn as
                noRichText = k_StripRegexLinkTarget.Replace(noRichText, "$1");
            } while (noRichText.Length != lastLength);

            return noRichText;
        }

        internal void RegisterAdditionalMessageText(
            AssistantMessageId messageId,
            VisualElement element,
            string additionalText)
        {
            if (!k_AdditionalTextForMessages.TryGetValue(messageId, out var additionalTextList))
            {
                additionalTextList = new();
                k_AdditionalTextForMessages[messageId] = additionalTextList;
            }

            for (var i = 0; i < additionalTextList.Count; i++)
                if (additionalTextList[i].Item1 == element)
                    additionalTextList.RemoveAt(i--);

            additionalTextList.Add(new Tuple<VisualElement, string>(element, additionalText));
        }

        internal void UnregisterAdditionalMessageText(
            AssistantMessageId messageId,
            VisualElement element)
        {
            if (!k_AdditionalTextForMessages.TryGetValue(messageId, out var additionalTextList))
                return;

            for (var i = 0; i < additionalTextList.Count; i++)
                if (additionalTextList[i].Item1 == element)
                    additionalTextList.RemoveAt(i--);

            if (additionalTextList.Count == 0)
                k_AdditionalTextForMessages.Remove(messageId);
        }

        public void Clear()
        {
            k_AdditionalTextForMessages.Clear();
        }
    }
}
