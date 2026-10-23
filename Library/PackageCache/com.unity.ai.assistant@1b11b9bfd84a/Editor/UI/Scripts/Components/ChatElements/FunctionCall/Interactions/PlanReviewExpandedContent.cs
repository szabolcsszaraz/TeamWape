using System.Collections.Generic;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class PlanReviewExpandedContent : ManagedTemplate
    {
        readonly string m_PlanContent;

        public PlanReviewExpandedContent(string planContent)
            : base(AssistantUIConstants.UIModulePath)
        {
            m_PlanContent = planContent ?? string.Empty;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var container = view.Q<VisualElement>("contentContainer");
            var markdownElements = new List<VisualElement>();
            MarkdownAPI.MarkupText(Context, m_PlanContent, null, markdownElements, null);
            foreach (var el in markdownElements)
                container.Add(el);
        }
    }
}
