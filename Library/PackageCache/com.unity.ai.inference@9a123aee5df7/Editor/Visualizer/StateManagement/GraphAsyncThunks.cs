using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Compiler.Analyser;
using Unity.InferenceEngine.Editor.Visualizer.GraphData;
using Unity.InferenceEngine.Editor.Visualizer.Views;
using UnityEngine;

namespace Unity.InferenceEngine.Editor.Visualizer.StateManagement
{
    static class GraphAsyncThunks
    {
        public static async Task<GraphState> LoadAndComputeGraph(IThunkAPI<GraphState> thunkAPI)
        {
            if (thunkAPI.store is not IStore<PartitionedState> store)
            {
                const string message = "Store is null or of incorrect type";
                thunkAPI.RejectWithValue(new GraphState { ErrorMessage = message, LoadingStatus = GraphState.LoadingState.Error });
                throw new InvalidOperationException(message);
            }

            store.Dispatch(GraphStoreManager.UpdateLoadingState.Invoke(GraphState.LoadingState.LoadingModel));

            var state = store.GetState<GraphState>(GraphSlice.Name);
            var modelAsset = state.ModelAsset;


            // Phase 1: Load model + partial inference
            await Task.Run(() =>
            {
                state = LoadModel(thunkAPI, modelAsset, state);
            }, thunkAPI.cancellationToken);

            // Phase 2: Prepare layout
            store.Dispatch(GraphStoreManager.UpdateLoadingState.Invoke(GraphState.LoadingState.LayoutComputation));

            // Clear previous node/edge data to avoid stale state from previous iterations
            state.Nodes.Clear();
            state.Edges.Clear();

            using var graphHandler = new GraphLayoutHandler(state);

            graphHandler.InitializeNodes();

            state.GraphView.InitializeNodeViews();

            var nodeViews = state.GraphView.NodeViews;

            // Phase 3: Measure nodes
            foreach (var nodeKvp in nodeViews)
            {
                await MeasureNode(thunkAPI, nodeKvp, state);
            }

            // Phase 4: Compute layout
            await Task.Run(() =>
            {
                ComputeLayout(thunkAPI, graphHandler, state);
            }, thunkAPI.cancellationToken);

            // Phase 5: Build view edges and positions
            state.GraphView.CreateEdges();

            state.GraphView.UpdateNodesCanvasPosition();

            return state with { LoadingStatus = GraphState.LoadingState.Done };
        }

        static GraphState LoadModel(IThunkAPI<GraphState> thunkAPI, ModelAsset modelAsset, GraphState state)
        {
            try
            {
                if (thunkAPI.cancellationToken.IsCancellationRequested)
                {
                    thunkAPI.cancellationToken.ThrowIfCancellationRequested();
                }

                if (modelAsset == null)
                {
                    throw new InvalidOperationException("ModelAsset is null");
                }

                var model = ModelLoader.Load(modelAsset);

                var partialInferenceContext = PartialInferenceAnalysis.InferModelPartialTensors(model);

                state = state with { ModelAsset = modelAsset, Model = model, PartialInferenceContext = partialInferenceContext };
            }
            catch (OperationCanceledException oce)
            {
                thunkAPI.RejectWithValue(state with { ErrorMessage = $"Canceled loading model: {oce.Message}", LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }
            catch (Exception e)
            {
                thunkAPI.RejectWithValue(state with { ErrorMessage = $"Error loading model: {e.Message}", LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }

            return state;
        }

        static async Task MeasureNode(IThunkAPI<GraphState> thunkAPI, KeyValuePair<NodeData, NodeView> nodeKvp, GraphState state)
        {
            var node = nodeKvp.Key;
            if (node == null)
            {
                var message = "NodeData is null during measurement";
                thunkAPI.RejectWithValue(state with { ErrorMessage = message, LoadingStatus = GraphState.LoadingState.Error });
                throw new ArgumentNullException(message);
            }

            var nodeView = nodeKvp.Value;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5d));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, thunkAPI.cancellationToken);

            var nodeLabel = node.Name;

            try
            {
                var worldBound = await nodeView.GetWorldBound(linkedCts.Token);
                node.CanvasSize = new Vector2(worldBound.width, worldBound.height);
            }
            catch (OperationCanceledException e)
            {
                var wasTimeout = cts.IsCancellationRequested && !thunkAPI.cancellationToken.IsCancellationRequested;
                var reason = wasTimeout ? "timeout" : "canceled";
                var errorMessage = $"Layout computation failed for node {nodeLabel}: {reason}: {e.Message}";
                thunkAPI.RejectWithValue(state with { ErrorMessage = errorMessage, LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }
            catch (Exception ex)
            {
                thunkAPI.RejectWithValue(state with { ErrorMessage = $"Layout computation failed for node {nodeLabel}: {ex.Message}", LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }
        }

        static void ComputeLayout(IThunkAPI<GraphState> thunkAPI, GraphLayoutHandler graphHandler, GraphState state)
        {
            try
            {
                if (thunkAPI.cancellationToken.IsCancellationRequested)
                {
                    thunkAPI.cancellationToken.ThrowIfCancellationRequested();
                }

                graphHandler.ComputeLayout();
            }
            catch (OperationCanceledException oce)
            {
                thunkAPI.RejectWithValue(state with { ErrorMessage = $"Layout computation canceled: {oce.Message}", LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }
            catch (Exception ex)
            {
                thunkAPI.RejectWithValue(state with { ErrorMessage = $"Error during layout computation: {ex.Message}", LoadingStatus = GraphState.LoadingState.Error });
                throw;
            }
        }
    }
}