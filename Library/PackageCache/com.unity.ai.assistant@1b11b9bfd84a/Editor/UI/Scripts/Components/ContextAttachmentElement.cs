using System;
using System.Collections.Generic;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ContextAttachmentElement : ManagedTemplate, IContextReferenceVisualElement
    {
        const string k_ImageModeClass = "mui-ca-image";
        const string k_EditingClass = "mui-ca-editing";
        const string k_TargetMissingClass = "mui-ca-target-missing";
        const string k_MissingObjectIconName = "mui-object-missing";
        const string k_MissingComponentIconName = "mui-component-missing";
        const string k_VirtualContextIconNameImage = "context-virtual";
        const string k_VirtualContextIconNameProfiler = "code-block";
        const string k_VirtualContextIconNameGeneric = "pick";
        const string k_TooltipUnavailableAttachment = "The resource is no longer available.";
        static readonly List<ContextAttachmentElement> s_AllElements = new();

        VisualElement m_Root;
        VisualElement m_ThumbnailContainer;
        Image m_ThumbnailImage;
        Button m_ThumbnailRemoveButton;
        VisualElement m_PillContainer;
        AssistantImage m_PillIcon;
        Label m_PillName;
        Label m_PillType;
        Button m_PillRemoveButton;

        AssistantContextEntry m_ContextEntry;
        AssistantView m_Owner;
        bool m_ContextSet;
        bool m_VisualRegistryRegistered;

        Texture2D m_PreviewTexture;
        byte[] m_CachedImageBytes;

        Object m_CachedTargetObject;
        Component m_CachedTargetComponent;

        public ContextAttachmentElement() : base(AssistantUIConstants.UIModulePath) {}

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>(className: "mui-ca-root");
            m_ThumbnailContainer = view.Q<VisualElement>("thumbnailContainer");
            m_ThumbnailImage = view.Q<Image>("thumbnailImage");
            m_ThumbnailRemoveButton = view.SetupButton("thumbnailRemoveButton", OnRemoveClicked);
            m_PillContainer = view.Q<VisualElement>("pillContainer");
            m_PillIcon = view.SetupImage("pillIcon");
            m_PillName = view.Q<Label>("pillName");
            m_PillName.enableRichText = false;
            m_PillType = view.Q<Label>("pillType");
            m_PillType.enableRichText = false;
            m_PillRemoveButton = view.SetupButton("pillRemoveButton", OnRemoveClicked);

            m_ThumbnailContainer.RegisterCallback<PointerUpEvent>(OnThumbnailClick);
            m_PillContainer.RegisterCallback<PointerUpEvent>(OnPillClick);
            view.RegisterCallback<ContextClickEvent>(OnContextClick);

            RegisterAttachEvents(OnAttachToPanel, OnDetachFromPanel);
        }

        public void SetData(AssistantContextEntry contextEntry, AssistantView owner)
        {
            m_ContextEntry = contextEntry;
            m_Owner = owner;
            m_ContextSet = true;

            if (!m_VisualRegistryRegistered)
                RegisterContextVisualUpdate(true);

            RefreshContextCache();

            if (m_ContextEntry.EntryType == AssistantContextType.Virtual)
                TryDecodeImageFromEntry();

            RefreshUI();
        }

        void TryDecodeImageFromEntry()
        {
            if (string.IsNullOrEmpty(m_ContextEntry.Value)) return;

            try
            {
                m_CachedImageBytes = Convert.FromBase64String(m_ContextEntry.Value);
                CreateTextureFromCache();
            }
            catch (FormatException)
            {
                m_CachedImageBytes = null;
            }
        }

        void ApplyImageMode(bool imageMode)
        {
            m_Root.EnableInClassList(k_ImageModeClass, imageMode);
            m_ThumbnailContainer.SetDisplay(imageMode);
            m_PillContainer.SetDisplay(!imageMode);
        }

        static Texture GetThumbnailTexture(Object obj) =>
            obj switch
            {
                Texture texture => texture,
                Sprite sprite => sprite.texture,
                _ => null
            };

        void CreateTextureFromCache()
        {
            if (m_CachedImageBytes == null) return;

            DestroyTexture();

            var texture = new Texture2D(2, 2) { hideFlags = HideFlags.DontSave };
            if (texture.LoadImage(m_CachedImageBytes))
            {
                m_PreviewTexture = texture;
                m_ThumbnailImage.image = m_PreviewTexture;
                m_ThumbnailImage.scaleMode = ScaleMode.ScaleAndCrop;
                ApplyImageMode(true);
            }
            else
            {
                Object.DestroyImmediate(texture);
                m_CachedImageBytes = null;
            }
        }

        void DestroyTexture()
        {
            if (m_PreviewTexture != null)
            {
                Object.DestroyImmediate(m_PreviewTexture);
                m_PreviewTexture = null;
            }
        }

        void OnAttachToPanel(AttachToPanelEvent evt)
        {
            if (!m_ContextSet) return;

            if (!m_VisualRegistryRegistered)
                RegisterContextVisualUpdate(true);
            s_AllElements.Add(this);

            if (m_PreviewTexture == null && m_CachedImageBytes != null)
                CreateTextureFromCache();

            if (EditScreenCaptureWindow.Instance != null)
                EditScreenCaptureWindow.Instance.TryHighlightCurrentAttachment();
        }

        void OnDetachFromPanel(DetachFromPanelEvent evt)
        {
            DestroyTexture();
            RegisterContextVisualUpdate(false);
            s_AllElements.Remove(this);
        }

        void RegisterContextVisualUpdate(bool register)
        {
            m_VisualRegistryRegistered = register;
            if (register)
                ContextVisualElementRegistry.AddElement(m_ContextEntry, this);
            else
                ContextVisualElementRegistry.RemoveElement(this);
        }

        public void RefreshVisualElement(Object activeTargetObject, Component activeTargetComponent)
        {
            m_CachedTargetObject = activeTargetObject;
            m_CachedTargetComponent = activeTargetComponent;
            RefreshUI();
        }

        void RefreshContextCache()
        {
            switch (m_ContextEntry.EntryType)
            {
                case AssistantContextType.HierarchyObject:
                case AssistantContextType.SubAsset:
                case AssistantContextType.SceneObject:
                    m_CachedTargetObject = m_ContextEntry.GetTargetObject();
                    break;
                case AssistantContextType.Component:
                    m_CachedTargetObject = m_ContextEntry.GetTargetObject();
                    m_CachedTargetComponent = m_ContextEntry.GetComponent();
                    break;
            }
        }

        void RefreshUI()
        {
            if (!IsInitialized) return;

            m_Root.RemoveFromClassList(k_TargetMissingClass);
            m_PillName.tooltip = null;

            switch (m_ContextEntry.EntryType)
            {
                case AssistantContextType.Virtual:
                {
                    bool isImageEntry = m_ContextEntry.ImageMetadata != null;
                    bool hasPreview = m_PreviewTexture != null;

                    if (isImageEntry && hasPreview)
                    {
                        ApplyImageMode(true);
                    }
                    else
                    {
                        ApplyImageMode(false);

                        // Profiler entries
                        var iconClassName = k_VirtualContextIconNameGeneric;
                        if (m_ContextEntry.IsProfilerEntry())
                        {
                            iconClassName = k_VirtualContextIconNameProfiler;
                        }
                        // Image entries
                        else if (isImageEntry)
                        {
                            iconClassName = k_VirtualContextIconNameImage;
                        }

                        m_PillIcon.SetIconClassName(iconClassName);
                        m_PillName.text = m_ContextEntry.DisplayValue;
                        m_PillType.text = m_ContextEntry.ValueType;
                    }

                    if (!m_ContextEntry.IsVirtualResourceLocallyAvailable() && !(isImageEntry && hasPreview))
                    {
                        m_Root.AddToClassList(k_TargetMissingClass);
                        m_PillName.tooltip = k_TooltipUnavailableAttachment;
                    }

                    break;
                }

                case AssistantContextType.ConsoleMessage:
                {
                    ApplyImageMode(false);

                    if (!Enum.TryParse<LogDataType>(m_ContextEntry.ValueType, out var logMode))
                        logMode = LogDataType.Info;

                    m_PillIcon.SetIconClassName(LogUtils.GetLogIconClassName(logMode));
                    m_PillType.text = logMode.ToString();

                    if (string.IsNullOrEmpty(m_ContextEntry.Value))
                        m_PillName.text = "Unknown";
                    else
                    {
                        var lines = m_ContextEntry.Value.Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
                        if (lines.Length > 0)
                        {
                            var firstLine = lines[0];
                            m_PillName.text = firstLine.Length > 20 ? firstLine[..20] + "..." : firstLine;
                            m_PillName.tooltip = $"Console {logMode}:\n{firstLine}";
                        }
                    }

                    break;
                }

                case AssistantContextType.SceneObject:
                {
                    if (m_CachedTargetObject == null)
                    {
                        ApplyImageMode(false);
                        m_Root.AddToClassList(k_TargetMissingClass);
                        m_PillIcon.SetIconClassName(k_MissingObjectIconName);
                        m_PillName.text = m_ContextEntry.DisplayValue;
                        m_PillType.text = ContextUtils.GetShortTypeName(m_ContextEntry.ValueType);
                    }
                    else
                    {
                        var thumbnail = GetThumbnailTexture(m_CachedTargetObject);
                        if (thumbnail != null)
                        {
                            m_ThumbnailImage.image = thumbnail;
                            m_ThumbnailImage.scaleMode = ScaleMode.ScaleAndCrop;
                            ApplyImageMode(true);
                        }
                        else
                        {
                            ApplyImageMode(false);
                            m_PillIcon.SetIconClassName(null);
                            m_PillIcon.SetTexture(m_CachedTargetObject.GetTextureForObject());
                            m_PillName.text = m_CachedTargetObject.name;
                            m_PillType.text = ContextUtils.GetShortTypeName(m_ContextEntry.ValueType);
                        }
                    }

                    break;
                }

                case AssistantContextType.HierarchyObject:
                case AssistantContextType.SubAsset:
                {
                    if (m_CachedTargetObject == null)
                    {
                        ApplyImageMode(false);
                        m_Root.AddToClassList(k_TargetMissingClass);
                        m_PillIcon.SetIconClassName(k_MissingObjectIconName);
                        m_PillName.text = m_ContextEntry.DisplayValue;
                        m_PillType.text = ContextUtils.GetShortTypeName(m_ContextEntry.ValueType);
                        m_PillName.tooltip = ContextUtils.GetObjectTooltipByName(m_ContextEntry.DisplayValue, m_ContextEntry.ValueType);
                    }
                    else
                    {
                        var thumbnail = GetThumbnailTexture(m_CachedTargetObject);
                        if (thumbnail != null)
                        {
                            m_ThumbnailImage.image = thumbnail;
                            m_ThumbnailImage.scaleMode = ScaleMode.ScaleAndCrop;
                            ApplyImageMode(true);
                        }
                        else
                        {
                            ApplyImageMode(false);
                            m_PillIcon.SetIconClassName(null);
                            m_PillIcon.SetTexture(m_CachedTargetObject.GetTextureForObject());
                            m_PillName.text = m_CachedTargetObject.name;
                            m_PillType.text = ContextUtils.GetShortTypeName(m_ContextEntry.ValueType);
                            m_PillName.tooltip = ContextUtils.GetObjectTooltip(m_CachedTargetObject);
                        }
                    }

                    break;
                }

                case AssistantContextType.Component:
                {
                    ApplyImageMode(false);
                    if (m_CachedTargetComponent == null)
                    {
                        m_Root.AddToClassList(k_TargetMissingClass);
                        m_PillIcon.SetIconClassName(k_MissingComponentIconName);
                        m_PillName.text = m_ContextEntry.DisplayValue;
                        m_PillType.text = ContextUtils.GetShortTypeName(m_ContextEntry.ValueType);
                    }
                    else
                    {
                        m_PillIcon.SetIconClassName(null);
                        m_PillIcon.SetTexture(m_CachedTargetComponent.GetTextureForObjectType());
                        m_PillName.text = m_CachedTargetComponent.name;
                        m_PillType.text = m_CachedTargetComponent.GetType().Name;
                    }

                    break;
                }

                default:
                    throw new InvalidOperationException("Unhandled Context Type: " + m_ContextEntry.EntryType);
            }
        }

        void OnRemoveClicked(PointerUpEvent evt)
        {
            m_Owner?.OnRemoveContextEntry(m_ContextEntry);
            if (m_Owner != null)
            {
                AIAssistantAnalytics.ReportContextRemoveSingleAttachedContextEvent(
                    m_Owner.ActiveUIContext.Blackboard.ContextAnalyticsCache,
                    m_Owner.ActiveUIContext.Blackboard.ActiveConversationId,
                    m_ContextEntry);
            }
        }

        void OnThumbnailClick(PointerUpEvent evt)
        {
            if (IsRemoveButtonTarget(evt, m_ThumbnailRemoveButton) || !m_ContextSet)
                return;

            m_ContextEntry.Activate();
        }

        void OnPillClick(PointerUpEvent evt)
        {
            if (IsRemoveButtonTarget(evt, m_PillRemoveButton) || !m_ContextSet)
                return;

            m_ContextEntry.Activate();
        }

        static bool IsRemoveButtonTarget(PointerUpEvent evt, Button removeButton) =>
            evt.target == removeButton || (evt.target is VisualElement ve && ve.parent == removeButton);

        void OnContextClick(ContextClickEvent evt)
        {
            if (!m_ContextSet || m_Owner == null || m_ContextEntry.EntryType != AssistantContextType.Virtual) return;

            var menu = new GenericMenu();
            // Image options
            if (m_ContextEntry.ImageMetadata != null && m_ContextEntry.CanActivate())
            {
                menu.AddItem(new GUIContent(StringUtils.EscapeMenuAmpersands("View & Annotate")), false, () =>
                {
                    m_ContextEntry.Activate();
                });
            }
            // Generic options
            menu.AddItem(new GUIContent("Remove"), false, () =>
            {
                m_Owner.OnRemoveContextEntry(m_ContextEntry);
                AIAssistantAnalytics.ReportContextRemoveSingleAttachedContextEvent(
                    m_Owner.ActiveUIContext.Blackboard.ContextAnalyticsCache,
                    m_Owner.ActiveUIContext.Blackboard.ActiveConversationId,
                    m_ContextEntry);
            });
            menu.ShowAsContext();

            evt.StopPropagation();
        }

        static bool IsMatchingVirtualAttachment(ContextAttachmentElement element, VirtualAttachment attachment) =>
            element.m_ContextEntry.EntryType == AssistantContextType.Virtual &&
            element.m_ContextEntry.Value == attachment.Payload;

        public static void HighlightByVirtualAttachment(VirtualAttachment attachment)
        {
            if (attachment == null) return;

            foreach (var element in s_AllElements)
                element.m_Root?.EnableInClassList(k_EditingClass, IsMatchingVirtualAttachment(element, attachment));
        }

        public static void RemoveHighlightByVirtualAttachment(VirtualAttachment attachment)
        {
            if (attachment == null) return;

            foreach (var element in s_AllElements)
                if (IsMatchingVirtualAttachment(element, attachment))
                    element.m_Root?.RemoveFromClassList(k_EditingClass);
        }
    }
}
