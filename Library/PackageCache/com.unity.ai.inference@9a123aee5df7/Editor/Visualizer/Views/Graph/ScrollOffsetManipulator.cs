using System;
using Unity.InferenceEngine.Editor.Visualizer.Extensions;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Graph
{
    class ScrollOffsetManipulator : Manipulator
    {
        GraphView m_GraphView;
        VisualElement m_BoundsMarker;

        Rect Bound
        {
            get
            {
                if (m_BoundsMarker == null || m_GraphView.NodeViews.Count == 0)
                    return Rect.zero;

                var markerBounds = m_BoundsMarker.GetWorldBoundingBox();

                return markerBounds;
            }
        }

        public void UpdateBoundsMarker()
        {
            if (m_BoundsMarker == null || m_GraphView.NodeViews.Count == 0)
                return;

            // Re-add marker if it was removed (e.g., by Clear())
            if (m_BoundsMarker.parent == null)
                m_GraphView.Add(m_BoundsMarker);

            // Schedule bounds update after layout pass
            m_GraphView.schedule.Execute(() =>
            {
                if (m_GraphView.NodeViews.Count == 0)
                    return;

                var first = true;
                var bounds = Rect.zero;

                foreach (var nodeKvp in m_GraphView.NodeViews)
                {
                    var nodeData = nodeKvp.Key;
                    var nodeBounds = new Rect(nodeData.CanvasPosition, nodeData.CanvasSize);

                    if (nodeBounds.width <= 0 || nodeBounds.height <= 0)
                        continue;

                    if (first)
                    {
                        bounds = nodeBounds;
                        first = false;
                    }
                    else
                    {
                        var xMin = Mathf.Min(bounds.xMin, nodeBounds.xMin);
                        var yMin = Mathf.Min(bounds.yMin, nodeBounds.yMin);
                        var xMax = Mathf.Max(bounds.xMax, nodeBounds.xMax);
                        var yMax = Mathf.Max(bounds.yMax, nodeBounds.yMax);
                        bounds = new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
                    }
                }

                // Update marker position and size
                m_BoundsMarker.style.left = bounds.x;
                m_BoundsMarker.style.top = bounds.y;
                m_BoundsMarker.style.width = bounds.width;
                m_BoundsMarker.style.height = bounds.height;
            });
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_GraphView = target as GraphView;
            if (m_GraphView is null)
            {
                throw new InvalidOperationException("ScrollOffsetManipulator can only be used on GraphView.");
            }

            // Create bounds marker element
            m_BoundsMarker = new VisualElement
            {
                style =
                {
                    position = Position.Absolute
                },
                pickingMode = PickingMode.Ignore,
                name = "BoundsMarker"
            };
            m_GraphView.Add(m_BoundsMarker);

            m_GraphView.scrollOffsetChanged += OnScrollOffsetChanged;
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            if (m_GraphView is not null)
            {
                m_GraphView.scrollOffsetChanged -= OnScrollOffsetChanged;

                if (m_BoundsMarker != null && m_BoundsMarker.parent != null)
                {
                    m_BoundsMarker.parent.Remove(m_BoundsMarker);
                    m_BoundsMarker = null;
                }
            }
        }

        void OnScrollOffsetChanged()
        {
            var contentBounds = Bound;
            var viewportSize = new Vector2(m_GraphView.resolvedStyle.width, m_GraphView.resolvedStyle.height);

            if (viewportSize.x <= 0 || viewportSize.y <= 0 || contentBounds.width <= 0 || contentBounds.height <= 0)
                return;

            // Convert to local space (accounts for zoom/scale)
            var localBounds = m_GraphView.WorldToLocal(contentBounds);

            // Get offsets that would center each corner
            var topLeftPoint = new Rect(contentBounds.xMin, contentBounds.yMin, 0, 0);
            var bottomRightPoint = new Rect(contentBounds.xMax, contentBounds.yMax, 0, 0);
            var offsetForTopLeft = ScrollOffsetForWorldRect(topLeftPoint);
            var offsetForBottomRight = ScrollOffsetForWorldRect(bottomRightPoint);

            var halfViewport = viewportSize * 0.5f;

            // Minimum portion of content that must remain visible (in local pixels)
            var minVisible = new Vector2(
                Mathf.Min(50f, localBounds.width),
                Mathf.Min(50f, localBounds.height)
            );

            // Calculate margins: how far past the "centered corner" we can scroll
            // This keeps minVisible pixels on screen
            var margin = halfViewport - minVisible;

            // Calculate raw offset limits based on corners and margins
            var rawMin = offsetForTopLeft - margin;
            var rawMax = offsetForBottomRight + margin;

            // Ensure min <= max for each axis (handles both small and large content)
            var minOffset = new Vector2(
                Mathf.Min(rawMin.x, rawMax.x),
                Mathf.Min(rawMin.y, rawMax.y)
            );
            var maxOffset = new Vector2(
                Mathf.Max(rawMin.x, rawMax.x),
                Mathf.Max(rawMin.y, rawMax.y)
            );

            m_GraphView.scrollOffsetChanged -= OnScrollOffsetChanged;

            m_GraphView.scrollOffset = new Vector2(
                Mathf.Clamp(m_GraphView.scrollOffset.x, minOffset.x, maxOffset.x),
                Mathf.Clamp(m_GraphView.scrollOffset.y, minOffset.y, maxOffset.y)
            );

            m_GraphView.scrollOffsetChanged += OnScrollOffsetChanged;
        }

        Vector2 ScrollOffsetForWorldRect(Rect worldRect)
        {
            var container = m_GraphView.frameContainer.IsSet ? m_GraphView.frameContainer.Value : m_GraphView.contentRect;
            var containerCenter = new Vector2(container.width * 0.5f, container.height * 0.5f) + container.position;

            var localRect = m_GraphView.WorldToLocal(worldRect);
            var centerDelta = localRect.center - containerCenter;
            var offset = m_GraphView.scrollOffset + centerDelta;

            return offset;
        }
    }
}
