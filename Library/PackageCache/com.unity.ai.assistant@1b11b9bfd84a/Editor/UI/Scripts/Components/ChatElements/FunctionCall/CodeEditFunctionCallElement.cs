using System;
using System.IO;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Editor.FunctionCalling;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    [FunctionCallRenderer(typeof(CodeEditTool), nameof(CodeEditTool.SaveCode), Emphasized = true)]
    class CodeEditFunctionCallElement : ManagedTemplate, IFunctionCallRenderer, IExpandableRenderer, IInlineHeaderActionsProvider
    {
        const string k_FunctionDisplayName = "Code Edit";

        public virtual string Title => k_FunctionDisplayName;
        public virtual string TitleDetails { get; private set; }
        public virtual bool Expanded => true;

        bool m_IsInExpandedPanel;
        CodeBlockElement m_CodeDiff;
        VisualElement m_HeaderActions;

        public CodeEditFunctionCallElement() : base(AssistantUIConstants.UIModulePath) { }

        // Allows subclasses (e.g. EditPlanFunctionCallElement) to reuse this element's UXML
        // while being registered as a different FunctionCallRenderer.
        protected CodeEditFunctionCallElement(Type elementType) : base(elementType, AssistantUIConstants.UIModulePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_HeaderActions = view.Q("codeEditHeaderActions");

            m_CodeDiff = new CodeBlockElement();
            m_CodeDiff.Initialize(Context);
            m_CodeDiff.SetEmbeddedMode();
            m_CodeDiff.ShowSaveButton(false);
            view.Q("codeEditContent").Add(m_CodeDiff);

            m_CodeDiff.CloneActionButtons(m_HeaderActions);
        }

        public VisualElement GetInlineHeaderActions() => m_IsInExpandedPanel ? null : m_HeaderActions;

        public void OnCallRequest(AssistantFunctionCall functionCall)
        {
            TitleDetails = functionCall.Parameters?["description"]?.ToString();

            functionCall.GetCodeEditParameters(out var filePath, out var newCode, out var oldCode);

            // Drawn against the code it replaces, the block lays the code the searcher read out among
            // lines it never read: no occurrence in it sits where a result of the conversation says.
            m_CodeDiff.IndexedBySearch = string.IsNullOrEmpty(oldCode);

            if (!string.IsNullOrEmpty(newCode) || !string.IsNullOrEmpty(oldCode))
                m_CodeDiff.SetCode(newCode, oldCode);

            var filename = filePath != null ? Path.GetFileName(filePath) : string.Empty;
            m_CodeDiff.SetCustomTitle(filename);
            m_CodeDiff.SetFilename(filename);
        }

        public void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
          /* Compilation error display is currently disabled

           var typedResult = result.GetTypedResult<CodeEditTool.CodeEditOutput>();

            // If there is a compilation error, show the error after the code
            if (!string.IsNullOrEmpty(typedResult.CompilationOutput))
            {
                 Uncomment when code highlight is fixed to show error
                var errors = GetErrorsFromCompilationOutput(typedResult.CompilationOutput);
                m_CodeDiff.DisplayErrors(errors);
            }
            */
        }

        public void OnCallError(string functionId, Guid callId, string error)
        {
            Add(FunctionCallUtils.CreateContentLabel(error));
        }

        public void SetExpandedPanelMode()
        {
            m_IsInExpandedPanel = true;
            m_CodeDiff.ShowHorizontalScrollbar(true);
        }

        public VisualElement CreateHeaderActions()
        {
            var container = new VisualElement();
            container.AddToClassList("mui-header-actions-container");
            m_CodeDiff.CloneActionButtons(container);
            return container;
        }

        CompilationErrors GetErrorsFromCompilationOutput(string compilationOutput)
        {
            var errors = new CompilationErrors();
            var lines = compilationOutput.Split(new[] { AssistantConstants.NewLineCRLF, AssistantConstants.NewLineLF }, StringSplitOptions.None);
            foreach (var line in lines)
            {
                var error = CompilationErrorUtils.Parse(line);
                errors.Add(error.Message, error.Line);
            }

            return errors;
        }
    }
}
