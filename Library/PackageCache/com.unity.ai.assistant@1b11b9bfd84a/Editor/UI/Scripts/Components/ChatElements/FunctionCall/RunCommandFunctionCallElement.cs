using System;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(RunCommandTool), nameof(RunCommandTool.ExecuteCommand), Emphasized = true)]
    class RunCommandFunctionCallElement : ManagedTemplate, IFunctionCallRenderer, IExpandableRenderer, IInlineHeaderActionsProvider
    {
        const string k_TabActiveClass = "tab-active";

        string m_FunctionDisplayName;

        protected virtual string DefaultTitle => "Run Command";

        public virtual string Title => m_FunctionDisplayName ?? DefaultTitle;
        public virtual string TitleDetails { get; private set; }
        public virtual bool Expanded => true;

        bool m_IsInExpandedPanel;

        VisualElement m_HeaderActions;
        VisualElement m_TabButtons;
        Button m_CodeTab;
        Button m_OutputTab;
        VisualElement m_CodePane;
        VisualElement m_OutputPane;
        CodeBlockElement m_CodeBlock;
        VisualElement m_LogsContainer;

        public RunCommandFunctionCallElement() : base(AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            view.AddToClassList("run-command-tabs");

            m_TabButtons = view.Q("runCommandTabButtons");
            m_CodeTab = view.SetupButton("runCommandCodeTab", OnCodeTabClicked);
            m_OutputTab = view.SetupButton("runCommandOutputTab", OnOutputTabClicked);

            m_CodePane = view.Q<VisualElement>("runCommandCodePane");
            m_OutputPane = view.Q<VisualElement>("runCommandOutputPane");
            m_LogsContainer = view.Q<VisualElement>("runCommandLogsContainer");

            m_CodeBlock = new CodeBlockElement();
            m_CodeBlock.Initialize(Context);

            // The searcher reads a call for the code an edit saved, and this is the code a command ran.
            m_CodeBlock.IndexedBySearch = false;

            m_CodeBlock.SetCodeType("csharp");
            m_CodeBlock.SetActions(copy: true, save: false, select: true, edit: false);
            m_CodeBlock.SetEmbeddedMode();
            m_CodePane.Add(m_CodeBlock);

            m_HeaderActions = view.Q("runCommandHeaderActions");
            m_CodeBlock.CloneActionButtons(m_HeaderActions);
        }

        public VisualElement GetInlineHeaderActions() => m_IsInExpandedPanel ? null : m_HeaderActions;

        public void SetExpandedPanelMode()
        {
            m_IsInExpandedPanel = true;
            m_CodeBlock.ShowHorizontalScrollbar(true);
        }

        public VisualElement CreateHeaderActions()
        {
            var container = new VisualElement();
            container.AddToClassList("mui-header-actions-container");
            container.Add(m_TabButtons);
            m_CodeBlock.CloneActionButtons(container);
            return container;
        }

        void OnCodeTabClicked(PointerUpEvent evt)
        {
            ShowCodeTab();

            // Reported from the gesture: the call's own result switches tabs too, while the element is
            // being bound, and nothing has moved under a cursor there.
            this.ReportHeightChange();
            AIAssistantAnalytics.ReportUITriggerLocalSwitchRunCommandTabEvent(
                Context.Blackboard.ActiveConversationId, "Code");
        }

        void OnOutputTabClicked(PointerUpEvent evt)
        {
            ShowOutputTab();
            this.ReportHeightChange();
            AIAssistantAnalytics.ReportUITriggerLocalSwitchRunCommandTabEvent(
                Context.Blackboard.ActiveConversationId, "Output");
        }

        internal bool IsCodeTabActive => m_CodePane.style.display == DisplayStyle.Flex;

        internal void ShowCodeTab()
        {
            m_CodeTab.AddToClassList(k_TabActiveClass);
            m_OutputTab.RemoveFromClassList(k_TabActiveClass);
            m_CodePane.SetDisplay(true);
            m_OutputPane.SetDisplay(false);
        }

        void ShowOutputTab()
        {
            m_CodeTab.RemoveFromClassList(k_TabActiveClass);
            m_OutputTab.AddToClassList(k_TabActiveClass);
            m_CodePane.SetDisplay(false);
            m_OutputPane.SetDisplay(true);
        }

        public void OnCallRequest(AssistantFunctionCall functionCall)
        {
            var code = functionCall.Parameters?["code"]?.ToString();
            var title = functionCall.Parameters?["title"]?.ToString();

            if (!string.IsNullOrEmpty(title))
                m_FunctionDisplayName = title;

            if (!string.IsNullOrEmpty(code))
                m_CodeBlock.SetCode(code);
        }

        public void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
            var typedResult = result.GetTypedResult<RunCommandTool.ExecutionOutput>();

            // Display execution logs if present
            if (!string.IsNullOrEmpty(typedResult.ExecutionLogs))
                DisplayFormattedLogs(typedResult.ExecutionLogs);
        }

        public void OnCallError(string functionId, Guid callId, string error)
        {
            if (!string.IsNullOrEmpty(error))
                DisplayFormattedLogs(error);
        }

        void DisplayFormattedLogs(string logs)
        {
            ShowOutputTab();
            ExecutionLogFormatter.PopulateLogContainer(m_LogsContainer, logs);
        }
    }
}
