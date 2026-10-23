using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using Unity.AI.Generators.Contexts;
using Unity.AI.Generators.Redux;
using Unity.AI.Generators.UIElements.Extensions;
using Unity.AI.Mesh.Components;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Mesh.Windows
{
    class MeshSettingsWindow : EditorWindow
    {
        IStore m_Store;
        VisualElement m_Element;

        TaskCompletionSource<bool> m_TaskCompletionSource;
        MeshSettings m_View;

        public static async Task Open(IStore store, AssetReference asset, VisualElement element)
        {
            var window = EditorWindowExtensions.CreateWindow<MeshSettingsWindow>(store, asset, "Export Settings", false);

            var tcs = new TaskCompletionSource<bool>();
            window.m_TaskCompletionSource?.TrySetResult(false);
            window.m_TaskCompletionSource = tcs;
            window.m_Store = store;
            window.m_Element = element;

            window.minSize = new Vector2(256, 92);
            window.maxSize = new Vector2(256, 92);
            window.ShowAuxWindow();

            await tcs.Task;
        }

        void CreateGUI()
        {
            rootVisualElement.Clear();
            m_View = new MeshSettings();
            m_View.ProvideContext(StoreExtensions.storeKey, m_Store);
            rootVisualElement.Add(m_View);
        }

        void OnDestroy() => m_TaskCompletionSource?.TrySetResult(true);
    }
}