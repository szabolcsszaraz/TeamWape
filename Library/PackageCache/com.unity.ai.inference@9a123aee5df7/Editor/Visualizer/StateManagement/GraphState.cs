using System;
using System.Collections.Generic;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.Views;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.StateManagement
{
    /// <summary>
    /// Request to focus an object in the graph view.
    /// </summary>
    /// <param name="Target">The object to focus (NodeData or tensor index)</param>
    /// <param name="Alignment">Normalized Vector2 for positioning (zero=center, up=top, etc.)</param>
    /// <param name="ZoomLevel">The zoom level for framing</param>
    /// <param name="SkipAnimation">Whether to skip the framing animation</param>
    record FocusData(object Target, Vector2 Alignment, float ZoomLevel, bool SkipAnimation);

    record GraphState: IDisposable
    {

        // Model State
        [NonSerialized]
        public Model Model;
        public ModelAsset ModelAsset;

        // Computation State
        [NonSerialized]
        public PartialInferenceContext PartialInferenceContext;
        public GraphView GraphView;
        public LoadingState LoadingStatus;
        public string ErrorMessage;
        public List<NodeData> Nodes = new();
        public List<EdgeData> Edges = new();

        // UI State
        public FocusData FocusedData = null;
        public List<object> SelectionHistory = new();
        public int CurrentSelectionIndex = -1;
        public List<object> HoveredObjects = new();

        public object SelectedObject
        {
            get
            {
                try
                {
                    return SelectionHistory[CurrentSelectionIndex];
                }
                catch (ArgumentOutOfRangeException)
                {
                    return null;
                }
            }
        }

        public void Dispose()
        {
            PartialInferenceContext = null;
            Model = null;
            ModelAsset = null;
        }

        public enum LoadingState
        {
            Idle,
            LoadingModel,
            LayoutComputation,
            Done,
            Error
        }
    }
}
