using System.IO;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.ChatElements
{
    class CompletedActionEntry : ManagedTemplate
    {
        const string k_MissingRefClass = "completed-action-ref-missing";

        VisualElement m_StatusIcon;
        Label m_Description;
        VisualElement m_LineCountContainer;
        Label m_LinesAdded;
        Label m_LinesRemoved;
        VisualElement m_ClickableRefButton;
        Image m_RefIcon;
        Label m_RefLabel;

        string m_ClickableRef;

        public CompletedActionEntry()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_StatusIcon = view.Q("actionStatusIcon");
            m_Description = view.Q<Label>("actionDescription");
            m_LineCountContainer = view.Q("lineCountContainer");
            m_LinesAdded = view.Q<Label>("linesAdded");
            m_LinesRemoved = view.Q<Label>("linesRemoved");
            m_ClickableRefButton = view.Q("objectRefButton");
            m_RefIcon = view.Q<Image>("objectRefIcon");
            m_RefLabel = view.Q<Label>("objectRefLabel");
        }

        public void SetData(CompletedActionData data)
        {
            m_StatusIcon.EnableInClassList("mui-icon-checkmark-green", data.IsSuccess);
            m_StatusIcon.EnableInClassList("mui-icon-error", !data.IsSuccess);

            m_Description.text = data.Title;
            m_ClickableRef = data.ClickableRef;

            bool hasClickableRef = !string.IsNullOrEmpty(m_ClickableRef);
            m_ClickableRefButton.SetDisplay(hasClickableRef);
            if (hasClickableRef)
            {
                m_RefLabel.text = GetButtonText(m_ClickableRef);
                UpdateRefState(m_ClickableRef);
            }
            else
            {
                m_RefIcon.SetDisplay(false);
            }

            bool hasLineCounts = data.LinesAdded > 0 || data.LinesRemoved > 0;
            m_LineCountContainer.SetDisplay(hasLineCounts);
            if (hasLineCounts)
            {
                m_LinesAdded.text = data.LinesAdded > 0 ? "+" + data.LinesAdded : "";
                m_LinesRemoved.text = data.LinesRemoved > 0 ? "-" + data.LinesRemoved : "";
            }
        }

        void UpdateRefState(string clickableRef)
        {
            if (TryParseInstanceRef(clickableRef, out _, out long instanceId))
            {
#if UNITY_6000_5_OR_NEWER
                var obj = EditorUtility.EntityIdToObject(EntityId.FromULong((ulong)instanceId));
#elif UNITY_6000_3_OR_NEWER
                var obj = EditorUtility.EntityIdToObject((int)instanceId);
#else
                var obj = EditorUtility.InstanceIDToObject((int)instanceId);
#endif
                if (obj != null)
                {
                    SetRefIcon(EditorGUIUtility.ObjectContent(obj, obj.GetType()).image as Texture2D);
                    SetRefMissing(false);
                }
                else
                {
                    m_RefIcon.SetDisplay(false);
                    SetRefMissing(true);
                }
            }
            else
            {
                // Asset path ref
                bool exists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(clickableRef));
                if (exists)
                {
                    SetRefIcon(AssetDatabase.GetCachedIcon(clickableRef) as Texture2D);
                    SetRefMissing(false);
                }
                else
                {
                    m_RefIcon.SetDisplay(false);
                    SetRefMissing(true);
                }
            }
        }

        void SetRefIcon(Texture2D icon)
        {
            if (icon != null)
            {
                m_RefIcon.image = icon;
                m_RefIcon.SetDisplay(true);
            }
            else
            {
                m_RefIcon.SetDisplay(false);
            }
        }

        void SetRefMissing(bool missing)
        {
            m_ClickableRefButton.EnableInClassList(k_MissingRefClass, missing);
            m_ClickableRefButton.UnregisterCallback<ClickEvent>(OnRefClicked);
            if (!missing)
                m_ClickableRefButton.RegisterCallback<ClickEvent>(OnRefClicked);
        }

        void OnRefClicked(ClickEvent evt)
        {
            OnClickableRefClicked();
        }

        void OnClickableRefClicked()
        {
            if (string.IsNullOrEmpty(m_ClickableRef))
                return;

            if (TryParseInstanceRef(m_ClickableRef, out _, out long instanceId))
                ExecutionLogUtils.PingObjectByInstanceId(instanceId);
            else
                ExecutionLogUtils.PingAssetAtPath(m_ClickableRef);
        }

        static string GetButtonText(string clickableRef)
        {
            if (TryParseInstanceRef(clickableRef, out string displayName, out _))
                return displayName;

            return Path.GetFileName(clickableRef);
        }

        // clickableRef format: "DisplayName|InstanceID:12345" for object refs, or an asset path otherwise.
        static bool TryParseInstanceRef(string clickableRef, out string displayName, out long instanceId)
        {
            int separatorIndex = clickableRef.LastIndexOf("|InstanceID:");
            if (separatorIndex >= 0)
            {
                string idStr = clickableRef.Substring(separatorIndex + "|InstanceID:".Length);
                if (long.TryParse(idStr, out instanceId))
                {
                    displayName = clickableRef.Substring(0, separatorIndex);
                    return true;
                }
            }

            displayName = null;
            instanceId = 0;
            return false;
        }
    }
}
