using Unity.AI.Assistant.UI.Editor.Scripts.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components
{
    class ContextUsageView : ManagedTemplate
    {
        const string k_WarningClass = "mui-context-usage-warning";
        const string k_Tooltip = "Conversation context used.\nWhen full, the conversation history will be summarized to free up space.";

        Label m_Label;
        VisualElement m_Ring;
        VisualElement m_Root;
        float m_Progress;

        public ContextUsageView()
            : base(AssistantUIConstants.UIModulePath)
        {
        }

        public void SetUsage(int usedTokens, int maxTokens)
        {
            m_Progress = maxTokens > 0 ? (float)usedTokens / maxTokens : 0f;
            m_Label.text = Mathf.RoundToInt(m_Progress * 100) + "%";
            m_Root.SetDisplay(true);
            m_Root.EnableInClassList(k_WarningClass, m_Progress > 0.8f);
            m_Ring.MarkDirtyRepaint();
        }

        public void ResetUsage()
        {
            m_Progress = 0f;
            m_Root.SetDisplay(false);
            m_Root.EnableInClassList(k_WarningClass, false);
        }

        protected override void InitializeView(TemplateContainer view)
        {
            m_Root = view.Q<VisualElement>("contextUsageRoot");
            m_Root.tooltip = L10n.Tr(k_Tooltip);
            m_Label = view.Q<Label>("contextUsageLabel");
            m_Ring = view.Q<VisualElement>("contextUsageRing");
            m_Ring.generateVisualContent += DrawRing;
        }

        void DrawRing(MeshGenerationContext ctx)
        {
            var rect = m_Ring.contentRect;
            if (rect.width <= 0)
            {
                return;
            }

            var center = rect.center;
            var radius = Mathf.Min(rect.width, rect.height) * 0.5f;
            var strokeWidth = 2f;
            var baseColor = EditorGUIUtility.isProSkin ? new Color(1f, 1f, 1f) : new Color(0.035f, 0.035f, 0.035f);

            var painter = ctx.painter2D;
            painter.lineWidth = strokeWidth;
            painter.lineCap = LineCap.Butt;

            painter.strokeColor = new Color(baseColor.r, baseColor.g, baseColor.b, 0.15f);
            painter.BeginPath();
            painter.Arc(center, radius - strokeWidth * 0.5f, 0, 360);
            painter.Stroke();

            if (m_Progress <= 0)
            {
                return;
            }

            var warningColor = EditorGUIUtility.isProSkin ? new Color(1f, 0.71f, 0.24f) : new Color(0.722f, 0.533f, 0.039f);
            painter.strokeColor = m_Progress > 0.8f ? warningColor : new Color(baseColor.r, baseColor.g, baseColor.b, 0.7f);
            painter.BeginPath();
            painter.Arc(center, radius - strokeWidth * 0.5f, -90, -90 + m_Progress * 360f);
            painter.Stroke();
        }
    }
}
