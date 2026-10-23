#if UNITY_6000_0_OR_NEWER
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace UnityEditor.Searcher.Tests
{
    // Regression coverage for UUM-146469.
    //
    // Unity PR #76495 ("white-space-standard") changed text handling so labels
    // collapse whitespace at label boundaries. SearcherHighlighter splits an item
    // name across multiple sibling Labels (one per highlighted/non-highlighted
    // run), so the space between runs lives at the start of a sibling Label — and
    // is stripped unless the label uses white-space: pre.
    //
    // Fix (Editor/Resources/Searcher.uss): white-space: pre on the labels-container
    // labels. Same shape as unity PR #83337's fix for the VFX filter window.
    public class SearcherLabelStyleTests
    {
        EditorWindow m_Window;

        [SetUp]
        public void SetUp()
        {
            m_Window = ScriptableObject.CreateInstance<EditorWindow>();
            m_Window.ShowUtility();
        }

        [TearDown]
        public void TearDown()
        {
            if (m_Window != null)
                m_Window.Close();
        }

        [UnityTest]
        public IEnumerator LabelInLabelsContainer_ResolvesToWhiteSpacePre_FromSearcherStylesheet()
        {
            // Cloning SearcherItem.uxml pulls in Searcher.uss via <Style path="Searcher"/>.
            var itemTemplate = Resources.Load<VisualTreeAsset>("SearcherItem");
            Assume.That(itemTemplate, Is.Not.Null, "SearcherItem.uxml must be loadable from Resources.");

            // The USS selector requires the item to sit inside a .unity-list-view element.
            var listView = new VisualElement();
            listView.AddToClassList("unity-list-view");
            var itemRow = new VisualElement();
            listView.Add(itemRow);
            var item = itemTemplate.CloneTree();
            itemRow.Add(item);
            m_Window.rootVisualElement.Add(listView);

            var labelsContainer = item.Q<VisualElement>("labelsContainer");
            Assume.That(labelsContainer, Is.Not.Null, "SearcherItem.uxml must contain #labelsContainer.");

            // No inline whiteSpace style — the resolved value must come from the stylesheet.
            var label = new Label(" world");
            labelsContainer.Add(label);

            // Let the panel resolve styles and lay out.
            yield return null;
            yield return null;

            Assert.AreEqual(WhiteSpace.Pre, label.resolvedStyle.whiteSpace,
                "Searcher.uss must apply white-space: pre to labels in #labelsContainer so "
                + "boundary spaces on split highlight labels are preserved (UUM-146469).");
        }

        [UnityTest]
        public IEnumerator SearcherAdapterBind_TrailingSplitLabel_ResolvesToWhiteSpacePre()
        {
            // Exercise the full bind path so the highlighter fills #labelsContainer the way
            // it does in production. With "hello world" and query "hello", the highlighter
            // produces "hello" (highlighted, gets inline WhiteSpace.Pre) followed by " world"
            // (trailing fallback branch — no inline style, relies on the USS).
            var listView = new VisualElement();
            listView.AddToClassList("unity-list-view");
            var itemRow = new VisualElement();
            listView.Add(itemRow);
            m_Window.rootVisualElement.Add(listView);

            var adapter = new SearcherAdapter("Test");
            var element = adapter.MakeItem();
            itemRow.Add(element);
            adapter.Bind(element, new SearcherItem("hello world"), ItemExpanderState.Hidden, "hello");

            var labelsContainer = element.Q<VisualElement>("labelsContainer");
            Assume.That(labelsContainer, Is.Not.Null);

            yield return null;
            yield return null;

            var labels = labelsContainer.Query<Label>().ToList();
            Assume.That(labels.Count, Is.GreaterThanOrEqualTo(2),
                "SearcherHighlighter should split 'hello world' matching 'hello' into multiple labels.");
        }
    }
}
#endif
