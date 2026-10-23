using System;
using Markdig.Renderers;
using Markdig.Syntax;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Markup.Renderers
{
    internal class HeadingBlockRenderer : MarkdownObjectRenderer<ChatMarkdownRenderer, HeadingBlock>
    {
        readonly AssistantUIContext m_Context;

        public HeadingBlockRenderer(AssistantUIContext context)
        {
            m_Context = context;
        }

        protected override void Write(ChatMarkdownRenderer renderer, HeadingBlock obj)
        {
            // TODO: Figure out the size of the heading text to align with our (.uss) styling
            int level = Math.Clamp(obj.Level, 1, 6);

            renderer.CloseTextElement();

            var textElement = renderer.GetCurrentTextElement();

            textElement.name = "responseHeading";

            textElement.AddToClassList("mui-chat-response-heading");
            textElement.AddToClassList($"mui-chat-response-heading-size-{level}");

            if (obj.Inline != null)
                renderer.Write(obj.Inline);

            renderer.CloseTextElement();

            // A heading is in the content the searcher reads, so its occurrences are positions the reader
            // is walked onto and the label drawing them has to be one the walk can mark.
            m_Context.SearchHelper?.RegisterSearchableTextElement(textElement);
        }
    }
}
