using System;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// UI element for displaying Unity function calls in the assistant chat.
    /// Uses an IFunctionCallRenderer to customize the display based on the function type.
    /// </summary>
    class FunctionCallElement : FunctionCallBaseElement
    {
        const string k_AgentBadgeClassName = "mui-function-call-agent-badge";

        IFunctionCallRenderer Renderer { get; }
        bool GotResult { get; set; }
        Guid CallId { get; set; }
        string FunctionId { get; set; }
        AssistantFunctionCall m_StoredFunctionCall;

        Label m_AgentBadge;

        public FunctionCallElement() : this(null) { }

        public FunctionCallElement(IFunctionCallRenderer renderer)
        {
            Renderer = renderer;
        }

        protected override void InitializeContent()
        {
            ContentRoot.Add(Renderer as VisualElement);

            if (Renderer is ManagedTemplate managedTemplate)
                managedTemplate.Initialize(Context);
            if (Renderer is IAssistantUIContextAware contextAware)
                contextAware.Context = Context;

            if (Renderer is IInlineHeaderActionsProvider provider)
                PlaceInlineHeaderActions(provider.GetInlineHeaderActions());
        }

        public void OnConversationCancelled()
        {
            if (CurrentState == ToolCallState.InProgress)
                OnCallError(FunctionId, CallId, "Conversation cancelled.");
        }

        public void UpdateData(AssistantFunctionCall functionCall)
        {
            if (CallId != functionCall.CallId)
            {
                // Store the call id and function id to track the state of the function call
                CallId = functionCall.CallId;
                FunctionId = functionCall.FunctionId;
                GotResult = false;

                OnCallRequest(functionCall);
            }

            if (!GotResult && functionCall.Result.IsDone)
            {
                m_StoredFunctionCall = functionCall;

                if (functionCall.Result.HasFunctionCallSucceeded)
                    OnCallSuccess(functionCall.FunctionId, functionCall.CallId, functionCall.Result);
                else
                    OnCallError(functionCall.FunctionId, functionCall.CallId, GetErrorMessage(functionCall.Result.Result));

                GotResult = true;
                SetupExpandButton();
            }
        }

        // Success means the call was performed without throwing any exception.
        // Internal logic to display a failed state even if the call succeeded (ex: didCompile = false) should be handled here
        void OnCallRequest(AssistantFunctionCall functionCall)
        {
            SetState(ToolCallState.InProgress);

            // Clear the existing visual tree to support element pooling and reuse.
            // Existing IFunctionCallRenderer implementations in the package build their UI
            // dynamically via Add() during lifecycle methods rather than in constructors.
            // Failing to clear here results in duplicated, stacked UI elements.
            // ManagedTemplate renderers set up their UI once in InitializeView and must not be cleared.
            if (Renderer is VisualElement rendererElement and not ManagedTemplate)
                rendererElement.Clear();

            Renderer.OnCallRequest(functionCall);

            if (functionCall.SubAgentsActive
                && !string.IsNullOrEmpty(functionCall.Agent)
                && !SubagentHeaderElement.IsSubagent(functionCall.Agent))
            {
                SetAgentBadge(FormatAgentBadge(functionCall.Agent));
            }
            else
            {
                SetAgentBadge(null);
            }

            SetTitle(Renderer.Title);
            SetDetails(Renderer.TitleDetails);

            if (Renderer.Expanded)
            {
                EnableChevronHover();
                EnableFoldout();
                SetFoldoutExpanded(true);
            }
        }

        void OnCallSuccess(string functionId, Guid callId, FunctionCallResult result)
        {
            SetState(ToolCallState.Success);
            Renderer.OnCallSuccess(functionId, callId, result);
            if (Renderer.Expanded)
                SetFoldoutExpanded(true);
            EnableChevronHover();
            EnableFoldout();
        }

        void OnCallError(string functionId, Guid callId, string error)
        {
            SetState(ToolCallState.Failed);

            Renderer.OnCallError(functionId, callId, error);

            if (error != null)
            {
                EnableChevronHover();
                EnableFoldout();
            }
        }

        internal void ForceExpandedPanel()
        {
            HideHeader();
            HideExpandButton();
            DisableContentScroll();
            SetFoldoutExpanded(true);
        }

        protected override EventExpandedViewRequested CreateExpandedViewRequest()
        {
            var titleText = string.IsNullOrEmpty(Renderer.TitleDetails) ? Renderer.Title : $"{Renderer.Title}: {Renderer.TitleDetails}";

            var renderer = FunctionCallRendererFactory.CreateFunctionCallRenderer(m_StoredFunctionCall.FunctionId);
            var element = new FunctionCallElement(renderer);
            element.Initialize(Context);
            if (renderer is IExpandableRenderer expandable)
                expandable.SetExpandedPanelMode();
            element.UpdateData(m_StoredFunctionCall);
            element.ForceExpandedPanel();

            return new EventExpandedViewRequested(titleText, element, CreateRendererHeaderActions(renderer));
        }

        VisualElement CreateRendererHeaderActions(IFunctionCallRenderer renderer)
        {
            if (renderer is RunCommandFunctionCallElement runCommand
                && Renderer is RunCommandFunctionCallElement { IsCodeTabActive: true })
                runCommand.ShowCodeTab();

            return (renderer as IExpandableRenderer)?.CreateHeaderActions();
        }

        static string GetErrorMessage(JToken result) => result?.Type == JTokenType.String ? result.Value<string>() : null;

        // Converts internal agent identifiers (e.g., "CoreAgent") to display strings ("Core Agent").
        static string FormatAgentBadge(string agent)
        {
            if (string.Equals(agent, "CoreAgent", StringComparison.Ordinal))
                return "Core Agent";
            return agent;
        }

        void SetAgentBadge(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                if (m_AgentBadge != null)
                {
                    m_AgentBadge.SetDisplay(false);
                }
                return;
            }
            if (m_AgentBadge == null)
            {
                m_AgentBadge = new Label();
                m_AgentBadge.AddToClassList(k_AgentBadgeClassName);
                m_Header.Insert(m_Header.IndexOf(m_Title), m_AgentBadge);
            }
            m_AgentBadge.text = text;
            m_AgentBadge.SetDisplay(true);
        }

    }
}
