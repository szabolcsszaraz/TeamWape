using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Markup;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    abstract class CommandDisplayTemplate : ManagedTemplate
    {
        public class ContentGroup
        {
            public string Info = "";
            public string Content = "";
            public string Arguments = "";
            public DisplayState State;
            public string Logs = "";

            public ContentGroup() { }

            public ContentGroup(ContentGroup other)
            {
                Info = other.Info;
                Content = other.Content;
                Arguments = other.Arguments;
                State = other.State;
                Logs = other.Logs;
            }
        }
        public enum DisplayState
        {
            Success = 0,
            Fail
        }

        const string k_ExpandButtonName = "commandCodeBlockExpandButton";

        readonly List<ContentGroup> m_Content = new();
        readonly List<ContentGroup> m_ExtraContent = new();

        protected CodeBlockElement m_CodeElement;

        string m_Title;
        string m_Filename;
        string m_CodeType;
        bool m_ReformatCode;

        protected CommandDisplayTemplate(System.Type customElementType, string basePath = null) : base(customElementType, basePath) { }
        protected CommandDisplayTemplate(string basePath = null, string subPath = null) : base(basePath, subPath) { }
        public string Fence { get; set; }

        protected override void InitializeView(TemplateContainer view)
        {
            m_CodeElement = new CodeBlockElement();
            m_CodeElement.Initialize(Context);

            var expandButton = view.Q<Button>(k_ExpandButtonName);
            if (expandButton != null)
            {
                expandButton.RegisterCallback<PointerUpEvent>(OnExpandButtonClicked);
                m_CodeElement.Q("actionButtonsContainer").Insert(0, expandButton);
            }
        }

        public void SetContent(FencedContent content)
        {
            m_Content.Clear();
            m_Content.Add(new ContentGroup
            {
                Content = content.Content,
                Info = content.Info,
                Arguments = content.Arguments
            });
        }

        public void SetExtraContent(IList<FencedContent> extraContent)
        {
            m_ExtraContent.Clear();
            foreach (var content in extraContent)
            {
                m_ExtraContent.Add(new ContentGroup
                {
                    Content = content.Content,
                    Info = content.Info,
                    Arguments = content.Arguments
                });
            }
        }

        public void AddContent(string content)
        {
            m_Content.Add(new ContentGroup { Content = content });
        }

        public List<ContentGroup> ContentGroups => m_Content;
        public List<ContentGroup> ExtraContent => m_ExtraContent;

        public bool Validate(int index)
        {
            if (index < 0 || index >= m_Content.Count)
            {
                Debug.LogWarning("Invalid index supplied");
                return false;
            }

            var group = m_Content[index];
            var valid = ValidateInternal(index, out group.Logs);
            group.State = valid ? DisplayState.Success : DisplayState.Fail;
            return valid;
        }

        protected virtual bool ValidateInternal(int index, out string logs)
        {
            logs = null;
            return true;
        }

        public abstract void Display(bool isUpdate = false);

        public virtual void Sync()
        {

        }

        public virtual void SetCustomTitle(string title)
        {
            m_Title = title;
        }

        public virtual void SetCodeReformatting(bool reformatCode)
        {
            m_ReformatCode = reformatCode;
        }

        public virtual void SetCodeType(string codeType)
        {
            m_CodeType = codeType;
        }

        public virtual void SetFilename(string filename)
        {
            m_Filename = filename;
        }

        void OnExpandButtonClicked(PointerUpEvent _)
        {
            var request = CreateExpandedViewRequest();
            if (request != null)
                AssistantEvents.Send(request);
        }

        protected virtual EventExpandedViewRequested CreateExpandedViewRequest()
        {
            if (ContentGroups.Count == 0)
                return null;

            var titleText = m_Title ?? m_Filename ?? "Code";

            var instance = CreateExpandedInstance();
            instance.Initialize(Context);

            foreach (var group in ContentGroups)
                instance.AddContent(group.Content);

            if (m_Title != null) instance.SetCustomTitle(m_Title);
            if (m_Filename != null) instance.SetFilename(m_Filename);
            if (m_CodeType != null) instance.SetCodeType(m_CodeType);
            instance.SetCodeReformatting(m_ReformatCode);

            instance.Display();
            instance.ForceExpandedPanel();

            var headerActions = instance.CreateExpandedHeaderActions();

            return new EventExpandedViewRequested(titleText, instance, headerActions);
        }

        protected virtual CommandDisplayTemplate CreateExpandedInstance()
        {
            return (CommandDisplayTemplate)Activator.CreateInstance(GetType());
        }

        internal virtual void ForceExpandedPanel()
        {
            m_CodeElement?.SetEmbeddedMode();
        }

        internal virtual VisualElement CreateExpandedHeaderActions()
        {
            if (m_CodeElement == null)
                return null;

            var container = new VisualElement();
            container.AddToClassList("mui-header-actions-container");
            m_CodeElement.CloneActionButtons(container);
            return container;
        }
    }
}
