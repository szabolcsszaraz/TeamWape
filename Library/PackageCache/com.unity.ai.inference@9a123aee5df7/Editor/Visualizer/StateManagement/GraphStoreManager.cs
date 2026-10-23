using System;
using Unity.AppUI.Redux;
using Unity.InferenceEngine.Editor.Visualizer.Views;

namespace Unity.InferenceEngine.Editor.Visualizer.StateManagement
{
    sealed class GraphStoreManager : IDisposable
    {
        public IStore<PartitionedState> Store { get; }
        public static ActionCreator<FocusData> SetFocusedObject = new(GraphSlice.SetFocusedObject);
        public static ActionCreator<object> SetSelectedObject = new(GraphSlice.SetSelectedObject);
        public static ActionCreator MoveStackIndexUp = new(GraphSlice.MoveStackIndexUp);
        public static ActionCreator MoveStackIndexDown = new(GraphSlice.MoveStackIndexDown);
        public static ActionCreator<object> AddHoveredObject = new(GraphSlice.AddHoveredObject);
        public static ActionCreator<object> RemoveHoveredObject = new(GraphSlice.RemoveHoveredObject);
        public static ActionCreator<GraphState.LoadingState> UpdateLoadingState = new(GraphSlice.UpdateLoadingState);
        public static readonly AsyncThunkCreator<GraphState> ComputeGraph = new(GraphSlice.ComputeGraph, GraphAsyncThunks.LoadAndComputeGraph);

        public GraphStoreManager(ModelAsset modelAsset, GraphView graphView)
        {
            var slice = StoreFactory.CreateSlice(
                GraphSlice.Name,
                new GraphState { ModelAsset = modelAsset, GraphView = graphView },
                builder =>
                {
                    builder.AddCase(SetFocusedObject, GraphReducers.SetFocusedNode);
                    builder.AddCase(SetSelectedObject, GraphReducers.SetSelectedObject);
                    builder.AddCase(MoveStackIndexUp, GraphReducers.MoveStackIndexUp);
                    builder.AddCase(MoveStackIndexDown, GraphReducers.MoveStackIndexDown);
                    builder.AddCase(AddHoveredObject, GraphReducers.AddHoveredObject);
                    builder.AddCase(RemoveHoveredObject, GraphReducers.RemoveHoveredObject);
                    builder.AddCase(UpdateLoadingState, GraphReducers.UpdateLoadingState);
                    builder.AddCase(ComputeGraph.fulfilled, (_, action) => action.payload);
                    builder.AddCase(ComputeGraph.rejected, (_, action) =>
                    {
                        Debug.LogError(action.payload.ErrorMessage);
                        return action.payload;
                    });
                });

            Store = StoreFactory.CreateStore(new[] { slice });

            var action = ComputeGraph.Invoke();
            _ = Store.DispatchAsyncThunk(action);
        }

        public void Dispose()
        {
            var state = Store.GetState<GraphState>(GraphSlice.Name);
            state.Dispose();
            Store?.Dispose();
        }
    }
}
