using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch
{
    class SearchHighlighter
    {
        internal static readonly string k_HighlightStartTag = EditorGUIUtility.isProSkin
            ? "<mark=#F4BC0233><color=#FFFFFF>"
            : "<mark=#F4BC024D><color=#000000>";

        internal const string k_HighlightEndTag = "</mark></color>";

        // The current match is orange against the other matches' yellow: an alpha step on one hue
        // does not read as "you are here" the way a hue step does.
        internal static readonly string k_HighlightMainStartTag =
            EditorGUIUtility.isProSkin
                ? "<mark=#FF8A00A6><color=#FFFFFF>"
                : "<mark=#FF8A00B3><color=#000000>";

        internal const string k_HighlightMainEndTag = "</mark></color>";

        const string k_NoparseStart = "<noparse>";
        const string k_NoParseEnd = "</noparse>";

        // What a walk reads as "the main occurrence is already behind us", which counts every occurrence
        // it is spent on as one of the others.
        const int k_MainHighlightPassed = -1;

        readonly Regex k_NoParse1Regex = new(@$"{k_NoParseEnd}\s*{k_NoparseStart}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly Regex k_NoParse2Regex = new(@$"{k_NoparseStart}\s*{k_NoParseEnd}",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        readonly Regex k_TagRegex = new(
            $@"</?(?:{string.Join("|", MarkupUtil.k_ValidRichTextTags.Select(Regex.Escape))})(?:=(?:(?!>).)*)?>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        class HighlightStringInfo
        {
            public readonly string OriginalText;
            public readonly bool EnableRichText;
            public readonly TextElement Label;

            public bool UpdatedThisFrame;

            public HighlightStringInfo(TextElement label)
            {
                Label = label;
                OriginalText = label.text;
                EnableRichText = label.enableRichText;
            }

            public override int GetHashCode() => Label.GetHashCode();
        }

        readonly HashSet<HighlightStringInfo> m_HighlightedElements = new();

        /// <summary>Every element the walk marks, against whether the searcher reads the text it
        /// draws.</summary>
        readonly Dictionary<TextElement, bool> m_HighlightableElements = new();

        readonly Action<TextElement> ScrollLabelIntoView;
        readonly Func<string, AssistantMessageId?, string> GetRenderedMessage;

        // Reusable builders and lists to avoid allocations:
        readonly StringBuilder k_CleanBuilder = new();
        readonly List<int> k_OrigIndex = new();
        readonly List<TextElement> k_DetachedElements = new();

        public SearchHighlighter(
            Action<TextElement> scrollLabelIntoView,
            Func<string, AssistantMessageId?, string> getRenderedMessage)
        {
            ScrollLabelIntoView = scrollLabelIntoView;
            GetRenderedMessage = getRenderedMessage;
        }

        /// <summary>
        /// Adds "mark" tags around all occurrences of highlightString in text, ignoring any existing rich text tags.
        /// If richTextEnabled is false, also wraps non-highlighted parts in "noparse" tags to avoid interpreting any rich text tags.
        /// </summary>
        /// <param name="highlightString">The string to find and highlight in `text`</param>
        /// <param name="mainHighlightIndex">The index of the main item to highlight, will be decremented for each found occurrence of `highlightString`</param>
        /// <param name="text">The input string to highlight</param>
        /// <param name="richTextEnabled">Whether the TextElement displaying `text` had rich text enabled. If false, the non-highlighted parts will be wrapped in "noparse" tags</param>
        /// <returns>The "text" input string with rich text tags added to highlight the search string</returns>
        internal string GetHighlightedString(
            string highlightString,
            ref int mainHighlightIndex,
            string text,
            bool richTextEnabled)
        {
            if (string.IsNullOrEmpty(highlightString) || string.IsNullOrEmpty(text))
                return text;

            // Build cleanText and origIndex
            k_CleanBuilder.Clear();
            k_OrigIndex.Clear();

            var pos = 0;
            foreach (Match tag in k_TagRegex.Matches(text))
            {
                for (var i = pos; i < tag.Index; i++)
                {
                    k_CleanBuilder.Append(text[i]);
                    k_OrigIndex.Add(i);
                }

                pos = tag.Index + tag.Length;
            }

            for (var i = pos; i < text.Length; i++)
            {
                k_CleanBuilder.Append(text[i]);
                k_OrigIndex.Add(i);
            }

            var cleanText = k_CleanBuilder.ToString();
            var searchRegex = new Regex(Regex.Escape(highlightString),
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
            var matches = searchRegex.Matches(cleanText);

            if (matches.Count == 0)
                return richTextEnabled ? text : $"{k_NoparseStart}{text}{k_NoParseEnd}";

            // Map matches back to original text indices and merge overlapping
            var ranges = MergeRanges(matches
                .Select(m =>
                {
                    var start = k_OrigIndex[m.Index];
                    var end = k_OrigIndex[m.Index + m.Length - 1] + 1;
                    return (start, end);
                }).ToList());

            var sb = new StringBuilder();
            var lastIndex = 0;

            foreach (var r in ranges)
            {
                // Non-highlighted text
                if (r.start > lastIndex)
                {
                    if (richTextEnabled)
                    {
                        sb.Append(text.Substring(lastIndex, r.start - lastIndex));
                    }
                    else
                    {
                        sb.Append(k_NoparseStart);
                        sb.Append(text.Substring(lastIndex, r.start - lastIndex));
                        sb.Append(k_NoParseEnd);
                    }
                }

                // Highlighted text
                var insideNoparse = false;
                if (richTextEnabled)
                {
                    var nopOpen = text.LastIndexOf(k_NoparseStart, r.start, StringComparison.OrdinalIgnoreCase);
                    var nopClose = text.LastIndexOf(k_NoParseEnd, r.start, StringComparison.OrdinalIgnoreCase);
                    insideNoparse = nopOpen != -1 && (nopClose == -1 || nopClose < nopOpen);
                    if (insideNoparse) sb.Append(k_NoParseEnd);
                }

                sb.Append(GetHighlightedString(text.Substring(r.start, r.end - r.start), ref mainHighlightIndex));

                if (insideNoparse) sb.Append(k_NoparseStart);

                lastIndex = r.end;
            }

            // Trailing text
            if (lastIndex < text.Length)
            {
                if (richTextEnabled)
                    sb.Append(text.Substring(lastIndex));
                else
                    sb.Append(k_NoparseStart).Append(text.Substring(lastIndex)).Append(k_NoParseEnd);
            }

            // Cleanup adjacent <noparse> blocks
            if (!richTextEnabled)
            {
                var merged = k_NoParse1Regex.Replace(sb.ToString(), "");
                merged = k_NoParse2Regex.Replace(merged, "");
                return merged;
            }

            return sb.ToString();
        }

        /// <summary>
        /// Merges overlapping or adjacent ranges
        /// </summary>
        static List<(int start, int end)> MergeRanges(List<(int start, int end)> input)
        {
            if (input.Count == 0) return input;
            input.Sort((a, b) => a.start.CompareTo(b.start));
            var outRanges = new List<(int start, int end)>();
            var cur = input[0];
            for (var i = 1; i < input.Count; ++i)
            {
                var r = input[i];
                if (r.start <= cur.end)
                    cur.end = Math.Max(cur.end, r.end);
                else
                {
                    outRanges.Add(cur);
                    cur = r;
                }
            }

            outRanges.Add(cur);
            return outRanges;
        }

        static string GetHighlightedString(string s, ref int mainHighlightIndex)
        {
            if (mainHighlightIndex-- != 0)
                return $"{k_HighlightStartTag}{s}{k_HighlightEndTag}";

            return $"{k_HighlightMainStartTag}{s}{k_HighlightMainEndTag}";
        }

        internal static Rect GetHighlightedLineNumberRect(TextElement label)
        {
            var localRect = label.worldBound;

            var text = label.text;
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(k_HighlightMainStartTag))
                return localRect;

            var index = text.IndexOf(k_HighlightMainStartTag, StringComparison.Ordinal);
            if (index < 0)
                return localRect; // not found

            // Count how many '\n' there are in total:
            var totalLines = 1;
            var linesBeforeMainHighlight = 1;
            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (c == '\n')
                {
                    totalLines++;

                    if (i < index)
                        linesBeforeMainHighlight++;
                }
            }

            // Calculate the height of one line:
            var lineHeight = localRect.height / totalLines;
            // Make local rect only cover the highlighted line:
            localRect.y += lineHeight * (linesBeforeMainHighlight - 1);
            localRect.height = lineHeight;

            return localRect;
        }
        
        /// <summary>
        /// Marks every occurrence of <paramref name="highlightString"/> under <paramref name="roots"/>,
        /// which the view hands over in reading order: the main highlight is the <paramref name="index"/>-th
        /// occurrence counted along it.
        /// </summary>
        internal void Highlight(
            IEnumerable<VisualElement> roots,
            string highlightString,
            bool scrollMainIntoView,
            int index)
        {
            DropDetachedElements();

            if (highlightString != null)
            {
                foreach (var root in roots)
                    HighlightRecursive(root, highlightString, scrollMainIntoView, ref index);
            }

            ClearNotUpdatedSearchHighlights();
        }

        void HighlightRecursive(
            VisualElement element,
            string highlightString,
            bool scrollMainIntoView,
            ref int index)
        {
            if (element is TextElement label && m_HighlightableElements.TryGetValue(label, out var indexedBySearch))
            {
                var text = GetRenderedMessage(label.text, null);
                if (text.Contains(highlightString, StringComparison.OrdinalIgnoreCase))
                {
                    var highlightInfo = m_HighlightedElements.FirstOrDefault(info => info.Label == label);

                    if (highlightInfo == null)
                    {
                        highlightInfo = new HighlightStringInfo(label);
                        m_HighlightedElements.Add(highlightInfo);
                    }

                    highlightInfo.UpdatedThisFrame = true;

                    if (indexedBySearch)
                    {
                        var wasBeforeMainHighlight = index >= 0;

                        Mark(highlightInfo, highlightString, ref index);

                        // If this is the main element to highlight,
                        // wait a frame and then make sure the textElement is scrolled into view.
                        if (scrollMainIntoView && wasBeforeMainHighlight && index < 0)
                        {
                            ScrollLabelIntoView?.Invoke(label);
                        }
                    }
                    else
                    {
                        // Text the searcher never read holds no result of the conversation's, so its
                        // occurrences are marked off a count of their own and leave the reader's alone.
                        var uncounted = k_MainHighlightPassed;
                        Mark(highlightInfo, highlightString, ref uncounted);
                    }
                }
            }

            foreach (var child in element.Children())
                HighlightRecursive(child, highlightString, scrollMainIntoView, ref index);
        }

        void Mark(HighlightStringInfo info, string highlightString, ref int mainHighlightIndex)
        {
            info.Label.enableRichText = true;
            info.Label.text = GetHighlightedString(
                highlightString,
                ref mainHighlightIndex,
                info.OriginalText,
                info.EnableRichText);
        }

        /// <summary>Sheds the registrations whose element has left the panel, which no walk reaches and
        /// nothing else drops.</summary>
        void DropDetachedElements()
        {
            k_DetachedElements.Clear();

            foreach (var candidate in m_HighlightableElements.Keys)
            {
                if (candidate.panel == null)
                    k_DetachedElements.Add(candidate);
            }

            foreach (var detached in k_DetachedElements)
                m_HighlightableElements.Remove(detached);
        }

        /// <summary>
        /// Restores the original text and settings in all labels that were not updated this frame.
        /// </summary>
        /// <param name="overrideToClearAll">If true, all highlights are cleared, otherwise only highlights that have not been updated in the last highlight pass.</param>
        void ClearNotUpdatedSearchHighlights(bool overrideToClearAll = false)
        {
            var infosToRemove = new List<HighlightStringInfo>();
            foreach (var info in m_HighlightedElements)
            {
                if (overrideToClearAll || info.UpdatedThisFrame)
                {
                    info.UpdatedThisFrame = false;
                    continue;
                }

                Restore(info);
                infosToRemove.Add(info);
            }

            foreach (var info in infosToRemove)
            {
                m_HighlightedElements.Remove(info);
            }
        }

        internal void ClearAllHighlights()
        {
            ClearNotUpdatedSearchHighlights(true);
        }

        internal void ClearHighlightableElements()
        {
            ClearAllHighlights();
            m_HighlightableElements.Clear();
        }

        /// <param name="indexedBySearch">Whether <see cref="ConversationSearcher"/> reads the text this
        /// element draws. Occurrences in text it does not read are marked like any other, and counted
        /// nowhere: the reader's position is one of the searcher's results, which text it never read holds
        /// none of.</param>
        internal void RegisterHighlightableTextElement(TextElement textElement, bool indexedBySearch = true)
        {
            // If this was already highlighted, clear the highlight first to ensure the original text is restored:
            var existingHighlight = m_HighlightedElements.FirstOrDefault(t => t.Label == textElement);
            if (existingHighlight != null)
            {
                textElement.enableRichText = existingHighlight.EnableRichText;
                m_HighlightedElements.Remove(existingHighlight);
            }

            m_HighlightableElements[textElement] = indexedBySearch;
        }

        internal void UnregisterHighlightableTextElement(TextElement textElement)
        {
            // Restored before the record is dropped, or nothing is left that knows the element did not
            // render rich text before it was highlighted, and it keeps interpreting tags for the rest of
            // its pooled life.
            var existingHighlight = m_HighlightedElements.FirstOrDefault(t => t.Label == textElement);
            if (existingHighlight != null)
            {
                Restore(existingHighlight);
                m_HighlightedElements.Remove(existingHighlight);
            }

            m_HighlightableElements.Remove(textElement);
        }

        static void Restore(HighlightStringInfo info)
        {
            info.Label.enableRichText = info.EnableRichText;
            info.Label.text = info.OriginalText;
        }
    }
}
