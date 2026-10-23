using System;
using System.Collections.Generic;
using System.Linq;
using Unity.AppUI.UI;
using Unity.InferenceEngine.Editor.Visualizer.StateManagement;
using Unity.InferenceEngine.Editor.Visualizer.Views.Finder;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.InferenceEngine.Editor.Visualizer.Views
{
    class GraphFinder : VisualElement
    {
        readonly GraphStoreManager m_StoreManager;
        readonly SearchBar m_SearchBar;
        readonly ListView m_SearchResults;
        readonly GraphView m_Graph;

        const string k_StylePath = "Packages/com.unity.ai.inference/Editor/Visualizer/Styles/GraphFinder.uss";

        public SearchBar searchBar => m_SearchBar;

        public GraphFinder(GraphView graph, GraphStoreManager storeManager)
        {
            m_StoreManager = storeManager;
            pickingMode = PickingMode.Ignore;

            m_Graph = graph;

            m_SearchBar = new SearchBar();
            m_SearchBar.AddToClassList("search-bar");
            m_SearchBar.RegisterValueChangingCallback(OnValueChanging);
            m_SearchBar.placeholder = "Search nodes & related index";
            Add(m_SearchBar);

            m_SearchResults = new ListView();
            m_SearchResults.AddToClassList("search-results");
            m_SearchResults.showAlternatingRowBackgrounds = AlternatingRowBackground.None;
            m_SearchResults.showBorder = true;
            m_SearchResults.selectionType = SelectionType.Single;
            m_SearchResults.style.flexGrow = 1;
            m_SearchResults.horizontalScrollingEnabled = true;
            m_SearchResults.makeItem = MakeItem;
            m_SearchResults.bindItem = BindItem;
            m_SearchResults.selectionChanged += OnSelectionChanged;

            //We prevent the event to bubble up since it is used as dismiss for the inspector
            m_SearchResults.RegisterCallback<PointerUpEvent>(evt => evt.StopImmediatePropagation());
            hierarchy.Add(m_SearchResults);

            AddToClassList("graph-finder");
            styleSheets.Add(AssetDatabase.LoadAssetAtPath<StyleSheet>(k_StylePath));

            SearchForString(null);
        }

        void OnSelectionChanged(IEnumerable<object> obj)
        {
            if (obj.FirstOrDefault() is NodeView nodeView)
            {
                m_StoreManager.Store.Dispatch(GraphStoreManager.SetSelectedObject.Invoke(nodeView.nodeData));
            }
        }

        void BindItem(VisualElement visualElement, int index)
        {
            if (m_SearchResults.itemsSource[index] is NodeView nodeView)
            {
                var finderElement = visualElement as FinderElement;
                finderElement?.BindItem(m_StoreManager, nodeView.nodeData);
            }
        }

        VisualElement MakeItem()
        {
            var label = new FinderElement
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleLeft
                }
            };

            return label;
        }

        void OnValueChanging(ChangingEvent<string> evt)
        {
            SearchForString(evt.newValue);
        }

        void SearchForString(string searchString)
        {
            m_SearchResults.itemsSource = null;
            m_SearchResults.selectedIndex = -1;
            m_SearchResults.style.display = DisplayStyle.None;

            if (string.IsNullOrEmpty(searchString))
                return;

            var itemSource = new List<NodeView>();
            var lowerSearchString = searchString.ToLower();

            foreach (var nodeView in m_Graph.NodeViews.Values)
            {
                if (int.TryParse(searchString, out var intValue) && (nodeView.nodeData.SentisIndex == intValue ||
                                                                     nodeView.nodeData.Inputs.Any(i => i.SentisIndex == intValue) ||
                                                                     nodeView.nodeData.Outputs.Any(i => i.SentisIndex == intValue)))
                {
                    itemSource.Add(nodeView);
                    continue;
                }

                if (nodeView.nodeData.Name.ToLower().Contains(lowerSearchString))
                {
                    itemSource.Add(nodeView);
                }
            }

            m_SearchResults.itemsSource = itemSource;
            if (itemSource.Count > 0)
                m_SearchResults.style.display = DisplayStyle.Flex;
        }
    }
}
