using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class ChatElementBlockAnswer : ChatElementBlockMarkdown<AnswerBlockModel>
    {
        VisualElement m_TextFieldRoot;
        Foldout m_SourcesFoldout;
        VisualElement m_SourcesContent;

        protected override void InitializeView(TemplateContainer view)
        {
            base.InitializeView(view);

            m_TextFieldRoot = view.Q<VisualElement>("textFieldRoot");

            m_SourcesFoldout = view.Q<Foldout>("sourcesFoldout");
            m_SourcesFoldout.RegisterValueChangedCallback(evt => OnSourcesFoldoutChanged(evt.newValue));
            m_SourcesContent = view.Q<VisualElement>("sourcesContent");
        }

        /// <summary>
        /// Applies the expansion the flattener recorded for this answer's sources, which the pooled
        /// element cannot carry: it renders whichever answer it was last given. Binding is not a user
        /// gesture, so the foldout is set without notification and no height change is reported.
        /// </summary>
        public void SetChromeState(bool expanded)
        {
            m_SourcesFoldout.SetValueWithoutNotify(expanded);
        }

        protected override void OnBlockModelChanged()
        {
            if (ShouldRebuildMarkdown(BlockModel.Content, BlockModel.IsComplete))
            {
                BuildMarkdownChunks(BlockModel.Content, BlockModel.IsComplete);
                RefreshText(m_TextFieldRoot);
            }

            RefreshSourceBlocks();
        }

        void RefreshSourceBlocks()
        {
            if (!BlockModel.IsComplete || m_SourceBlocks == null || m_SourceBlocks.Count == 0)
            {
                m_SourcesFoldout.style.display = DisplayStyle.None;
                return;
            }

            m_SourcesFoldout.style.display = DisplayStyle.Flex;
            m_SourcesContent.Clear();

            for (var index = 0; index < m_SourceBlocks.Count; index++)
            {
                var sourceBlock = m_SourceBlocks[index];
                var entry = new ChatElementSourceEntry();
                entry.Initialize(Context);
                entry.SetData(index, sourceBlock);
                m_SourcesContent.Add(entry);
            }
        }

        void OnSourcesFoldoutChanged(bool expanded)
        {
            this.ReportHeightChange(expanded);
            EditorTask.delayCall += Context.SendScrollToEndRequest;
        }
    }
}
