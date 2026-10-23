using System.Text.RegularExpressions;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementBlockThought : ChatElementBlockMarkdown<ThoughtBlockModel>
    {
        static readonly Regex k_TitleRegex = new(@"\*\*(.+?)\*\*", RegexOptions.Compiled);

        Label m_TitleLabel;
        VisualElement m_Container;

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_TitleLabel = view.Q<Label>("titleLabel");
            m_Container = view.Q("textContent");
        }

        protected override void OnBlockModelChanged()
        {
            // Extract title and get content without title
            var title = ExtractTitle(BlockModel.Content, out var contentWithoutTitle);

            Context.SearchHelper?.UnregisterSearchableTextElement(m_TitleLabel);

            // Display title
            if (!string.IsNullOrEmpty(title))
            {
                m_TitleLabel.text = title;
                m_TitleLabel.SetDisplay(true);

                // The title is cut out of the block's own content, which the searcher reads whole, so its
                // occurrences are positions the reader is walked onto and this label has to be markable.
                Context.SearchHelper?.RegisterSearchableTextElement(m_TitleLabel);
            }
            else
            {
                m_TitleLabel.SetDisplay(false);
            }

            if (!ShouldRebuildMarkdown(contentWithoutTitle, true))
                return;

            BuildMarkdownChunks(contentWithoutTitle, true);
            RefreshText(m_Container);
        }

        public static string ExtractTitle(string content, out string contentWithoutTitle)
        {
            contentWithoutTitle = content;

            if (string.IsNullOrEmpty(content))
                return null;

            var match = k_TitleRegex.Match(content);
            if (!match.Success)
                return null;

            // Remove the title line from content
            contentWithoutTitle = content.Substring(match.Index + match.Length);
            
            // Trim actual newlines
            contentWithoutTitle = contentWithoutTitle.TrimStart('\r', '\n', ' ');
            
            // Also trim escaped newlines (\\n) that may appear at the start
            while (contentWithoutTitle.StartsWith("\\n"))
                contentWithoutTitle = contentWithoutTitle.Substring(2).TrimStart();
            
            return match.Groups[1].Value;
        }
    }
}
