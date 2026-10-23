using System;
using Microsoft.Msagl.Core.Geometry;
using Microsoft.Msagl.Core.Geometry.Curves;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Edges
{
    static class EdgeEventsUtils
    {
        public static EdgeData GetTouchingEdge(GraphState state, Vector2 position)
        {
            for (var i = 0; i < state.Edges.Count; i++)
            {
                var edge = state.Edges[i];
                var isEdgeOnCurve = IsPointOnCurve(edge.Edge.Curve, position);
                if (isEdgeOnCurve)
                {
                    return edge;
                }
            }

            return null;
        }

        static bool IsPointOnCurve(ICurve curve, Vector2 point, float threshold = 5f)
        {
            if (curve is Curve curves)
            {
                for (var i = 0; i < curves.Segments.Count; ++i)
                {
                    if (IsPointOnCurve(curves.Segments[i], point, threshold))
                    {
                        return true;
                    }
                }

                return false;
            }

            // Convert Unity Vector2 to MSAGL Point (note Y-flip)
            var msaglPoint = new Point(point.x, -point.y);

            // Find the closest parameter value on the curve
            var t = ClosestPointOnCurve.ClosestPoint(curve, msaglPoint, 0.5, 0, 1);

            // Get the closest point on the curve using the found parameter
            var closestPoint = curve[t];

            // Convert MSAGL point back to Unity coordinates
            var unityPoint = new Vector2((float)closestPoint.X, -(float)closestPoint.Y);

            // Calculate distance
            var distance = Vector2.Distance(point, unityPoint);

            // Check if the distance is within threshold
            return distance <= threshold;
        }
    }
}
