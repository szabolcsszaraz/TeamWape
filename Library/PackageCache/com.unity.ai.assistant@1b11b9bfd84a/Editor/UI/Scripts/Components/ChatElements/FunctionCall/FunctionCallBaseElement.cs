using System;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    /// <summary>
    /// Abstract base class for tool/function call UI elements.
    /// Provides common visual infrastructure for displaying tool calls with title, status indicator,
    /// and expandable content area. Subclasses handle specific data models (AssistantFunctionCall, AcpToolCallInfo, etc.).
    /// </summary>
    abstract class FunctionCallBaseElement : ManagedTemplate, ISearchRevealable
    {
        protected const string k_TextFieldClassName = "mui-function-call-text-field";
        protected const string k_ExpandButtonHiddenClass = "mui-expand-button--hidden";
        protected const string k_ExpandedContentClassName = "mui-function-call-expanded";
        protected const string k_StatusSuccessClassName = "mui-function-call-status-success";
        protected const string k_StatusInProgressClassName = "mui-function-call-status-in-progress";
        protected const string k_StatusFailedClassName = "mui-function-call-status-failed";
        protected const string k_IconCheckmarkClassName = "mui-icon-checkmark";
        protected const string k_IconCloseClassName = "mui-icon-arrows-counter-clockwise";
        const string k_ScrollDisabledClassName = "mui-function-call-scroll-disabled";
        const string k_ChevronClass = "function-call-chevron";
        const string k_ChevronHiddenClass = "function-call-chevron--hidden";
        const string k_IconExpandedClass = "mui-icon-arrow-caret-down";
        const string k_IconCollapsedClass = "mui-icon-arrow-caret-right";
        const string k_StatusHiddenClass = "function-call-status--hidden";

        VisualElement m_Root;
        protected VisualElement m_Header;
        protected Label m_Title;
        Label m_Parameters;
        Button m_ExpandButton;
        VisualElement m_StatusElement;
        LoadingSpinner m_LoadingSpinner;
        ScrollView m_ScrollView;
        bool m_FoldoutDisabled;
        Image m_Chevron;
        bool m_ChevronHoverEnabled;

        protected enum ToolCallState
        {
            InProgress,
            Success,
            Failed
        }

        protected VisualElement ContentRoot { get; private set; }
        protected ToolCallState CurrentState { get; private set; }

        protected FunctionCallBaseElement() : base(typeof(FunctionCallBaseElement), AssistantUIConstants.UIModulePath) { }

        protected FunctionCallBaseElement(string basePath) : base(typeof(FunctionCallBaseElement), basePath) { }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q("functionCallEntryRoot");
            m_Header = view.Q("functionCallHeader");

            m_Title = view.Q<Label>("functionCallTitle");
            m_Parameters = view.Q<Label>("functionCallParameters");
            m_ExpandButton = view.SetupButton("functionCallExpandButton", OnExpandButtonClicked);
            ContentRoot = view.Q<VisualElement>("functionCallContent");
            m_StatusElement = view.Q("functionCallStatus");
            m_ScrollView = view.Q<ScrollView>("functionCallScrollView");

            m_Title.AddToClassList(k_TextFieldClassName);

            // Create and initialize the loading spinner
            m_LoadingSpinner = new LoadingSpinner();
            m_StatusElement.Add(m_LoadingSpinner);
            m_LoadingSpinner.AddToClassList(k_StatusInProgressClassName);
            m_LoadingSpinner.Show();

            ContentRoot.SetDisplay(false);

            InitializeContent();
        }

        /// <summary>
        /// Called during initialization to allow subclasses to set up their content in ContentRoot.
        /// </summary>
        protected abstract void InitializeContent();

        protected void SetState(ToolCallState state)
        {
            if (state == CurrentState)
                return;

            switch (state)
            {
                case ToolCallState.InProgress:
                    m_StatusElement.RemoveFromClassList(k_StatusSuccessClassName);
                    m_StatusElement.RemoveFromClassList(k_StatusFailedClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCheckmarkClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCloseClassName);
                    m_LoadingSpinner?.Show();
                    break;
                case ToolCallState.Success:
                    m_LoadingSpinner?.Hide();
                    m_StatusElement.RemoveFromClassList(k_StatusFailedClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCloseClassName);
                    m_StatusElement.AddToClassList(k_StatusSuccessClassName);
                    m_StatusElement.AddToClassList(k_IconCheckmarkClassName);

                    // This is temporary until this is fixed in UITK https://jira.unity3d.com/browse/UUM-108227
                    m_StatusElement.AddToClassList("mui-icon-tint-success");
                    break;
                case ToolCallState.Failed:
                    m_LoadingSpinner?.Hide();
                    m_StatusElement.RemoveFromClassList(k_StatusSuccessClassName);
                    m_StatusElement.RemoveFromClassList(k_IconCheckmarkClassName);
                    m_StatusElement.AddToClassList(k_StatusFailedClassName);
                    m_StatusElement.AddToClassList(k_IconCloseClassName);

                    // This is temporary until this is fixed in UITK https://jira.unity3d.com/browse/UUM-108227
                    m_StatusElement.AddToClassList("mui-icon-tint-error");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(state), state, null);
            }

            CurrentState = state;
        }

        protected void SetTitle(string title) => m_Title.text = title;

        protected void SetContentMaxHeight(float height) => m_ScrollView.style.maxHeight = height;

        protected void PlaceInlineHeaderActions(VisualElement headerActions)
        {
            m_Header.Add(headerActions);
            headerActions.Insert(Math.Min(1, headerActions.childCount), m_ExpandButton);
            m_ScrollView.style.maxHeight = StyleKeyword.None;
        }

        protected void DisableContentScroll()
        {
            m_ScrollView.AddToClassList(k_ScrollDisabledClassName);
            m_ScrollView.mode = ScrollViewMode.Vertical;
            m_ScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
            m_ScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        }

        protected void SetDetails(string details)
        {
            m_Parameters.text = details;
            m_Header.tooltip = details;
        }

        void OnExpandButtonClicked(PointerUpEvent _)
        {
            var request = CreateExpandedViewRequest();
            if (request != null)
                AssistantEvents.Send(request);
        }

        protected virtual EventExpandedViewRequested CreateExpandedViewRequest() => null;

        protected void SetupExpandButton()
        {
            m_ExpandButton.RemoveFromClassList(k_ExpandButtonHiddenClass);
        }

        protected void DisableFoldout()
        {
            m_FoldoutDisabled = true;
            m_Header.UnregisterCallback<MouseDownEvent>(ToggleFoldout);
        }

        protected void HideExpandButton() => m_ExpandButton.AddToClassList(k_ExpandButtonHiddenClass);

        protected void HideHeader() => m_Header.SetDisplay(false);

        protected void EnableFoldout()
        {
            if (m_FoldoutDisabled)
                return;

            // Unregister first to ensure the callback is only registered once
            // regardless of how many times EnableFoldout is called
            m_Header.UnregisterCallback<MouseDownEvent>(ToggleFoldout);
            m_Header.RegisterCallback<MouseDownEvent>(ToggleFoldout);
        }

        void ToggleFoldout(MouseDownEvent evt)
        {
            var newValue = !IsContentVisible();
            SetFoldoutExpanded(newValue);

            // Reported from the gesture rather than from SetFoldoutExpanded, which a call's own data also
            // calls while the element is being bound, where nothing has moved under a cursor.
            this.ReportHeightChange();
            AIAssistantAnalytics.ReportUITriggerLocalExpandFunctionCallResultEvent(
                Context.Blackboard.ActiveConversationId, m_Title.text, newValue);
        }

        protected void SetFoldoutExpanded(bool expanded)
        {
            ContentRoot.SetDisplay(expanded);

            if (expanded)
                m_Root.AddToClassList(k_ExpandedContentClassName);
            else
                m_Root.RemoveFromClassList(k_ExpandedContentClassName);

            SyncChevronRotation();
        }

        protected bool IsContentVisible() => ContentRoot.style.display != DisplayStyle.None;

        public bool RevealForSearch(VisualElement hidden)
        {
            if (IsContentVisible() || !ContentRoot.Contains(hidden))
                return false;

            SetFoldoutExpanded(true);

            // Reported the way the header's own gesture reports it: the calls below this one move either
            // way.
            this.ReportHeightChange();
            return true;
        }

        protected void EnableChevronHover()
        {
            if (m_ChevronHoverEnabled) return;
            m_ChevronHoverEnabled = true;

            if (m_Chevron == null)
            {
                m_Chevron = new Image();
                m_Chevron.AddToClassList(k_ChevronClass);
                m_Chevron.AddToClassList(k_ChevronHiddenClass);
                m_Chevron.AddToClassList(k_IconExpandedClass);
                m_Header.Insert(0, m_Chevron);
            }

            SyncChevronRotation();

            m_Header.RegisterCallback<MouseEnterEvent>(OnChevronMouseEnter);
            m_Header.RegisterCallback<MouseLeaveEvent>(OnChevronMouseLeave);
        }

        void OnChevronMouseEnter(MouseEnterEvent evt)
        {
            if (m_Chevron == null) return;
            if (m_FoldoutDisabled)
                return;
            SyncChevronRotation();
            m_StatusElement.AddToClassList(k_StatusHiddenClass);
            m_Chevron.RemoveFromClassList(k_ChevronHiddenClass);
        }

        void OnChevronMouseLeave(MouseLeaveEvent evt)
        {
            if (m_Chevron == null) return;
            m_Chevron.AddToClassList(k_ChevronHiddenClass);
            m_StatusElement.RemoveFromClassList(k_StatusHiddenClass);
        }

        void SyncChevronRotation()
        {
            if (m_Chevron == null || !m_ChevronHoverEnabled) return;
            var expanded = IsContentVisible();
            m_Chevron.EnableInClassList(k_IconExpandedClass, expanded);
            m_Chevron.EnableInClassList(k_IconCollapsedClass, !expanded);
        }
    }
}
