using System;
using Microsoft.Msagl.Core.Geometry.Curves;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Edges
{
    static class EdgeUtil
    {
        const float k_MinimumWidth = 50f;
        const float k_MinimumHeight = 20f;
        const float k_Padding = 10f;
        const int k_BoundsCalculationSamples = 8;

        public static Rect CalculateCurveBounds(this EdgeData edgeData, ICurve curve)
        {
            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            void UpdateBounds(float x, float y)
            {
                minX = Mathf.Min(minX, x);
                minY = Mathf.Min(minY, y);
                maxX = Mathf.Max(maxX, x);
                maxY = Mathf.Max(maxY, y);
            }

            switch (curve)
            {
                case CubicBezierSegment bezier:
                {
                    var p0 = new Vector2((float)bezier.B(0).X, -(float)bezier.B(0).Y);
                    var p1 = new Vector2((float)bezier.B(1).X, -(float)bezier.B(1).Y);
                    var p2 = new Vector2((float)bezier.B(2).X, -(float)bezier.B(2).Y);
                    var p3 = new Vector2((float)bezier.B(3).X, -(float)bezier.B(3).Y);

                    UpdateBounds(p0.x, p0.y);
                    UpdateBounds(p1.x, p1.y);
                    UpdateBounds(p2.x, p2.y);
                    UpdateBounds(p3.x, p3.y);

                    for (var i = 1; i < k_BoundsCalculationSamples; i++)
                    {
                        var t = i / (float)k_BoundsCalculationSamples;
                        var samplePoint = CalculateBezierPoint(t, p0, p1, p2, p3);
                        UpdateBounds(samplePoint.x, samplePoint.y);
                    }

                    break;
                }

                case LineSegment line:
                {
                    UpdateBounds((float)line.Start.X, -(float)line.Start.Y);
                    UpdateBounds((float)line.End.X, -(float)line.End.Y);
                    break;
                }

                case Curve complexCurve:
                {
                    foreach (var segment in complexCurve.Segments)
                    {
                        var segmentBounds = CalculateCurveBounds(edgeData, segment);
                        UpdateBounds(segmentBounds.xMin, segmentBounds.yMin);
                        UpdateBounds(segmentBounds.xMax, segmentBounds.yMax);
                    }

                    break;
                }

                default:
                    return CreateRectFromDiag(edgeData.Source.CanvasPosition, edgeData.Target.CanvasPosition);
            }

            if (minX > maxX || minY > maxY)
            {
                return CreateRectFromDiag(edgeData.Source.CanvasPosition, edgeData.Target.CanvasPosition);
            }

            var width = maxX - minX + k_Padding * 2;
            var height = maxY - minY + k_Padding * 2;

            if (width < k_MinimumWidth)
            {
                var additionalWidth = k_MinimumWidth - width;
                minX -= additionalWidth / 2;
                width = k_MinimumWidth;
            }

            if (height < k_MinimumHeight)
            {
                var additionalHeight = k_MinimumHeight - height;
                minY -= additionalHeight / 2;
                height = k_MinimumHeight;
            }

            return new Rect(minX - k_Padding, minY - k_Padding, width, height);
        }

        public static Vector2 CalculateBezierPoint(float t, Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var u = 1 - t;
            var tt = t * t;
            var uu = u * u;
            var uuu = uu * u;
            var ttt = tt * t;

            // Formula: (1-t)³P₀ + 3(1-t)²tP₁ + 3(1-t)t²P₂ + t³P₃
            var point = uuu * p0;
            point += 3 * uu * t * p1;
            point += 3 * u * tt * p2;
            point += ttt * p3;

            return point;
        }

        static Rect CreateRectFromDiag(Vector2 point1, Vector2 point2)
        {
            var minX = Mathf.Min(point1.x, point2.x);
            var minY = Mathf.Min(point1.y, point2.y);
            var maxX = Mathf.Max(point1.x, point2.x);
            var maxY = Mathf.Max(point1.y, point2.y);

            var width = maxX - minX;
            var height = maxY - minY;

            if (width < k_MinimumWidth)
            {
                var additionalWidth = k_MinimumWidth - width;
                minX -= additionalWidth / 2;
                maxX += additionalWidth / 2;
            }

            if (height < k_MinimumHeight)
            {
                var additionalHeight = k_MinimumHeight - height;
                minY -= additionalHeight / 2;
                maxY += additionalHeight / 2;
            }

            return new Rect(minX, minY, maxX - minX, maxY - minY);
        }
    }
}
