using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementUser : ChatElementBase
    {
        VisualElement m_TextFieldRoot;
        Foldout m_ContextFoldout;
        VisualElement m_ContextContent;
        Label m_Prompt;
        MessageModel m_Message;

        protected override void InitializeView(TemplateContainer view)
        {
            m_ContextFoldout = view.Q<Foldout>("contextFoldout");
            m_ContextFoldout.SetValueWithoutNotify(false);
            m_ContextFoldout.RegisterValueChangedCallback(OnContextFoldoutToggled);

            m_ContextContent = view.Q<VisualElement>("contextContent");

            m_TextFieldRoot = view.Q<VisualElement>("userMessageTextFieldRoot");
        }

        /// <summary>
        /// Set the user data used by this element
        /// </summary>
        /// <param name="message">the message to display</param>
        public override void SetData(MessageModel message)
        {
            var isFirstBuild = m_Prompt == null;

            // Validate that there is only one block and that it's a markdown block
            if (isFirstBuild && (message.Blocks == null || message.Blocks.Count != 1 || message.Blocks[0] is not PromptBlockModel))
                throw new System.Exception("User message should only contain one markdown block");

            var isAlreadyDisplayed = !isFirstBuild
                && m_Message.Id == message.Id
                && m_Message.HasContent()
                && m_Message.HasEqualContent(message);

            m_Message = message;

            if (isAlreadyDisplayed)
                return;

            if (isFirstBuild)
            {
                m_Prompt = new Label
                {
                    selection = { isSelectable = true },
                    enableRichText = false
                };
                m_TextFieldRoot.Add(m_Prompt);
            }
            else
            {
                Context.SearchHelper?.UnregisterSearchableTextElement(m_Prompt);
            }

            m_Prompt.text = PromptContent(message);

            Context.SearchHelper?.RegisterSearchableTextElement(m_Prompt);

            RefreshContext(message);
        }

        static string PromptContent(MessageModel message)
            => message.Blocks is { Count: > 0 } blocks && blocks[0] is PromptBlockModel prompt ? prompt.Content : null;

        void OnContextFoldoutToggled(ChangeEvent<bool> evt)
        {
            if (evt.newValue)
            {
                AIAssistantAnalytics.ReportUITriggerLocalExpandUserMessageContextEvent(
                    m_Message.Id,
                    m_Message.Context?.Length ?? 0);
            }

            this.ReportHeightChange();
        }

        void RefreshContext(MessageModel message)
        {
            m_ContextContent.Clear();

            if (message.Context == null || message.Context.Length == 0)
            {
                m_ContextFoldout.style.display = DisplayStyle.None;
                return;
            }

            m_ContextFoldout.style.display = DisplayStyle.Flex;

            for (var index = 0; index < message.Context.Length; index++)
            {
                var contextEntry = message.Context[index];
                var entry = new ContextElement();
                entry.Initialize(Context);
                entry.SetData(contextEntry);
                entry.AddChatElementUserStyling();
                m_ContextContent.Add(entry);
            }
        }
    }
}
