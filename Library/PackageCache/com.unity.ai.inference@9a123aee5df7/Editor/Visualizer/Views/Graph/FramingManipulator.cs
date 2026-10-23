using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Msagl.Core.Geometry;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Graph
{
    class FramingManipulator : Manipulator
    {
        const float k_TopPadding = 50f;

        GraphView m_Target;
        readonly GraphStoreManager m_GraphStoreManager;
        IDisposableSubscription m_StoreUnsub;
        EditorApplication.CallbackFunction m_OnAnimationUpdate;

        public FramingManipulator(GraphStoreManager storeManager)
        {
            m_GraphStoreManager = storeManager;
        }

        protected override void RegisterCallbacksOnTarget()
        {
            m_Target = target as GraphView;

            m_StoreUnsub = m_GraphStoreManager.Store.Subscribe(GraphSlice.Name, (GraphState state) =>
            {
                if (state.FocusedData != null)
                {
                    var data = state.FocusedData;
                    StartFramingObject(data);
                    m_GraphStoreManager.Store.Dispatch(GraphStoreManager.SetFocusedObject?.Invoke(null));
                }
            });
        }

        protected override void UnregisterCallbacksFromTarget()
        {
            m_Target = null;

            m_StoreUnsub?.Dispose();
        }

        void StartFramingObject(FocusData data)
        {
            switch (data.Target)
            {
                case int index:
                    FrameEdges(index, data.SkipAnimation);
                    break;

                case NodeData node:
                    _ = FrameNode(node, data);
                    break;

                default:
                    throw new NotImplementedException();
            }
        }

        void FrameEdges(int sentisIndex, bool skipAnimation)
        {
            Rect? ogRect = null;
            var relatedEdges = m_Target.EdgeViews.Values.Where(x => x.edgeData.TensorIndex == sentisIndex);
            foreach (var edge in relatedEdges)
            {
                var edgeRect = ToRect(edge.edgeData.Edge.BoundingBox);
                if (ogRect == null)
                {
                    ogRect = edgeRect;
                    continue;
                }

                ogRect = MergeRects(ogRect.Value, edgeRect);
            }

            if (ogRect == null)
                return;

            _ = FramePosition(ogRect.Value.center, ogRect.Value.size, skipAnimation);
        }

        static Rect ToRect(Rectangle rectangle)
        {
            var size = new Vector2((float)rectangle.Width, (float)rectangle.Height);
            var position = new Vector2((float)rectangle.Center.X - size.x / 2f, -(float)rectangle.Center.Y - size.y / 2f);
            return new Rect(position, size);
        }

        static Rect MergeRects(Rect a, Rect b)
        {
            // Find the minimum x and y coordinates
            var xMin = Mathf.Min(a.xMin, b.xMin);
            var yMin = Mathf.Min(a.yMin, b.yMin);

            // Find the maximum x and y coordinates
            var xMax = Mathf.Max(a.xMax, b.xMax);
            var yMax = Mathf.Max(a.yMax, b.yMax);

            // Create a new rect with the calculated dimensions
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        async Task FrameNode(NodeData node, FocusData data)
        {
            var nodeCenter = node.CanvasPosition + node.CanvasSize / 2f;
            var isOffsetAlignment = data.Alignment != Vector2.zero;

            if (isOffsetAlignment)
                m_Target.contentContainer.style.display = DisplayStyle.None;

            try
            {
                var frameCenterOffsetY = data.Alignment.y * (data.ZoomLevel / 2f - k_TopPadding - node.CanvasSize.y / 2f);
                var frameCenterOffsetX = data.Alignment.x * (data.ZoomLevel / 2f - k_TopPadding - node.CanvasSize.x / 2f);
                var frameCenter = new Vector2(nodeCenter.x + frameCenterOffsetX, nodeCenter.y + frameCenterOffsetY);
                var frameSize = new Vector2(data.ZoomLevel, data.ZoomLevel);

                await FramePosition(frameCenter, frameSize, data.SkipAnimation);
            }
            finally
            {
                if (isOffsetAlignment && m_Target != null)
                    m_Target.contentContainer.style.display = DisplayStyle.Flex;
            }
        }

        async Task FramePosition(Vector2 position, Vector2 rectSize, bool skipAnimation)
        {
            const float animationSpeed = 0.1f;

            // Cancel any in-flight animation to avoid stacked update callbacks
            if (m_OnAnimationUpdate != null)
            {
                EditorApplication.update -= m_OnAnimationUpdate;
                m_OnAnimationUpdate = null;
            }

            var currentZoom = m_Target.zoom;
            var currentTranslation = m_Target.scrollOffset;

            rectSize.x = Mathf.Max(rectSize.x, GraphView.ZoomLevel.MinZoom);
            rectSize.y = Mathf.Max(rectSize.y, GraphView.ZoomLevel.MinZoom);

            position -= rectSize / 2f;
            m_Target.FrameArea(new Rect(position, rectSize));

            if (skipAnimation)
                return;

            var targetZoom = m_Target.zoom;
            var trayElement = m_Target.GraphInspector.Tray.view.Q("appui-tray__tray");
            var targetTranslation = m_Target.scrollOffset + new Vector2(trayElement.resolvedStyle.width / 2f, 0);

            m_Target.zoom = currentZoom;
            m_Target.scrollOffset = currentTranslation;

            var tcs = new TaskCompletionSource<bool>();
            var elapsed = 0.0;
            var lastTime = EditorApplication.timeSinceStartup;

            m_OnAnimationUpdate = () =>
            {
                if (m_Target == null)
                {
                    EditorApplication.update -= m_OnAnimationUpdate;
                    m_OnAnimationUpdate = null;
                    tcs.TrySetCanceled();
                    return;
                }

                var now = EditorApplication.timeSinceStartup;
                var deltaTime = now - lastTime;
                lastTime = now;
                elapsed += deltaTime;

                var progress = Mathf.Clamp01((float)(elapsed / animationSpeed));
                m_Target.zoom = Mathf.Lerp(currentZoom, targetZoom, progress);
                m_Target.scrollOffset = Vector2.Lerp(currentTranslation, targetTranslation, progress);
                m_Target.MarkDirtyRepaint();

                if (elapsed >= animationSpeed)
                {
                    m_Target.zoom = targetZoom;
                    m_Target.scrollOffset = targetTranslation;
                    EditorApplication.update -= m_OnAnimationUpdate;
                    m_OnAnimationUpdate = null;
                    tcs.TrySetResult(true);
                }
            };

            EditorApplication.update += m_OnAnimationUpdate;

            try
            {
                await tcs.Task;
            }
            finally
            {
                if (m_OnAnimationUpdate != null)
                {
                    EditorApplication.update -= m_OnAnimationUpdate;
                    m_OnAnimationUpdate = null;
                }
            }
        }
    }
}
