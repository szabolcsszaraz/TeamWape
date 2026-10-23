using System;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Tools.Editor.UI;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.PromptSuggestions
{
    internal struct PromptData
    {
        public readonly string Text;
        public readonly string UploadHintText;

        public PromptData(string text, string hint = null)
        {
            Text = text;
            UploadHintText = hint;
        }
    }
    
    internal abstract class TabData
    {
        public string Label;
        public PromptData[] Prompts;
        public Action Collapse;
        public AssistantUIContext Context;
        public Action<string> OnPromptSelected;
        
        public abstract void BuildContent(VisualElement container);

        protected VisualElement BuildSinglePrompt(PromptData prompt)
        {
            var row = new VisualElement();
            row.AddToClassList("mui-prompt-suggestions-item");

            var label = new Label(prompt.Text);
            label.AddToClassList("mui-prompt-suggestions-item-label");
            row.Add(label);

            if (prompt.UploadHintText != null)
                row.Add(BuildUploadHint(prompt.UploadHintText));

            row.AddManipulator(new Clickable(() =>
            {
                Collapse();
                AIAssistantAnalytics.ReportUITriggerLocalSuggestionPromptSelectedEvent(Label,
                    prompt.Text);
                OnPromptSelected?.Invoke(prompt.Text);
            }));

            return row;
        }
        
        VisualElement BuildUploadHint(string hintText)
        {
            var hint = new VisualElement();
            hint.AddToClassList("mui-prompt-suggestions-upload-hint");

            var hintLabel = new Label(hintText);
            hintLabel.AddToClassList("mui-prompt-suggestions-upload-hint-label");
            hint.Add(hintLabel);

            var hintIcon = new Image();
            hintIcon.AddToClassList("mui-prompt-suggestions-upload-hint-icon");
            hintIcon.AddToClassList("mui-icon-sort-descending");
            hint.Add(hintIcon);

            return hint;
        }
    }

    internal class PromptTab : TabData
    {
        public override void BuildContent(VisualElement container)
        {
            foreach (var prompt in Prompts)
            {
                container.Add(BuildSinglePrompt(prompt));
            }
        }
    }

    internal class FigmaAuthTab : TabData
    {
        public override void BuildContent(VisualElement container)
        {    
            if (!FigmaToUI.HasToken)
            {
              var authBar = new FigmaAuthBar();
              authBar.Initialize(Context);
              container.Add(authBar);
            }
            else
            {
                foreach (var prompt in Prompts)
                {
                  container.Add(BuildSinglePrompt(prompt));
                }
            }
        }
    }
}