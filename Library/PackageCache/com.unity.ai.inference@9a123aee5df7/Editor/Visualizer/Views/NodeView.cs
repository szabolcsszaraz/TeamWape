using System;
using System.Threading;
using System.Threading.Tasks;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class NodeView : Label
    {
        static readonly Rect k_FixedWorldBound = new(0f, 0f, 125f, 25f);
        readonly GraphStoreManager m_StoreManager;
        public NodeData nodeData { get; }
        bool Selected { get; set; }

        const string k_HoveredClass = "hovered";

        public NodeView(GraphStoreManager storeManager, NodeData nodeData)
            : base(nodeData.Name)
        {
            m_StoreManager = storeManager;
            this.nodeData = nodeData;

            usageHints = UsageHints.DynamicTransform;
            style.position = Position.Absolute;
            switch (nodeData)
            {
                case InputNodeData _:
                    AddToClassList("input-node");
                    break;
                case OutputNodeData _:
                    AddToClassList("output-node");
                    break;
                case LayerNodeData layerNode:
                    AddToClassList($"layer-node-{layerNode.Category}");
                    break;
            }

            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.unity.ai.inference/Editor/Visualizer/Styles/Node.uss");
            styleSheets.Add(sheet);

            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
        }

        public void UpdateState(GraphState state)
        {
            SetSelected(state.SelectedObject == nodeData);
            UpdateHoveredState(state);
        }

        void UpdateHoveredState(GraphState state)
        {
            if (state.HoveredObjects.Contains(nodeData) && state.SelectedObject != nodeData)
            {
                AddToClassList(k_HoveredClass);
                MarkDirtyRepaint();
            }
            else if (ClassListContains(k_HoveredClass))
            {
                RemoveFromClassList(k_HoveredClass);
                MarkDirtyRepaint();
            }
        }

        void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (!m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name).HoveredObjects.Contains(nodeData))
                return;

            evt.StopImmediatePropagation();
            evt.StopPropagation();

            m_StoreManager.Store.Dispatch(GraphStoreManager.RemoveHoveredObject.Invoke(nodeData));
        }

        void OnPointerMove(PointerMoveEvent evt)
        {
            if (m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name).HoveredObjects.Contains(nodeData))
                return;

            evt.StopImmediatePropagation();
            evt.StopPropagation();

            m_StoreManager.Store.Dispatch(GraphStoreManager.AddHoveredObject.Invoke(nodeData));
        }

        void OnPointerDown(PointerDownEvent evt)
        {
            evt.StopImmediatePropagation();
            evt.StopPropagation();

            if (evt.button != 0 || Selected)
                return;

            m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(nodeData));
        }

        void SetSelected(bool selected)
        {
            if (Selected == selected)
                return;

            Selected = selected;
            if (Selected)
                AddToClassList("selected");
            else
                RemoveFromClassList("selected");

            MarkDirtyRepaint();
        }

        public void UpdateCanvasPosition()
        {
            style.translate = new Translate(nodeData.CanvasPosition.x, nodeData.CanvasPosition.y);
        }

        public async Task <Rect> GetWorldBound(CancellationToken cancellationToken = default)
        {
            if (IsWorldBoundValid())
                return worldBound;

            if (Application.isBatchMode) //UiToolkit is not officially supported in batchmode, so we return a fixed precomputed bound
            {
                return k_FixedWorldBound;
            }

            return await GetWorldBoundWhenReady(cancellationToken);
        }

        async Task<Rect> GetWorldBoundWhenReady(CancellationToken cancellationToken = default(CancellationToken))
        {
            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            CancellationTokenRegistration ctr = default;
            var cleanedUp = false;

            RegisterCallback<GeometryChangedEvent>(OnGeometryChanged);

            ctr = cancellationToken.Register(() =>
            {
                tcs.TrySetCanceled(cancellationToken);
                Cleanup();
            });

            try
            {
                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                Cleanup();
            }

            return worldBound;

            void OnGeometryChanged(GeometryChangedEvent _)
            {
                if (!IsWorldBoundValid())
                    return;

                tcs.TrySetResult(true);
                Cleanup();
            }

            void Cleanup()
            {
                if (cleanedUp) return;
                cleanedUp = true;

                UnregisterCallback<GeometryChangedEvent>(OnGeometryChanged);
                ctr.Dispose();
            }
        }

        bool IsWorldBoundValid()
        {
            return worldBound.width is not float.NaN && worldBound is not { width: 0f } && worldBound.height is not float.NaN && worldBound is not { height: 0f };
        }
    }
}
