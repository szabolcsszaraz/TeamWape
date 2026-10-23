using System;
using System.Collections.Generic;
using Unity.AppUI.Redux;

namespace Unity.InferenceEngine.Editor.Visualizer.StateManagement
{
    static class GraphReducers
    {
        public static GraphState SetFocusedNode(GraphState state, IAction<FocusData> action)
        {
            return state with { FocusedData = action.payload };
        }

        public static GraphState SetSelectedObject(GraphState state, IAction<object> @object)
        {
            var newState = state with {};

            if (@object.payload is null)
            {
                newState.CurrentSelectionIndex = -1;
                return newState;
            }

            if (newState.SelectionHistory.Count > 0 && @object.payload == newState.SelectionHistory[^ 1])
                return newState with { CurrentSelectionIndex = newState.SelectionHistory.Count - 1 };

            if (newState.CurrentSelectionIndex >= 0 && newState.CurrentSelectionIndex < newState.SelectionHistory.Count - 1)
                newState.SelectionHistory.RemoveRange(newState.CurrentSelectionIndex + 1, newState.SelectionHistory.Count - newState.CurrentSelectionIndex - 1);

            newState.SelectionHistory.Add(@object.payload);
            newState.CurrentSelectionIndex = newState.SelectionHistory.Count - 1;

            return newState;
        }

        public static GraphState MoveStackIndexUp(GraphState state, IAction _)
        {
            if (state.CurrentSelectionIndex < state.SelectionHistory.Count - 1)
                return state with { CurrentSelectionIndex = state.CurrentSelectionIndex + 1 };

            return state;
        }

        public static GraphState MoveStackIndexDown(GraphState state, IAction _)
        {
            if (state.CurrentSelectionIndex > 0)
                return state with { CurrentSelectionIndex = state.CurrentSelectionIndex - 1 };

            return state;
        }

        public static GraphState AddHoveredObject(GraphState state, IAction<object> @object)
        {
            if (@object.payload is null)
                return state;

            var newState = state with {};
            newState.HoveredObjects = new List<object>();

            newState.HoveredObjects.Add(@object.payload);
            return newState;
        }

        public static GraphState RemoveHoveredObject(GraphState state, IAction<object> @object)
        {
            var newState = state with {};
            newState.HoveredObjects.Remove(@object.payload);
            newState.HoveredObjects = new List<object>(state.HoveredObjects);
            return newState;
        }

        public static GraphState UpdateLoadingState(GraphState state, IAction<GraphState.LoadingState> loadingState)
        {
            return state with { LoadingStatus = loadingState.payload };
        }
    }
}
