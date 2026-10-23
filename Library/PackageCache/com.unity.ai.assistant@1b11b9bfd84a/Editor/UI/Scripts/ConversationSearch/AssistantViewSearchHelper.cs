using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.ConversationSearch
{
    class AssistantViewSearchHelper : Manipulator
    {
        #region Constants

        const string k_NoResultsText = "No results";
        const string k_ResultsText = "{0}/{1}";
        const string k_SearchPlaceholderText = "Find in conversation";

        const string k_PreviousButtonTooltip = "Previous result";
        const string k_NextButtonTooltip = "Next result";
        const string k_CloseButtonTooltip = "Close search";

        const string k_SearchContainerName = "searchRow";
        const string k_SearchResultCountName = "searchResultCount";
        const string k_SearchNextButtonName = "searchNextButton";
        const string k_SearchPreviousButtonName = "searchPreviousButton";
        const string k_OpenSearchButtonName = "openSearchButton";

        const int k_LabelRectPadding = 20;

        // Negative is what the highlighter reads as "the main occurrence is already behind us", which it
        // never is when the pass starts there: nothing is marked as the main highlight, and it scrolls
        // nowhere.
        const int k_NoVisibleMainResult = -1;

        // How many times a jump asks again for the block it built to be on screen. The pass that
        // positions it is a tick or two away; a block still missing after this many is one no pass is
        // going to bring up, and the reader is left where they are rather than kept waiting.
        const int k_JumpAttempts = 8;

        const string k_SearchOpenClass = "mui-search-open";

        #endregion

        #region UI Elements

        ToolbarSearchField m_SearchField;
        TextField m_SearchTextField;

        Label m_SearchResultCountLabel;

        Button m_PreviousButton;
        Button m_NextButton;
        Button m_SearchButton;
        VisualElement m_SearchContainer;

        readonly IChatMessageView m_MessageView;

        #endregion

        readonly AssistantUIContext Context;

        int m_CurrentSearchResultIndex;
        int TotalResultCount => m_ConversationSearcher.TotalResultCount;

        internal Action<AssistantMessageId> SearchResultHighlighted;

        bool m_ScrollToMainSearchResult;

        /// <summary>
        /// Whether the jump the reader is on had to build its block, which anchors that block at the top
        /// of the viewport. A jump that did must not also be refined by <see cref="ScrollLabelIntoView"/>:
        /// the anchor moves the moment the jump asks for it and the labels are laid out at their new
        /// positions a pass later, while the highlight runs off the editor's own tick and falls between
        /// the two. Measured against the layout the move invalidated, the refinement carries the view off
        /// the block the jump landed on, and every pass after it finds nothing on screen to mark.
        /// </summary>
        bool m_JumpAnchoredTheBlock;

        /// <summary>What is left of <see cref="k_JumpAttempts"/> for the jump the reader is on. Spent
        /// down while its match is still nowhere on screen, and closed the moment a pass finds it.</summary>
        int m_JumpAttemptsLeft;

        /// <summary>Whether the pass that just ran had to open something to bring the reader's match on
        /// screen, which the scroll onto it has to be measured after rather than before.</summary>
        bool m_JumpRevealedTheMatch;

        /// <summary>Whether the reader has taken the view somewhere of their own since the jump began. A
        /// pass the jump queued before that may still paint what it finds on screen, so a highlight does
        /// not blink out from under a scroll, but the view is the reader's now.</summary>
        bool m_ReaderMovedTheView;

        bool m_NeedToFindInitialSearchResult;

        SearchHighlighter m_SearchHighlighter;
        AssistantSearchMessageConverter m_MessageConverter;
        ConversationSearcher m_ConversationSearcher;

        internal AssistantViewSearchHelper(
            IChatMessageView messageView,
            AssistantUIContext context)
        {
            Context = context;
            m_MessageView = messageView;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_MessageConverter = new AssistantSearchMessageConverter();
            m_SearchHighlighter = new SearchHighlighter(ScrollLabelIntoView, m_MessageConverter.GetRenderedMessage);
            m_ConversationSearcher = new ConversationSearcher(m_MessageConverter, Context);

            m_SearchContainer = target.Q<VisualElement>(k_SearchContainerName);

            m_SearchField = new ToolbarSearchField();
            m_SearchField.AddToClassList("mui-conversation-search-bar");
            m_SearchField.RegisterValueChangedCallback(SearchStringChanged);
            m_SearchField.placeholderText = k_SearchPlaceholderText;
            m_SearchField.RegisterCallback<KeyUpEvent>(OnSearchKeyUp);

            // Find textField inside ToolbarSearchField:
            m_SearchTextField = m_SearchField.Q<TextField>();
            m_SearchTextField.selectAllOnFocus = false;

            m_SearchContainer.Insert(0, m_SearchField);

            target.RegisterCallback<AttachToPanelEvent>(TargetAttached);
            target.RegisterCallback<DetachFromPanelEvent>(TargetDetached);

            m_SearchResultCountLabel = target.Q<Label>(k_SearchResultCountName);

            m_PreviousButton = target.SetupButton(k_SearchPreviousButtonName, PreviousResult);
            m_PreviousButton.tooltip = k_PreviousButtonTooltip;

            m_NextButton = target.SetupButton(k_SearchNextButtonName, NextResult);
            m_NextButton.tooltip = k_NextButtonTooltip;

            m_SearchButton = target.SetupButton(k_OpenSearchButtonName, _ => SearchButtonPressed());
            m_SearchButton.AddSessionAndCompatibilityStatusManipulators(Context.API.Provider, enableOnProviderError: true);

            Context.Blackboard.ActiveConversationChanged += OnActiveConversationChanged;
            m_MessageView.UserScrolled += OnUserScrolled;

            HideSearchBar();
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            m_SearchField.UnregisterValueChangedCallback(SearchStringChanged);

            Context.Blackboard.ActiveConversationChanged -= OnActiveConversationChanged;
            m_MessageView.UserScrolled -= OnUserScrolled;
        }

        void TargetDetached(DetachFromPanelEvent evt)
        {
            target.panel.visualTree.UnregisterCallback<KeyDownEvent>(OnKeyDown);
        }

        void TargetAttached(AttachToPanelEvent evt)
        {
            target.panel.visualTree.RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        void OnActiveConversationChanged(AssistantConversationId previousConversationId,
            AssistantConversationId currentConversationId)
        {
            ClearForNewConversation();
        }

        void ClearForNewConversation()
        {
            m_SearchHighlighter.ClearHighlightableElements();
            m_MessageConverter.Clear();

            SearchResultHighlighted = null;
            m_CurrentSearchResultIndex = 0;
            EndJump();
        }

        /// <summary>Starts a jump onto the match the reader has just moved to, which the passes that
        /// follow carry out however far the conversation has to travel to reach it.</summary>
        void BeginJump()
        {
            m_ScrollToMainSearchResult = true;
            m_JumpAnchoredTheBlock = false;
            m_JumpAttemptsLeft = k_JumpAttempts;
            m_ReaderMovedTheView = false;
        }

        /// <summary>Closes the jump the reader was on: nothing left to build, to scroll to or to ask for
        /// again.</summary>
        void EndJump()
        {
            m_ScrollToMainSearchResult = false;
            m_JumpAnchoredTheBlock = false;
            m_JumpAttemptsLeft = 0;
        }

        void RefreshUI(bool highlight = true)
        {
            var showNextPreviousButtons= m_ConversationSearcher.TotalResultCount > 0;
            m_PreviousButton.SetDisplay(showNextPreviousButtons);
            m_NextButton.SetDisplay(showNextPreviousButtons);

            if (m_ConversationSearcher.TotalResultCount > 0)
            {
                m_SearchResultCountLabel.SetDisplay(true);
                m_SearchResultCountLabel.text =
                    string.Format(k_ResultsText, m_CurrentSearchResultIndex + 1, TotalResultCount);
            }
            else if (string.IsNullOrEmpty(m_ConversationSearcher.SearchString))
            {
                m_SearchResultCountLabel.SetDisplay(false);
            }
            else
            {
                m_SearchResultCountLabel.SetDisplay(true);
                m_SearchResultCountLabel.text = k_NoResultsText;
            }

            if (highlight)
            {
                HighlightSearchResults();
            }
        }

        void HighlightSearchResults()
        {
            ConversationSearchResult mainSearchResult = null;

            if (m_ConversationSearcher.TotalResultCount > 0)
            {
                if (m_NeedToFindInitialSearchResult)
                {
                    // Find search result closest to the index closest to the top of the view:
                    var messageIndexInView =
                        m_MessageView.GetFirstItemInView();

                    var closestDistance = int.MaxValue;
                    var resultCounter = 0;

                    foreach (var searchResult in m_ConversationSearcher.SearchResults)
                    {
                        var distance = Math.Abs(searchResult.MessageIndex - messageIndexInView);
                        if (distance < closestDistance)
                        {
                            closestDistance = distance;
                            m_CurrentSearchResultIndex = resultCounter;

                            if (distance == 0)
                                break;
                        }

                        resultCounter += searchResult.MatchCount;
                    }

                    // We changed the current search result index, so refresh that part of the UI:
                    RefreshUI(false);
                }

                m_NeedToFindInitialSearchResult = false;

                mainSearchResult = ResultHolding(m_ConversationSearcher.SearchResults, m_CurrentSearchResultIndex);

                if (m_ScrollToMainSearchResult && mainSearchResult != null)
                {
                    // Asked before the block is built, and kept for the rest of the jump: the search that
                    // building it re-runs comes straight back through here, where the answer is no.
                    m_JumpAnchoredTheBlock |= !IsPopulated(mainSearchResult);

                    // Before anything is counted: building the block puts its labels among the roots the
                    // highlight walks, and the scroll it performs decides which other blocks are still
                    // there to be walked.
                    m_MessageView.PopulateBlock(mainSearchResult.MessageIndex, mainSearchResult.BlockIndex);
                    SearchResultHighlighted?.Invoke(mainSearchResult.MessageId);
                }
            }

            // The pass runs a frame from now, by which time another jump may have moved these on.
            var scrollToMain = m_ScrollToMainSearchResult && !m_JumpAnchoredTheBlock;
            var resultIndex = m_CurrentSearchResultIndex;

            EditorTask.delayCall += () =>
            {
                var visibleIndex = VisibleIndexOf(m_ConversationSearcher.SearchResults, mainSearchResult, resultIndex, IsPopulated);

                m_JumpRevealedTheMatch = false;

                // The reader may have taken the view somewhere of their own since this pass was queued.
                m_SearchHighlighter.Highlight(m_MessageView.HighlightRoots,
                    m_ConversationSearcher.SearchString,
                    scrollToMain && !m_ReaderMovedTheView,
                    visibleIndex);

                // The match was in a part of its element the reader had closed, which is open now and
                // laid out a pass from now: what is left of the jump is spent measuring the move it made.
                if (m_JumpRevealedTheMatch && m_JumpAttemptsLeft > 0)
                {
                    m_JumpAttemptsLeft--;
                    EditorTask.delayCall += HighlightSearchResults;
                    return;
                }

                if (visibleIndex != k_NoVisibleMainResult)
                {
                    m_JumpAttemptsLeft = 0;
                    return;
                }

                // Nothing on screen is the match the reader is on, which a jump that has just built its
                // block is a pass away from — and a delayed call is not a pass.
                if (mainSearchResult == null || m_JumpAttemptsLeft <= 0)
                    return;

                m_JumpAttemptsLeft--;
                EditorTask.delayCall += HighlightSearchResults;
            };
        }

        bool IsPopulated(ConversationSearchResult result)
            => m_MessageView.IsBlockPopulated(result.MessageIndex, result.BlockIndex);

        /// <summary>The result the <paramref name="resultIndex"/>-th match of the conversation is in.</summary>
        internal static ConversationSearchResult ResultHolding(
            IReadOnlyList<ConversationSearchResult> results,
            int resultIndex)
        {
            ConversationSearchResult holder = null;

            foreach (var result in results)
            {
                holder = result;
                resultIndex -= result.MatchCount;

                if (resultIndex < 0)
                    break;
            }

            return holder;
        }

        /// <summary>
        /// Where the main highlight falls among the occurrences the highlight pass can find, which is
        /// the reader's own index less the matches of every earlier result whose block is not built:
        /// those contribute no label for the pass to count. Answers <see cref="k_NoVisibleMainResult"/>
        /// when the main result itself is not built or <paramref name="results"/> no longer holds it, so
        /// that no occurrence is marked as the main one rather than an arbitrary later occurrence taking
        /// its marker.
        /// </summary>
        internal static int VisibleIndexOf(
            IReadOnlyList<ConversationSearchResult> results,
            ConversationSearchResult mainResult,
            int resultIndex,
            Func<ConversationSearchResult, bool> isPopulated)
        {
            // By value, not by reference: building the target block re-runs the search, which replaces
            // every result with an equal instance of its own.
            var main = IndexOfSameMatch(results, mainResult);

            if (main < 0 || !isPopulated(results[main]))
                return k_NoVisibleMainResult;

            for (var index = 0; index < main; index++)
            {
                if (!isPopulated(results[index]))
                    resultIndex -= results[index].MatchCount;
            }

            return resultIndex;
        }

        /// <summary>Where <paramref name="match"/> sits in <paramref name="results"/>, or -1 when the
        /// list names no such match.</summary>
        static int IndexOfSameMatch(IReadOnlyList<ConversationSearchResult> results, ConversationSearchResult match)
        {
            if (match == null)
                return -1;

            for (var index = 0; index < results.Count; index++)
            {
                if (results[index].IsSameMatch(match))
                    return index;
            }

            return -1;
        }

        void ScrollLabelIntoView(TextElement label)
        {
            // A label the element drawing it keeps closed has no place on screen to be scrolled to, and
            // opening what hides it is what the reader is asking for by moving onto the match.
            if (Reveal(label))
            {
                m_JumpRevealedTheMatch = true;
                return;
            }

            var labelRect = SearchHighlighter.GetHighlightedLineNumberRect(label);

            var mainViewRect = m_MessageView.AsVisualElement.worldBound.center;

            // If the label is already in view, do not move:
            var mainViewRectCheck = m_MessageView.AsVisualElement.worldBound;
            mainViewRectCheck.yMin += k_LabelRectPadding;
            mainViewRectCheck.yMax -= k_LabelRectPadding;

            if (mainViewRectCheck.Overlaps(labelRect))
                return;

            // Bring main highlight into centre of view:
            var scrollTarget = labelRect.center.y - mainViewRect.y;

            // Avoid micro-movements:
            if (Mathf.Abs(scrollTarget) > 1)
            {
                m_MessageView.ScrollDownBy(scrollTarget);
            }
        }

        /// <summary>
        /// Has whatever is keeping <paramref name="label"/> off screen show it, asked of each element the
        /// label is drawn inside from the nearest one out. Each of them answers for its own content, so
        /// the walk ends on the one that was hiding this label — and a label nothing is hiding leaves
        /// every one of them untouched. Answers whether anything opened.
        /// </summary>
        static bool Reveal(VisualElement label)
        {
            for (var ancestor = label.parent; ancestor != null; ancestor = ancestor.parent)
            {
                if (ancestor is ISearchRevealable revealable && revealable.RevealForSearch(label))
                    return true;
            }

            return false;
        }

        void DoSearch()
        {
            if (!m_ConversationSearcher.SearchActiveConversation())
            {
                RefreshUI();
                return;
            }

            if (m_ScrollToMainSearchResult)
            {
                m_CurrentSearchResultIndex =
                    Math.Clamp(m_CurrentSearchResultIndex, 0, Math.Max(0, TotalResultCount - 1));
            }

            RefreshUI();

            // Avoid auto-scrolling when new UI is created:
            m_ScrollToMainSearchResult = false;
        }

        void SearchButtonPressed()
        {
            if (m_SearchContainer.style.display == DisplayStyle.None)
            {
                ShowSearchBar();
            }
            else
            {
                HideSearchBar();
            }
        }

        void ShowSearchBar()
        {
            RefreshUI();
            m_SearchContainer.SetDisplay(true);
            m_SearchTextField.selectAllOnFocus = true;
            m_SearchField.Focus();

            m_SearchButton.tooltip = k_CloseButtonTooltip;
            m_SearchButton.EnableInClassList(k_SearchOpenClass, true);
        }

        internal void HideSearchBar()
        {
            m_SearchContainer.SetDisplay(false);

            m_SearchField.value = string.Empty;

            m_ConversationSearcher.Clear();
            m_SearchHighlighter.ClearAllHighlights();

            m_SearchButton.tooltip = k_SearchPlaceholderText;
            m_SearchButton.EnableInClassList(k_SearchOpenClass, false);
        }

        void RequestSearchNextFrame()
        {
            EditorTask.delayCall -= DoSearch;
            EditorTask.delayCall += DoSearch;
        }

        #region UI Element registration methods

        /// <summary>
        /// Registers additional text that should be considered when searching for a specific message.
        /// Used when a message has additional text in the UI that is not part of the message model's content.
        /// </summary>
        internal void RegisterAdditionalMessageText(
            AssistantMessageId messageId,
            VisualElement element,
            string additionalText)
        {
            m_MessageConverter.RegisterAdditionalMessageText(messageId, element, additionalText);

            RequestSearchNextFrame();
        }

        /// <summary>
        /// Remove entries registered with RegisterAdditionalMessageText.
        /// </summary>
        internal void UnregisterAdditionalMessageText(
            AssistantMessageId messageId,
            VisualElement element)
        {
            m_MessageConverter.UnregisterAdditionalMessageText(messageId, element);
            RequestSearchNextFrame();
        }

        /// <param name="indexedBySearch">Whether the searcher reads the text this element draws, which
        /// only the element registering it knows. False leaves its occurrences marked but out of the
        /// count the reader is walked along.</param>
        internal void RegisterSearchableTextElement(TextElement textElement, bool indexedBySearch = true)
        {
            m_SearchHighlighter.RegisterHighlightableTextElement(textElement, indexedBySearch);
            RequestSearchNextFrame();
        }

        internal void UnregisterSearchableTextElement(TextElement textElement)
        {
            m_SearchHighlighter.UnregisterHighlightableTextElement(textElement);
        }

        #endregion

        #region Event Handlers

        /// <summary>The reader has scrolled the conversation themselves, which ends whatever jump was
        /// still carrying it: the match they have just moved off is not one to be brought back to.</summary>
        void OnUserScrolled()
        {
            m_ReaderMovedTheView = true;
            EndJump();
        }

        void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.ctrlKey && evt.keyCode == KeyCode.F)
            {
                ShowSearchBar();
            }
        }

        void OnSearchKeyUp(KeyUpEvent evt)
        {
            if (evt.keyCode == KeyCode.Return)
            {
                NextResult(null);
                evt.StopImmediatePropagation();
                m_SearchTextField.selectAllOnFocus = false;
                m_SearchField.Focus();
            }
        }

        void PreviousResult(PointerUpEvent evt)
        {
            if (TotalResultCount > 0)
            {
                m_CurrentSearchResultIndex -= 1;
                if (m_CurrentSearchResultIndex < 0)
                    m_CurrentSearchResultIndex += TotalResultCount;
            }

            BeginJump();
            RefreshUI();
        }

        void NextResult(PointerUpEvent evt)
        {
            if (TotalResultCount > 0)
            {
                m_CurrentSearchResultIndex = (m_CurrentSearchResultIndex + 1) % TotalResultCount;
            }

            BeginJump();
            RefreshUI();
        }

        void SearchStringChanged(ChangeEvent<string> evt)
        {
            m_ConversationSearcher.SearchString = evt.newValue;

            BeginJump();
            m_NeedToFindInitialSearchResult = true;

            DoSearch();
        }

        #endregion
    }
}
