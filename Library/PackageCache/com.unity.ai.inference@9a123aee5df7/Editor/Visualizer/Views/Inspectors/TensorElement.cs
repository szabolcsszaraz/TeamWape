using System;
using Unity.AppUI.Redux;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.Extensions;
using Unity.InferenceEngine.Editor.Visualizer.LayerAnalysis;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Manipulators;
using UnityEngine.UIElements;
using TextField = Unity.AppUI.UI.TextField;

namespace Unity.InferenceEngine.Editor.Visualizer.Views.Inspectors
{
    class TensorElement : InputLabel
    {
        readonly GraphStoreManager m_StoreManager;

        public TensorElement(GraphStoreManager storeManager, LayerConnectionsHandler.LayerConnectionData tensorData)
        {
            m_StoreManager = storeManager;

            label = !string.IsNullOrEmpty(tensorData.name) ? $"{tensorData.name}: \"{tensorData.sentisIndex}\"" : $"\"{tensorData.sentisIndex}\": ";
            tooltip = tensorData.name;
            direction = Direction.Horizontal;
            contentContainer.style.flexDirection = FlexDirection.Row;
            contentContainer.style.alignContent = Align.FlexStart;

            var state = m_StoreManager.Store.GetState<GraphState>(GraphSlice.Name);
            var model = state.Model;
            var partialTensor = state.PartialInferenceContext.GetPartialTensor(tensorData.sentisIndex);

            var text = string.Empty;

            if (partialTensor == null)
            {
                text = "null";
            }
            else
            {
                text = model.PartialTensorToString(partialTensor);

                if (model.IsConstant(tensorData.sentisIndex))
                {
                    text = text.Insert(0, "Const, ");
                }
            }

            var textArea = new TextField(text)
            {
                isReadOnly = true,
                style =
                {
                    flexGrow = 1,
                    flexShrink = 1
                }
            };

            Add(textArea);

            var iconButton = new IconButton
            {
                icon = "squares-four",
                tooltip = "Inspect Tensor",
                quiet = true,
                enabledSelf = tensorData.sentisIndex >= 0
            };

            iconButton.clickable.clicked += () =>
            {
                m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(tensorData.sentisIndex));
            };

            Add(iconButton);

            this.AddManipulator(new HoverManipulator(m_StoreManager, tensorData.sentisIndex));
        }
    }
}
