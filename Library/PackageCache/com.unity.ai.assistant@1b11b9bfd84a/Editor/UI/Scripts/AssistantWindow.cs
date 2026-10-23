using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Backend.Socket;
using Unity.AI.Assistant.Editor.Config;
using Unity.AI.Assistant.Editor.Context;
using Unity.AI.Assistant.Editor.GraphGeneration;
using Unity.AI.Assistant.Editor.Connection;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.UI.Editor.Scripts.Components;
using Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEditor.Overlays;
using UnityEngine;
using TaskUtils = Unity.AI.Assistant.Editor.Utils.TaskUtils;

namespace Unity.AI.Assistant.UI.Editor.Scripts
{
    class AssistantWindow : EditorWindow, IAssistantHostWindow, IHasCustomMenu
    {
        const string k_WindowName = "Assistant";

        static Vector2 s_MinSize = new(400, 400);

        internal IAssistantProvider AssistantInstance => m_AssistantInstance;
        Assistant.Editor.Assistant m_AssistantInstance;
        ConnectionSupervisor m_ConnectionSupervisor;

        internal AssistantUIContext m_Context;
        internal AssistantView m_View;
        AssistantWindowUiContainer m_AssistantWindowUiContainer;

        static IAssistantBackend s_InternalBackendOverride = null;
        bool m_IsStaleDuplicate;

        public Action FocusLost { get; set; }

        /// <summary>
        /// Finds an existing AssistantWindow instance without creating a new one.
        /// Unlike GetWindow, this will not create a new window if none exists.
        /// Use this when you need a reference to the window but must not trigger creation
        /// (e.g. during EditorWindow lifecycle callbacks like CreateGUI).
        /// </summary>
        /// <returns>The existing AssistantWindow instance, or null if none exists.</returns>
        internal static AssistantWindow FindExistingWindow()
        {
            return Resources.FindObjectsOfTypeAll<AssistantWindow>()
                .FirstOrDefault(w => w.m_AssistantInstance != null);
        }

        [MenuItem("Window/AI/Assistant")]
        public static AssistantWindow ShowWindow()
        {
            var editor = GetWindow<AssistantWindow>();

            editor.Show();
            editor.minSize = s_MinSize;

            return editor;
        }

        void OnEnable()
        {
            // Enforce singleton: when Unity restores the dock layout after unmaximizing a
            // window, it deserializes all docked windows — including hidden tabs — and calls
            // OnEnable on each. If another AssistantWindow already exists and is initialized,
            // this instance is a stale duplicate from the layout and must close itself.
            var existingWindows = Resources.FindObjectsOfTypeAll<AssistantWindow>();
            foreach (var existing in existingWindows)
            {
                if (existing != this && existing.m_AssistantInstance != null)
                {
                    m_IsStaleDuplicate = true;
                    EditorApplication.delayCall += Close;
                    return;
                }
            }

            AIAssistantAnalytics.ReportUITriggerLocalWindowOpenedEvent();
        }

        void OnDisable()
        {
            if (m_IsStaleDuplicate)
                return;

            AIAssistantAnalytics.ReportUITriggerLocalWindowClosedEvent(
                m_Context?.Blackboard?.ActiveConversationId ?? default,
                m_Context?.Blackboard?.IsAPIWorking ?? false);
        }

        void CreateGUI()
        {
            if (m_IsStaleDuplicate)
                return;

            var iconPath =
                EditorGUIUtility.isProSkin
                    ? "Sparkle.png"
                    : "Sparkle_dark.png";

            var path = Path.Combine(AssistantUIConstants.BasePath, AssistantUIConstants.UIEditorPath,
                AssistantUIConstants.AssetFolder, "icons", iconPath);

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            titleContent = new GUIContent(k_WindowName, icon);

            var configuration = new AssistantConfiguration(
                backend: s_InternalBackendOverride ?? new AssistantRelayBackend());

            s_InternalBackendOverride = null;

            m_AssistantInstance = new Assistant.Editor.Assistant(configuration);

            // Recover the conversation/model refresh promptly after system sleep or a network
            // blip, instead of waiting for the 5-minute scheduler or the next prompt.
            m_ConnectionSupervisor?.Dispose();
            m_ConnectionSupervisor = new ConnectionSupervisor(
                () => TaskUtils.WithExceptionLogging(() => m_AssistantInstance.RefreshConversationsAsync()));

            // Create and initialize a context for this window, will be unique for every active set of assistant UI / elements
            m_Context = new AssistantUIContext(AssistantInstance);

            // Register the screenshot opener service to avoid circular dependencies
            ServiceRegistry.RegisterService<IScreenshotOpener>(new ScreenshotOpenerService());
            ServiceRegistry.RegisterService<IPlanPromptFile>(new PlanPromptFile());

            m_Context.WindowDockingState = () => docked;

            m_View = new AssistantView(this);

            m_View.Initialize(m_Context);
            m_View.style.flexGrow = 1;
            m_View.style.minWidth = s_MinSize.x;
            rootVisualElement.Add(m_View);

            m_View.InitializeThemeAndStyle();
            m_View.InitializeState();

            // Initialize the view to be used to display user interactions
            m_AssistantWindowUiContainer = new AssistantWindowUiContainer(m_Context);
            m_View.BindUiContainer(m_AssistantWindowUiContainer);
            var permissionPolicy = new SettingsPermissionsPolicyProvider();

            // TODO:
            // The only reason this cannot be configured in the constructor is because the EditorToolPermissions needs
            // an AssistantUIContext which needs a Assistant which needs a ToolInteractionAndPermissionBridge which
            // needs a EditorToolPermissions ...
            configuration.Bridge = new ToolInteractionAndPermissionBridge(
                new EditorToolPermissions(m_Context, m_AssistantWindowUiContainer, permissionPolicy),
                new ToolInteractions(m_AssistantWindowUiContainer));

            m_AssistantInstance.Reconfigure(configuration);

            UserAttentionNotifier.Register(m_Context.InteractionQueue);

            // Pre-warm the dependency graph in the background.
            // With one-hop direct dependencies, generation is fast and non-blocking.
            EditorTask.delayCall += () => AssistantGraphGenerator.GenerateGraphAsync();
        }

        internal void InternalConfigureBackend(IAssistantBackend backend)
        {
            s_InternalBackendOverride = backend;

            Close();
            CreateWindow<AssistantWindow>();
        }

        void OnDestroy()
        {
            UserAttentionNotifier.Unregister();

            // Flush any pending context attach events that were never paired with a message send
            if (m_Context != null)
                AIAssistantAnalytics.ReportContextClearAllAttachedContextEvent(m_Context.Blackboard.ContextAnalyticsCache, m_Context.Blackboard.ActiveConversationId);

            AIAssistantAnalytics.ReportUITriggerLocalWindowClosedEvent(
                m_Context?.Blackboard?.ActiveConversationId ?? default,
                m_Context?.Blackboard?.IsAPIWorking ?? false);

            m_ConnectionSupervisor?.Dispose();
            m_ConnectionSupervisor = null;

            m_View?.Deinit();
            m_AssistantWindowUiContainer?.Dispose();
            m_AssistantWindowUiContainer = null;

            // The relay client outlives this window (RelayService is a singleton), so the standalone
            // fallback's caller must not: it is bound to permissions rooted in the container just
            // disposed above. Clear it so a post-close redelivery refuses to run rather than
            // executing a tool against a dead UI (or, for an auto-allow tool, with no visible UI at
            // all) — muse-editor#2705. Guard on identity so a newer window that already published
            // its own caller is not clobbered.
            if (ReferenceEquals(Unity.Relay.Editor.RelayFunctionCallFallback.FunctionCaller, m_AssistantInstance?.FunctionCaller))
                Unity.Relay.Editor.RelayFunctionCallFallback.FunctionCaller = null;

            // TODO: https://jira.unity3d.com/browse/ASST-2178
            m_AssistantInstance?.Backend?.ActiveWorkflow?.Dispose();
            m_AssistantInstance = null;
        }

        void OnLostFocus()
        {
            FocusLost?.Invoke();
        }

        public void AddItemsToMenu(GenericMenu menu)
        {
#if ASSISTANT_DEV_TOOLS_PRESENT
            menu.AddItem(new GUIContent("Assistant Dev Tools"), false, () =>
            {
                var devWindowType = Type.GetType("Unity.AI.Assistant.DeveloperTools.AssistantDevelopmentWindow, Unity.AI.Assistant.DeveloperTools");
                devWindowType?.GetMethod("ShowWindow", BindingFlags.Public | BindingFlags.Static)?.Invoke(null, null);
            });
#endif
        }

        /// <summary>
        /// Attaches an annotated screenshot captured from annotation mode to the assistant context.
        /// This method converts the PNG bytes to a VirtualAttachment and adds it to the context.
        /// Called when the user clicks Done in the annotation toolbar.
        /// </summary>
        /// <param name="screenshotData">The PNG screenshot data as bytes</param>
        internal void AttachAnnotatedScreenshot(byte[] screenshotData, byte[] annotationsData = null)
        {
            if (screenshotData == null || screenshotData.Length == 0)
            {
                InternalLog.LogWarning("[Annotation] No screenshot data to attach");
                return;
            }

            try
            {
                // Call the GetAttachment extension method from ScreenContextUtility
                var attachment = ScreenContextUtility.GetAttachment(
                    screenshotData,
                    ImageContextCategory.Screenshot,
                    "png",
                    annotationsData
                );

                if (attachment != null)
                {
                    attachment.DisplayName = "Annotated Screenshot";
                    attachment.Type = "Image";

                    m_Context.Blackboard.AddVirtualAttachment(attachment);
                    m_Context.VirtualAttachmentAdded?.Invoke(attachment);
                    AIAssistantAnalytics.CacheContextAnnotationAttachedContextEvent(m_Context.Blackboard.ContextAnalyticsCache, attachment.DisplayName);
                }
            }
            catch (System.Exception ex)
            {
                InternalLog.LogError($"[Annotation] Failed to attach annotated screenshot: {ex.Message}");
            }
        }

        internal void ReplaceScreenshot(VirtualAttachment originalAttachment, byte[] annotatedScreenshotData, byte[] annotationsData = null)
        {
            if (originalAttachment == null)
            {
                InternalLog.LogWarning("[Annotation] Original attachment is null, cannot replace");
                AttachAnnotatedScreenshot(annotatedScreenshotData, annotationsData);
                return;
            }

            if (annotatedScreenshotData == null || annotatedScreenshotData.Length == 0)
            {
                InternalLog.LogWarning("[Annotation] No screenshot data to use for replacement");
                return;
            }

            try
            {
                // Check if the original attachment had an existing annotations mask and merge if needed
                byte[] mergedAnnotationsData = annotationsData;
                if (originalAttachment.Metadata is ImageContextMetaData oldMeta && annotationsData != null)
                {
                    mergedAnnotationsData = AnnotationMaskUtility.MergeAnnotationMasks(oldMeta, annotationsData);
                }

                // Process the annotated screenshot and annotations data to get a new attachment
                // We reuse GetAttachment to process the data and handle scaling consistently
                var newAttachment = ScreenContextUtility.GetAttachment(
                    annotatedScreenshotData,
                    ImageContextCategory.Screenshot,
                    "png",
                    mergedAnnotationsData
                );

                if (newAttachment == null || string.IsNullOrEmpty(newAttachment.Payload))
                {
                    InternalLog.LogWarning("[Annotation] Failed to process annotated screenshot to base64");
                    return;
                }

                // Make sure we keep the correct type and display name
                newAttachment.Type = originalAttachment.Type;
                newAttachment.DisplayName = originalAttachment.DisplayName;
                // GetAttachment created new metadata which includes the annotations mask info

                // Find and replace the original attachment in the context
                // We need to find the corresponding context entry and replace it
                if (m_View != null)
                {
                    m_View.ReplaceContextScreenshot(originalAttachment, newAttachment);
                }

                // Replace the attachment in the blackboard (without triggering callbacks)
                // This updates the attachment for sending to the server without creating a duplicate in the UI
                m_Context.Blackboard.ReplaceVirtualAttachment(originalAttachment, newAttachment);
            }
            catch (System.Exception ex)
            {
                InternalLog.LogError($"[Annotation] Failed to replace screenshot: {ex.Message}");
            }
        }

        /// <summary>
        /// Provides safe access to virtual attachments in the assistant context.
        /// Safer alternative to directly accessing m_Context that won't break with internal structure changes.
        /// </summary>
        /// <returns>Read-only collection of virtual attachments, or null if context is not initialized</returns>
        internal IReadOnlyCollection<VirtualAttachment> GetVirtualAttachments()
        {
            return m_Context?.Blackboard?.VirtualAttachments;
        }
    }
}
