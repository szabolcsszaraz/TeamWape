using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AskUserInteractionElement : InteractionContentView, INavigableInteractionView
    {
        const string k_OtherPlaceholder = "Other...";
        const string k_PlaceholderClass = "ask-user-field-placeholder";
        const string k_NotesPlaceholder = "Type something else, or add more optional details to your plan";

        readonly AskUserInteraction m_Interaction;
        readonly IReadOnlyList<AskUserQuestion> m_Questions;
        readonly List<AskUserQuestionPane> m_Panes = new();

        int m_CurrentIndex;
        bool m_Completed;
        readonly Dictionary<int, string> m_Answers = new();
        readonly HashSet<int> m_SkippedIndices = new();
        Button m_SubmitButton;
        Button m_ContinueButton;
        Button m_SkipButton;
        TextField m_NotesField;

        public string Title => m_Interaction.Title;

        // INavigableInteractionView
        public int NavigationIndex => m_CurrentIndex;
        public int NavigationCount => m_Questions.Count;

        internal bool IsSkipAvailable => m_SkipButton.style.display.value != DisplayStyle.None;

        public event Action NavigationChanged;

        public AskUserInteractionElement(AskUserInteraction interaction)
        {
            m_Interaction = interaction;
            m_Questions = interaction.Questions;
        }

        protected override void InitializeView(TemplateContainer view)
        {
            var tabContentContainer = view.Q<VisualElement>("askUserTabContent");
            view.SetupButton("askUserCancelButton", _ => OnCancel());
            m_ContinueButton = view.SetupButton("askUserContinueButton", _ => NavigateNext());
            m_SkipButton = view.SetupButton("askUserSkipButton", _ => OnSkip());
            m_SubmitButton = view.SetupButton("askUserSubmitButton", _ => OnSubmit());
            m_SubmitButton.SetEnabled(false);

            RegisterAttachEvents(OnAttach, OnDetach);

            m_NotesField = view.Q<TextField>("askUserNotesField");
            m_NotesField.SetValueWithoutNotify(k_NotesPlaceholder);
            m_NotesField.AddToClassList(k_PlaceholderClass);
            m_NotesField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (m_NotesField.ClassListContains(k_PlaceholderClass))
                {
                    m_NotesField.value = "";
                    m_NotesField.RemoveFromClassList(k_PlaceholderClass);
                }
            });
            m_NotesField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrWhiteSpace(m_NotesField.value))
                {
                    m_NotesField.SetValueWithoutNotify(k_NotesPlaceholder);
                    m_NotesField.AddToClassList(k_PlaceholderClass);
                }
            });

            for (var i = 0; i < m_Questions.Count; i++)
            {
                var pane = new AskUserQuestionPane();
                pane.Initialize(Context, autoShowControl: false);
                pane.SetActive(i == 0);
                pane.SetQuestion(m_Questions[i].Question);

                BuildQuestionContent(pane.ContentSlot, i, m_Questions[i]);
                tabContentContainer.Add(pane);
                m_Panes.Add(pane);
            }

            m_CurrentIndex = 0;
            RefreshContinueButton();
        }

        public void NavigatePrev() => NavigateTo(m_CurrentIndex - 1);
        public void NavigateNext() => NavigateTo(m_CurrentIndex + 1);

        void NavigateTo(int index)
        {
            if (index < 0 || index >= m_Questions.Count)
                return;

            m_Panes[m_CurrentIndex].SetActive(false);
            m_CurrentIndex = index;
            m_Panes[m_CurrentIndex].SetActive(true);
            RefreshContinueButton();
            RefreshSkipButton();
            NavigationChanged?.Invoke();
        }

        void RefreshContinueButton()
        {
            var q = m_Questions[m_CurrentIndex];
            var type = string.IsNullOrEmpty(q.Type) ? "choice" : q.Type.ToLowerInvariant();
            var show = (q.MultiSelect || type == "text")
                       && m_CurrentIndex < m_Questions.Count - 1;
            m_ContinueButton?.SetDisplay(show);
        }

        void RefreshSkipButton()
        {
            var isLastQuestion = m_CurrentIndex == m_Questions.Count - 1;
            var currentIsSkipped = m_SkippedIndices.Contains(m_CurrentIndex);
            m_SkipButton.SetDisplay(!(isLastQuestion && currentIsSkipped));
        }

        void SetAnswer(int questionIndex, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                m_Answers.Remove(questionIndex);
            else
                m_Answers[questionIndex] = value;

            m_SkippedIndices.Remove(questionIndex);
            m_Panes[questionIndex].SetSkipped(false);
            UpdateSubmitState();
            RefreshSkipButton();
        }

        void UpdateSubmitState()
        {
            for (var i = 0; i < m_Questions.Count; i++)
            {
                var answered = m_Answers.TryGetValue(i, out var answer) && !string.IsNullOrWhiteSpace(answer);
                if (!answered && !m_SkippedIndices.Contains(i))
                {
                    m_SubmitButton.SetEnabled(false);
                    return;
                }
            }

            m_SubmitButton.SetEnabled(true);
        }

        void BuildQuestionContent(VisualElement contentSlot, int questionIndex, AskUserQuestion question)
        {
            var type = string.IsNullOrEmpty(question.Type) ? "choice" : question.Type.ToLowerInvariant();

            switch (type)
            {
                case "text":
                    BuildTextQuestion(contentSlot, questionIndex, question.Placeholder);
                    break;
                case "yesno":
                    BuildYesNoQuestion(contentSlot, questionIndex);
                    break;
                default:
                    if (question.MultiSelect)
                        BuildMultiSelectQuestion(contentSlot, questionIndex, question.Options ?? new List<AskUserOption>(), question.Placeholder);
                    else
                        BuildSingleSelectQuestion(contentSlot, questionIndex, question.Options ?? new List<AskUserOption>(), question.Placeholder);
                    break;
            }
        }

        void BuildMultiSelectQuestion(VisualElement contentSlot, int questionIndex, List<AskUserOption> options, string placeholder = null)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var selected = new HashSet<string>();
            var otherPlaceholder = placeholder ?? k_OtherPlaceholder;

            var choicesContainer = new VisualElement();
            foreach (var option in options)
            {
                var toggle = new Toggle();
                toggle.AddToClassList("ask-user-radio-indicator");
                if (!string.IsNullOrEmpty(option.Description))
                    toggle.tooltip = option.Description;

                toggle.RegisterValueChangedCallback(evt =>
                {
                    if (evt.newValue) selected.Add(option.Label);
                    else selected.Remove(option.Label);
                    SetAnswer(questionIndex, string.Join(", ", selected));
                });
                choicesContainer.Add(BuildOptionRow(toggle, option.Label, option.Description));
            }

            // "Other" row
            var otherToggle = new Toggle();
            otherToggle.AddToClassList("ask-user-radio-indicator");
            var otherRoot = new AskUserOptionRow();
            otherRoot.Initialize(Context);
            var other = new AskUserOtherRow(otherRoot, otherToggle, otherPlaceholder);

            other.Field.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
            other.Field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (other.IsPlaceholder) other.ClearPlaceholder();
                otherToggle.SetValueWithoutNotify(true);
                other.SaveButton.SetDisplay(true);
                other.ResetSaveButton();
            });
            other.SaveButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());

            otherToggle.RegisterValueChangedCallback(evt =>
            {
                if (!evt.newValue)
                {
                    selected.RemoveWhere(s => s.StartsWith("Other: "));
                    other.SaveButton.SetDisplay(false);
                    SetAnswer(questionIndex, string.Join(", ", selected));
                }
                else
                {
                    other.SaveButton.SetDisplay(true);
                }
            });

            other.SaveButton.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (other.IsPlaceholder || string.IsNullOrWhiteSpace(other.Field.value)) return;
                selected.RemoveWhere(s => s.StartsWith("Other: "));
                selected.Add("Other: " + other.Field.value);
                SetAnswer(questionIndex, string.Join(", ", selected));
                other.MarkSaved();
                if (!isLastQuestion) NavigateNext();
            });

            choicesContainer.Add(other.Root);
            contentSlot.Add(choicesContainer);
        }

        void BuildSingleSelectQuestion(VisualElement contentSlot, int questionIndex, List<AskUserOption> options, string placeholder = null)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var indicators = new List<RadioButton>();
            var otherPlaceholder = placeholder ?? k_OtherPlaceholder;

            void SelectIndicator(int idx)
            {
                for (var k = 0; k < indicators.Count; k++)
                    indicators[k].SetValueWithoutNotify(k == idx);
            }

            var optionsContainer = new VisualElement();
            for (var i = 0; i < options.Count; i++)
            {
                var opt = options[i];
                var idx = i;

                var indicator = new RadioButton();
                indicator.AddToClassList("ask-user-radio-indicator");
                indicator.pickingMode = PickingMode.Position;
                indicators.Add(indicator);

                AddSelectableRow(optionsContainer, indicator, opt.Label, opt.Description, () =>
                {
                    SelectIndicator(idx);
                    SetAnswer(questionIndex, opt.Label);
                    if (!isLastQuestion) NavigateNext();
                });
            }

            // "Other" row
            var otherIndicator = new RadioButton();
            otherIndicator.AddToClassList("ask-user-radio-indicator");
            otherIndicator.pickingMode = PickingMode.Position;
            indicators.Add(otherIndicator);
            var otherRoot = new AskUserOptionRow();
            otherRoot.Initialize(Context);
            var other = new AskUserOtherRow(otherRoot, otherIndicator, otherPlaceholder);

            other.Field.RegisterCallback<FocusInEvent>(_ =>
            {
                if (other.IsPlaceholder) other.ClearPlaceholder();
                SelectIndicator(options.Count);
                m_Answers.Remove(questionIndex);
                other.SaveButton.SetDisplay(true);
                other.ResetSaveButton();
                UpdateSubmitState();
            });

            WireSelectableRow(other.Root, otherIndicator, () =>
            {
                SelectIndicator(options.Count);
                m_Answers.Remove(questionIndex);
                other.SaveButton.SetDisplay(true);
                UpdateSubmitState();
                other.Field.Focus();
            });

            other.SaveButton.RegisterCallback<PointerUpEvent>(_ =>
            {
                if (other.IsPlaceholder || string.IsNullOrWhiteSpace(other.Field.value)) return;
                SetAnswer(questionIndex, other.Field.value);
                other.MarkSaved();
                if (!isLastQuestion) NavigateNext();
            });

            optionsContainer.Add(other.Root);
            contentSlot.Add(optionsContainer);
        }

        void BuildTextQuestion(VisualElement contentSlot, int questionIndex, string placeholder)
        {
            var textField = new TextField { multiline = true, verticalScrollerVisibility = ScrollerVisibility.Auto };
            textField.AddToClassList("ask-user-text-field");

            if (!string.IsNullOrEmpty(placeholder))
            {
                textField.SetValueWithoutNotify(placeholder);
                textField.AddToClassList(k_PlaceholderClass);
                textField.RegisterCallback<FocusInEvent>(_ =>
                {
                    if (textField.ClassListContains(k_PlaceholderClass))
                    {
                        textField.value = "";
                        textField.RemoveFromClassList(k_PlaceholderClass);
                    }
                });
            }

            textField.RegisterValueChangedCallback(evt => SetAnswer(questionIndex, evt.newValue));
            contentSlot.Add(textField);
        }

        void BuildYesNoQuestion(VisualElement contentSlot, int questionIndex)
        {
            var isLastQuestion = questionIndex == m_Questions.Count - 1;
            var optionsContainer = new VisualElement();
            var indicators = new List<RadioButton>();

            void SelectIndicator(int idx)
            {
                for (var k = 0; k < indicators.Count; k++)
                    indicators[k].SetValueWithoutNotify(k == idx);
            }

            foreach (var label in new[] { "Yes", "No" })
            {
                var idx = indicators.Count;
                var indicator = new RadioButton();
                indicator.AddToClassList("ask-user-radio-indicator");
                indicator.pickingMode = PickingMode.Position;
                indicators.Add(indicator);

                AddSelectableRow(optionsContainer, indicator, label, null, () =>
                {
                    SelectIndicator(idx);
                    SetAnswer(questionIndex, label);
                    if (!isLastQuestion) NavigateNext();
                });
            }

            contentSlot.Add(optionsContainer);
        }

        AskUserOptionRow BuildOptionRow(VisualElement indicator, string label, string description = null)
        {
            var row = new AskUserOptionRow();
            row.Initialize(Context);
            row.SetData(indicator, label, description);
            return row;
        }

        void AddSelectableRow(VisualElement container, RadioButton indicator, string label, string description, Action onSelect)
        {
            var row = BuildOptionRow(indicator, label, description);
            WireSelectableRow(row, indicator, onSelect);
            container.Add(row);
        }

        void WireSelectableRow(VisualElement row, RadioButton indicator, Action onSelect)
        {
            indicator.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation(), TrickleDown.TrickleDown);
            row.RegisterCallback<PointerUpEvent>(evt =>
            {
                evt.StopPropagation();
                indicator.ReleasePointer(evt.pointerId);
                onSelect();
            }, TrickleDown.TrickleDown);
        }

        internal void OnSkip()
        {
            m_Answers.Remove(m_CurrentIndex);
            m_SkippedIndices.Add(m_CurrentIndex);
            ResetCurrentPane();
            m_Panes[m_CurrentIndex].SetSkipped(true);
            UpdateSubmitState();
            if (m_CurrentIndex < m_Questions.Count - 1)
            {
                NavigateNext();
            }
            else
            {
                RefreshSkipButton();
            }
        }

        void ResetCurrentPane()
        {
            var pane = m_Panes[m_CurrentIndex];
            pane.ContentSlot.Clear();
            BuildQuestionContent(pane.ContentSlot, m_CurrentIndex, m_Questions[m_CurrentIndex]);
        }

        internal void OnSubmit()
        {
            if (m_Completed) return;
            m_Completed = true;
            AIAssistantAnalytics.ReportUITriggerLocalClarifyingQuestionSubmittedEvent(Context.Blackboard.ActiveConversationId, m_Questions.Count);
            var notes = !m_NotesField.ClassListContains(k_PlaceholderClass)
                        && !string.IsNullOrWhiteSpace(m_NotesField.value)
                ? m_NotesField.value
                : null;
            m_Interaction.Complete(m_Answers, m_SkippedIndices, notes);
            InvokeCompleted();
        }

        void OnCancel()
        {
            if (m_Completed) return;
            m_Completed = true;
            AIAssistantAnalytics.ReportUITriggerLocalClarifyingQuestionCancelledEvent(Context.Blackboard.ActiveConversationId, m_Questions.Count);
            m_Interaction.CompleteCancelled();
            InvokeCompleted();
        }

        void OnAttach(AttachToPanelEvent evt)
        {
            m_Interaction.CancelRequested += OnInteractionCancelled;
        }

        void OnDetach(DetachFromPanelEvent evt)
        {
            m_Interaction.CancelRequested -= OnInteractionCancelled;
        }

        void OnInteractionCancelled()
        {
            if (m_Completed) return;
            m_Completed = true;
            InvokeCompleted();
        }
    }
}
