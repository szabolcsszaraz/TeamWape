using Unity.AI.Generators.Redux;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Image.Services.Stores.Selectors;
using Unity.AI.Image.Services.Utilities;
using Unity.AI.ModelSelector.Services.Stores.Actions.Payloads;
using Unity.AI.Generators.Redux.Thunks;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Components
{
    [UxmlElement]
    partial class ModelSelectorButton : VisualElement
    {
        const string k_Uxml = "Packages/com.unity.ai.assistant/modules/Unity.AI.Image/Components/ModelSelectorButton/ModelSelectorButton.uxml";

        [UxmlAttribute]
        public bool enabled
        {
            get => m_Enabled;
            set
            {
                m_Enabled = value;
                m_Button.SetEnabled(m_Enabled);
            }
        }

        bool m_Enabled = true;

        readonly Button m_Button;

        public ModelSelectorButton()
        {
            var tree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(k_Uxml);
            tree.CloneTree(this);

            m_Button = this.Q<Button>();
            // ReSharper disable once AsyncVoidLambda
            m_Button.clickable = new Clickable(async () => {
                if (!m_Button.enabledSelf)
                    return;
                try
                {
                    m_Button.SetEnabled(false);
                    await this.GetStoreApi().Dispatch(GenerationSettingsActions.openSelectModelPanel, this);
                }
                finally
                {
                    m_Button.SetEnabled(m_Enabled);
                }
            });
            this.UseStoreApi(DiscoverModels);
            this.Use(state => state.SelectShouldAutoAssignModel(this), payload =>
            {
                m_Button.SetEnabled(!payload.should);
                // An empty model list (e.g. no active AI subscription) also disables the button
                // (UUM-150582); explain the dead end rather than implying a model is auto-assigned.
                // hasModels rides in the subscribed payload: same modality-filtered query as
                // `should`, and both re-fire together on any model-list change. timestamp stays 0
                // until the first model discovery completes, so an empty list in that window means
                // "still loading", not a missing entitlement.
                if (!payload.should)
                    m_Button.tooltip = "Choose another AI model";
                else if (payload.hasModels)
                    m_Button.tooltip = "No additional model currently available";
                else if (payload.timestamp == 0)
                    m_Button.tooltip = "Looking for available AI models...";
                else
                    m_Button.tooltip = "No models available. An active Unity AI subscription may be required.";
            });
        }

        static async void DiscoverModels(IStoreApi store)
        {
            using var editorFocus = new EditorAsyncKeepAliveScope("Discovering AI Models for image.");
            await store.Dispatch(ModelSelector.Services.Stores.Actions.ModelSelectorActions.discoverModels, new DiscoverModelsData(WebUtils.selectedEnvironment));
        }
    }
}
