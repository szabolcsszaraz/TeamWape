using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Utils.Event;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.UserInteraction;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Events;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class AssistantWindowUiContainer : IToolUiContainer, IDisposable
    {
        readonly AssistantUIContext m_Context;

        TodoProgressInteractionElement m_TodoInteraction;
        UserInteractionEntry m_TodoEntry;
        string m_TodoConversationId;
        Action<bool> m_TodoExpandedChangedHandler;
        readonly Dictionary<Guid, PlanApprovalLifecycle> m_PlanLifecycles = new();
        readonly Dictionary<Guid, Task<string>> m_CompletedBeforeBridge = new();
        bool m_Disposed;

        public event Action PlanApproved;

        // Raised when the user approves Unity.EnterPlanMode; the TextField switches into Plan mode.
        public event Action PlanModeRequested;

        public AssistantWindowUiContainer(AssistantUIContext context)
        {
            m_Context = context;
            TodoUpdateEvent.OnTodoListUpdated += OnTodoListUpdated;
            m_Context.API.ConversationReload += OnConversationReload;
            m_Context.API.APIStateChanged += OnAPIStateChanged;

            // ConversationReload may have already fired before this container was constructed
            // (InitializeState runs before the container is created). Defer a session-state
            // restore to the next editor frame so the full UI is wired up, and so we don't
            // race with the incoming ConversationReload DispatchAndForget callback.
            // Use LastActiveConversationId from session state — ActiveConversationId is set
            // asynchronously inside LoadConversationAsync and is not available yet at this point.
            EditorApplication.delayCall += TryRestoreFromSessionState;
        }

        void TryRestoreFromSessionState()
        {
            if (m_Disposed) return;

            var lastActiveId = AssistantUISessionState.instance.LastActiveConversationId;
            if (string.IsNullOrEmpty(lastActiveId)) return;

            if (m_TodoEntry == null)
            {
                var (items, planPath, expanded) = AssistantUISessionState.instance.GetTodoState(lastActiveId);
                if (items != null && items.Count > 0)
                {
                    m_TodoConversationId = lastActiveId;
                    RestoreTodoPanel(items, planPath, expanded);
                }
            }

            RestorePendingExitPlanModeInteractions(lastActiveId);
        }

        void RestorePendingExitPlanModeInteractions(string conversationId)
        {
            var entries = ExitPlanModeStateStore.instance.GetStatesForConversation(conversationId);
            foreach (var (callId, planPath, planContent, title, expanded) in entries)
            {
                if (m_Context.PendingInlineInteractions.ContainsKey(callId)) continue;
                var interaction = new ExitPlanModeInteraction(planPath, planContent, title);
                var element = new ExitPlanModeInteractionElement(interaction);

                if (expanded) element.MarkRestoreExpanded();

                RegisterExitPlanModeInteraction(callId, element, conversationId);
            }
        }

        void RegisterExitPlanModeInteraction(Guid callId, ExitPlanModeInteractionElement exitPlanMode, string conversationId)
        {
            // Blackboard.ActiveConversationId can be momentarily null during a domain-reload restore;
            // fall back to the tool-side persisted id so the preserveConversationId filter still matches.
            if (string.IsNullOrEmpty(conversationId))
                conversationId = ExitPlanModeStateStore.instance.GetConversationId(callId);

            var lifecycle = new PlanApprovalLifecycle(this, callId, exitPlanMode, conversationId);
            m_Context.PendingInlineInteractions[callId] = exitPlanMode;
            m_PlanLifecycles[callId] = lifecycle;

            AssistantEvents.Send(new EventInlineInteractionPushed(callId, exitPlanMode));
            m_Context.InteractionQueue.EnqueueFront(lifecycle.FooterEntry);
        }

        void RemoveExitPlanModeEntry(Guid callId)
        {
            if (m_PlanLifecycles.TryGetValue(callId, out var lifecycle))
            {
                lifecycle.Dispose();
                m_PlanLifecycles.Remove(callId);
            }
            m_Context.PendingInlineInteractions.Remove(callId);
            ExitPlanModeStateStore.instance.ClearState(callId);
        }

        public void PushElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            if (userInteraction is IApprovalInteraction interaction)
            {
                var entry = EnqueueApproval(new ApprovalRequest
                {
                    Action = interaction.Action,
                    Detail = interaction.Detail,
                    AllowLabel = interaction.AllowLabel,
                    DenyLabel = interaction.DenyLabel,
                    ShowScope = interaction.ShowScope,
                    OnRespond = interaction.Respond,
                    OnCancel = userInteraction.CancelInteraction,
                    // Prefer rich "View" content (e.g. the code to be executed) over the plain-text fallback.
                    ExpandedContentFactory = (userInteraction as PermissionInteraction)?.ExpandedContentFactory
                });

                if (userInteraction is PermissionInteraction pi && pi.TryAutoResolve != null)
                {
                    entry.TryAutoResolve = () =>
                    {
                        var answer = pi.TryAutoResolve();
                        if (!answer.HasValue) return false;
                        pi.Complete(answer.Value);
                        return true;
                    };
                }

                // Remove the entry when the TCS resolves for any reason (user response, timeout,
                // or socket drop). Without this, a cancelled/timed-out TCS leaves the entry in
                // the queue permanently since PopElement is a no-op for approval interactions.
                userInteraction.TaskCompletionSource.Task.ContinueWith(
                    _ => EditorApplication.delayCall += () => m_Context.InteractionQueue.Complete(entry),
                    System.Threading.Tasks.TaskScheduler.Default);

                return;
            }

            if (userInteraction is EnterPlanModeInteraction enterPlanMode)
            {
                // Approval banner asking the user whether to switch into Plan mode.
                var entry = new UserInteractionEntry
                {
                    Title = "<b>" + EnterPlanModeInteraction.Title + "</b>",
                    Detail = EnterPlanModeInteraction.Subtitle,
                    ContentView = new EnterPlanModeFooterContent(enterPlanMode),
                    HideCounter = true,
                    OnCancel = enterPlanMode.CancelInteraction
                };

                m_Context.InteractionQueue.EnqueueFront(entry);

                // Remove the entry once the user responds, or it is cancelled/timed out.
                enterPlanMode.TaskCompletionSource.Task.ContinueWith(
                    _ => EditorApplication.delayCall += () => m_Context.InteractionQueue.Complete(entry),
                    System.Threading.Tasks.TaskScheduler.Default);

                // Switch into Plan mode once the user approves.
                enterPlanMode.OnCompleted += _ =>
                {
                    if (enterPlanMode.Approved)
                        PlanModeRequested?.Invoke();
                };
                return;
            }

            if (userInteraction is ExitPlanModeInteraction exitPlanMode)
            {
                if (m_CompletedBeforeBridge.TryGetValue(callInfo.CallId, out var completedTask))
                {
                    m_CompletedBeforeBridge.Remove(callInfo.CallId);
                    if (completedTask.IsCanceled)
                        exitPlanMode.TaskCompletionSource.TrySetCanceled();
                    else if (completedTask.IsFaulted && completedTask.Exception != null)
                        exitPlanMode.TaskCompletionSource.TrySetException(completedTask.Exception.InnerExceptions);
                    else
                        exitPlanMode.TaskCompletionSource.TrySetResult(completedTask.Result);
                    return;
                }

                if (m_Context.PendingInlineInteractions.TryGetValue(callInfo.CallId, out var existing)
                    && existing is ExitPlanModeInteractionElement existingExitPlanMode)
                {
                    existingExitPlanMode.Interaction.TaskCompletionSource.Task.ContinueWith(t =>
                    {
                        if (t.IsCanceled)
                            exitPlanMode.TaskCompletionSource.TrySetCanceled();
                        else if (t.IsFaulted)
                            exitPlanMode.TaskCompletionSource.TrySetException(t.Exception!.InnerExceptions);
                        else
                            exitPlanMode.TaskCompletionSource.TrySetResult(t.Result);
                    }, TaskContinuationOptions.ExecuteSynchronously);

                    exitPlanMode.TaskCompletionSource.Task.ContinueWith(
                        _ => existingExitPlanMode.Interaction.CancelInteraction(),
                        TaskContinuationOptions.ExecuteSynchronously | TaskContinuationOptions.NotOnRanToCompletion);
                    return;
                }

                var element = new ExitPlanModeInteractionElement(exitPlanMode);
                RegisterExitPlanModeInteraction(callInfo.CallId, element, m_Context.Blackboard.ActiveConversationId.Value);
                return;
            }

            if (userInteraction is AskUserInteraction askUser)
            {
                var askUserElement = new AskUserInteractionElement(askUser);
                var entry = new UserInteractionEntry
                {
                    Title = "Assistant wants to <b>" + askUser.Title + "</b>",
                    ContentView = askUserElement,
                    OnCancel = userInteraction.CancelInteraction,
                    HideMainInput = true
                };

                m_Context.InteractionQueue.EnqueueFront(entry);
                userInteraction.OnCompleted += _ => m_Context.InteractionQueue.Complete(entry);
                return;
            }

            if (userInteraction is VisualElement visualElement)
            {
                EnqueueCustomContent(visualElement, userInteraction);
                return;
            }

            // Bare IInteractionSource with no IApprovalInteraction or VisualElement implementation:
            // fall back to a default Allow/Deny approval so the interaction isn't silently dropped.
            EnqueueApproval(new ApprovalRequest
            {
                ShowScope = false,
                OnRespond = answer =>
                {
                    if (answer == PermissionUserAnswer.DenyOnce || answer == PermissionUserAnswer.DenyAlways)
                        userInteraction.CancelInteraction();
                    else
                        userInteraction.TaskCompletionSource.TrySetResult(default);
                },
                OnCancel = userInteraction.CancelInteraction
            });
        }

        UserInteractionEntry EnqueueApproval(ApprovalRequest request)
        {
            var content = new ApprovalInteractionContent();
            content.SetApprovalData(request.AllowLabel, request.DenyLabel, request.OnRespond, request.ShowScope);

            var action = request.Action;
            var detail = request.Detail;

            var entry = new UserInteractionEntry
            {
                Title = action != null ? "Assistant wants to <b>" + action + "</b>" : null,
                Detail = detail,
                ContentView = content,
                OnCancel = request.OnCancel,
                ExpandedTitle = action,
                ExpandedContentFactory = request.ExpandedContentFactory
                    ?? (() => ApprovalInteractionContent.CreateTextExpandedContent(action, detail))
            };

            m_Context.InteractionQueue.EnqueueFront(entry);
            return entry;
        }

        void EnqueueCustomContent<TOutput>(VisualElement visualElement, IInteractionSource<TOutput> userInteraction)
        {
            var entry = new UserInteractionEntry
            {
                CustomContent = visualElement,
                OnCancel = userInteraction.CancelInteraction
            };

            m_Context.InteractionQueue.Enqueue(entry);
            userInteraction.OnCompleted += _ => m_Context.InteractionQueue.Complete(entry);
        }

        public void PopElement<TOutput>(ToolExecutionContext.CallInfo callInfo, IInteractionSource<TOutput> userInteraction)
        {
            // No-op: the queue auto-advances when an entry is completed or cancelled.
        }

        void CancelPendingInlineInteractions(string preserveConversationId = null)
        {
            if (m_Context.PendingInlineInteractions.Count == 0) return;

            var keysToCancel = new List<Guid>();
            foreach (var kvp in m_Context.PendingInlineInteractions)
            {
                if (preserveConversationId != null
                    && m_PlanLifecycles.TryGetValue(kvp.Key, out var lifecycle)
                    && lifecycle.ConversationId == preserveConversationId)
                    continue;
                keysToCancel.Add(kvp.Key);
            }

            var sourcesToCancel = new List<IInteractionSource<string>>();
            foreach (var key in keysToCancel)
            {
                if (m_Context.PendingInlineInteractions.TryGetValue(key, out var view)
                    && view is ExitPlanModeInteractionElement exitPlanElement)
                    sourcesToCancel.Add(exitPlanElement.Interaction);

                if (m_PlanLifecycles.TryGetValue(key, out var entryLifecycle))
                    m_Context.InteractionQueue.Complete(entryLifecycle.FooterEntry);

                RemoveExitPlanModeEntry(key);
            }

            foreach (var source in sourcesToCancel)
                source.CancelInteraction();
        }

        void OnAPIStateChanged()
        {
            // When the active conversation is cleared (new chat initiated), tear down the todo panel.
            // TearDownTodoPanel preserves the session state so switching back to the old conversation
            // can still restore its todos.
            if (!m_Context.Blackboard.ActiveConversationId.IsValid)
            {
                CancelPendingInlineInteractions();
                m_CompletedBeforeBridge.Clear();
                if (m_TodoEntry != null)
                    TearDownTodoPanel();
            }
        }

        void OnConversationReload(AssistantConversationId conversationId)
        {
            var id = conversationId.Value;

            // Preserve current-conversation entries so the post-reload replay can bridge to them.
            CancelPendingInlineInteractions(preserveConversationId: id);

            if (string.IsNullOrEmpty(id) || m_TodoConversationId == id)
            {
                RestorePendingExitPlanModeInteractions(id);
                return;
            }

            // Switching to a different conversation — tear down current panel (preserve its stored state)
            TearDownTodoPanel();

            // Restore the new conversation's todos if any were previously stored
            var (items, planPath, expanded) = AssistantUISessionState.instance.GetTodoState(id);
            if (items != null && items.Count > 0)
            {
                m_TodoConversationId = id;
                RestoreTodoPanel(items, planPath, expanded);
            }

            RestorePendingExitPlanModeInteractions(id);
        }

        void OnTodoListUpdated(List<TodoItem> items, string planPath, string conversationId)
        {
            if (m_Disposed) return;

            // Preserve the current expanded state so it isn't overwritten when new todo data arrives.
            // For background conversations, read from session state — the live panel's IsExpanded only
            // reflects the active conversation and must not be used for a different conversation.
            var currentExpanded = (m_TodoInteraction != null && conversationId == m_TodoConversationId)
                ? m_TodoInteraction.IsExpanded
                : AssistantUISessionState.instance.GetTodoState(conversationId).expanded;
            AssistantUISessionState.instance.SetTodoState(conversationId, items, planPath, currentExpanded);

            // Only update the live panel when the update belongs to the currently active conversation.
            // Background tool calls for a different conversation must not overwrite the current UI.
            if (conversationId != m_Context.Blackboard.ActiveConversationId.Value)
                return;

            m_TodoConversationId = conversationId;
            RestoreTodoPanel(items, planPath, currentExpanded);
        }

        // Removes the panel from the queue without erasing the conversation's persisted state.
        void TearDownTodoPanel()
        {
            if (m_TodoEntry != null)
                m_Context.InteractionQueue.Complete(m_TodoEntry);

            if (m_TodoInteraction != null)
            {
                m_TodoInteraction.Completed -= ClearTodoPanel;
                if (m_TodoExpandedChangedHandler != null)
                    m_TodoInteraction.ExpandedChanged -= m_TodoExpandedChangedHandler;
            }

            m_TodoInteraction = null;
            m_TodoEntry = null;
            m_TodoConversationId = null;
            m_TodoExpandedChangedHandler = null;
        }

        // Removes the panel and erases the conversation's persisted state (plan completed).
        void ClearTodoPanel()
        {
            if (!string.IsNullOrEmpty(m_TodoConversationId))
                AssistantUISessionState.instance.ClearTodoState(m_TodoConversationId);

            TearDownTodoPanel();
        }

        void RestoreTodoPanel(List<TodoItem> items, string planPath, bool expanded)
        {
            if (m_TodoInteraction == null)
            {
                m_TodoInteraction = new TodoProgressInteractionElement(planPath, expanded);
                m_TodoInteraction.Initialize(m_Context);
                m_TodoInteraction.Completed += ClearTodoPanel;

                // Use m_TodoInteraction.PlanPath instead of capturing the planPath parameter so that
                // the handler always reflects the latest plan path (not the one from the first call).
                m_TodoExpandedChangedHandler = isExpanded =>
                    AssistantUISessionState.instance.SetTodoState(m_TodoConversationId, m_TodoInteraction.CurrentItems, m_TodoInteraction.PlanPath, isExpanded);
                m_TodoInteraction.ExpandedChanged += m_TodoExpandedChangedHandler;

                m_TodoEntry = new UserInteractionEntry
                {
                    ContentView = m_TodoInteraction,
                    HideCounter = true,
                    HideHeader = true,
                    Persistent = true
                };

                m_Context.InteractionQueue.Enqueue(m_TodoEntry);
            }

            m_TodoInteraction.UpdateTodos(items, planPath);
        }

        public void Dispose()
        {
            m_Disposed = true;
            TodoUpdateEvent.OnTodoListUpdated -= OnTodoListUpdated;
            m_Context.API.ConversationReload -= OnConversationReload;
            m_Context.API.APIStateChanged -= OnAPIStateChanged;
            m_Context.InteractionQueue.CancelAll();
            m_CompletedBeforeBridge.Clear();
        }

        ~AssistantWindowUiContainer()
        {
            Dispose();
        }

        sealed class PlanApprovalLifecycle : IDisposable
        {
            readonly AssistantWindowUiContainer m_Owner;
            readonly Guid m_CallId;
            readonly ExitPlanModeInteractionElement m_Element;

            public string ConversationId { get; }
            public UserInteractionEntry FooterEntry { get; }

            public PlanApprovalLifecycle(AssistantWindowUiContainer owner, Guid callId, ExitPlanModeInteractionElement element, string conversationId)
            {
                m_Owner = owner;
                m_CallId = callId;
                ConversationId = conversationId;
                m_Element = element;

                var footerContent = new PlanApprovalFooterContent(element);
                FooterEntry = new UserInteractionEntry
                {
                    Title = "<b>" + element.Title + "</b>",
                    ContentView = footerContent,
                    HideCounter = true,
                    Persistent = true,
                    OnCancel = OnFooterCancel
                };

                m_Element.ExpandedStateChanged += OnExpandedStateChanged;
                m_Element.Completed += OnElementCompleted;
                m_Element.Interaction.OnCompleted += OnInteractionCompleted;
            }

            void OnInteractionCompleted(string result)
            {
                if (!IsApproved(result)) return;
                m_Owner.PlanApproved?.Invoke();
            }

            static bool IsApproved(string result)
            {
                if (string.IsNullOrEmpty(result)) return false;
                try
                {
                    return JObject.Parse(result).Value<bool>("approved");
                }
                catch (JsonReaderException)
                {
                    return false;
                }
            }

            void OnExpandedStateChanged(bool isExpanded) =>
                ExitPlanModeStateStore.instance.SetState(m_CallId, ConversationId, m_Element.PlanPath, m_Element.PlanContent, m_Element.PlanTitle, isExpanded);

            void OnFooterCancel()
            {
                m_Owner.RemoveExitPlanModeEntry(m_CallId);
                m_Element.Interaction.CancelInteraction();
            }

            void OnElementCompleted()
            {
                m_Owner.m_CompletedBeforeBridge[m_CallId] = m_Element.Interaction.TaskCompletionSource.Task;
                m_Owner.RemoveExitPlanModeEntry(m_CallId);
                AssistantEvents.Send(new EventInlineInteractionCompleted(m_CallId));
                m_Owner.m_Context.InteractionQueue.Complete(FooterEntry);
            }

            public void Dispose()
            {
                m_Element.ExpandedStateChanged -= OnExpandedStateChanged;
                m_Element.Completed -= OnElementCompleted;
                m_Element.Interaction.OnCompleted -= OnInteractionCompleted;
                FooterEntry.OnCancel = null;
            }
        }
    }
}
