using System;
using Microsoft.Msagl.Core.Geometry.Curves;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Edges
{
    class EdgeDrawManipulator : Manipulator
    {
        const float k_ArrowSize = 10f;

        Color m_SelectedColor;
        Color m_UnselectedColor;
        Color m_HoveredColor;

        readonly GraphStoreManager m_StoreManager;
        readonly EdgeData m_EdgeData;

        public EdgeDrawManipulator(EdgeData edgeData, GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
            m_EdgeData = edgeData;
            InitializeColors();
        }

        void InitializeColors()
        {
            if (EditorGUIUtility.isProSkin)
            {
                m_SelectedColor = new Color(0.26666667f, 0.75294118f, 1f);
                m_UnselectedColor = new Color(0.4f, 0.4f, 0.4f);
                m_HoveredColor = new Color(0.26666667f, 0.75294118f, 1f, 0.5f);
            }
            else
            {
                m_SelectedColor = new Color(0.57254902f, 0.74117647f, 1.0f);
                m_UnselectedColor = new Color(0.43921569f, 0.43921569f, 0.43921569f);
                m_HoveredColor = new Color(0.57254902f, 0.74117647f, 1.0f, 0.5f);
            }
        }

        protected override void RegisterCallbacksOnTarget()
        {
            target.generateVisualContent += GenerateVisualContent;
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            target.generateVisualContent -= GenerateVisualContent;
        }

        void GenerateVisualContent(MeshGenerationContext context)
        {
            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            var selectedIndex = state.SelectedObject as int? ?? -1;
            var selected = selectedIndex == m_EdgeData.TensorIndex;
            var hovered = false;
            foreach (var hoveredObject in state.HoveredObjects)
            {
                if (hoveredObject is int index && index == m_EdgeData.TensorIndex)
                {
                    hovered = true;
                    break;
                }
            }

            var painter = context.painter2D;

            painter.lineWidth = 1.0f;
            painter.lineCap = LineCap.Butt;

            Color strokeColor;
            if (selected)
            {
                strokeColor = m_SelectedColor;
            }
            else if (hovered)
            {
                strokeColor = m_HoveredColor;
            }
            else
            {
                strokeColor = m_UnselectedColor;
            }

            painter.strokeColor = strokeColor;
            painter.fillColor = painter.strokeColor;

            DrawCurve(m_EdgeData.Edge.Curve, painter, new Vector2(0, 0));
        }

        static void DrawCurve(ICurve iCurve, Painter2D painter, Vector2 offset, bool drawArrowhead = true)
        {
            switch (iCurve)
            {
                case CubicBezierSegment bezier:
                {
                    var p0 = new Vector2((float)bezier.B(0).X, -(float)bezier.B(0).Y) - offset;
                    var p1 = new Vector2((float)bezier.B(1).X, -(float)bezier.B(1).Y) - offset;
                    var p2 = new Vector2((float)bezier.B(2).X, -(float)bezier.B(2).Y) - offset;
                    var p3 = new Vector2((float)bezier.B(3).X, -(float)bezier.B(3).Y) - offset;

                    var p3Adjusted = p3;
                    if (drawArrowhead)
                    {
                        var arrowDirection = (p3 - p2).normalized;
                        p3Adjusted = p3 - arrowDirection * k_ArrowSize;
                    }

                    painter.BeginPath();
                    painter.MoveTo(p0);
                    painter.BezierCurveTo(p1, p2, p3Adjusted);
                    painter.Stroke();
                    painter.ClosePath();

                    if (drawArrowhead)
                        DrawArrowhead(painter, p3Adjusted, p3);
                    break;
                }
                case Curve curve:
                    for (var i = 0; i < curve.Segments.Count; i++)
                    {
                        DrawCurve(curve.Segments[i], painter, offset, i == curve.Segments.Count - 1 && drawArrowhead);
                    }

                    break;
                case LineSegment line:
                {
                    var start = new Vector2((float)line.Start.X, -(float)line.Start.Y) - offset;
                    var end = new Vector2((float)line.End.X, -(float)line.End.Y) - offset;

                    var direction = (end - start).normalized;

                    var endAdjusted = end;
                    if (drawArrowhead)
                        endAdjusted = end - direction * k_ArrowSize;

                    painter.BeginPath();
                    painter.MoveTo(start);
                    painter.LineTo(endAdjusted);
                    painter.Stroke();
                    painter.ClosePath();

                    if (drawArrowhead)
                        DrawArrowhead(painter, endAdjusted, end);
                    break;
                }
                case Ellipse:
                case Polyline:
                case RoundedRect:
                    throw new NotImplementedException();
            }
        }

        static void DrawArrowhead(Painter2D painter, Vector2 start, Vector2 end)
        {
            var direction = (end - start).normalized;
            var left = RotatePoint(end - direction * k_ArrowSize, end, Mathf.PI / 6);
            var right = RotatePoint(end - direction * k_ArrowSize, end, -Mathf.PI / 6);

            painter.BeginPath();
            painter.MoveTo(end);
            painter.LineTo(left);
            painter.LineTo(right);
            painter.LineTo(end);
            painter.Fill();
            painter.ClosePath();
        }

        static Vector2 RotatePoint(Vector2 point, Vector2 center, float angle)
        {
            var cos = Mathf.Cos(angle);
            var sin = Mathf.Sin(angle);
            var tmp = point - center;
            var xNew = tmp.x * cos - tmp.y * sin;
            var yNew = tmp.x * sin + tmp.y * cos;
            return new Vector2(xNew, yNew) + center;
        }
    }
}
