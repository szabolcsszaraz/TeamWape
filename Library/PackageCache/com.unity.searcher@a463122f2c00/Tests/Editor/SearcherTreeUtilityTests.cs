using System.Collections.Generic;
using NUnit.Framework;

namespace UnityEditor.Searcher.Tests
{
    class SearcherTreeUtilityTests
    {
        List<SearcherItem> m_SearchTree = new List<SearcherItem>();

        const string item0Synonym = "The Company of the Ring";
        const string item0Help = "The Fellowship of the Ring is the first of three volumes of the epic novel The Lord of the Rings by the English author J. R. R. Tolkien.";
        const string item1Synonym = "Orthanc and Barad-dur";
        const string item1Help = "The Two Towers, first published in 1954, is the second volume of J. R. R. Tolkien's high fantasy novel The Lord of the Rings.";
        const string item4Help = "A book about improving one self.";
        const string item5Synonym = "Unknown";
        [OneTimeSetUp]
        public void Init()
        {
            var item0 = new SearcherItem("Fantasy/J. R. R. Tolkien/The Fellowship of the Ring");
            item0.Synonyms = new string[] { item0Synonym };
            item0.Help = item0Help;
            
            var item1 = new SearcherItem("Fantasy/J. R. R. Tolkien/The Two Towers", userData: 5);
            item1.Synonyms = new string[] { item1Synonym };
            item1.Help = item1Help;

            var item2 = new SearcherItem("Fantasy/J. R. R. Tolkien/The Return of the King");
            var item3 = new SearcherItem("Fantasy/Dragonlance/Dragons of Winter Night");

            var item4 = new SearcherItem("Health & Fitness/Becoming a Supple Leopard");
            item4.Help = item4Help;

            var item5 = new SearcherItem("Some Uncategorized Book", userData: 2);
            item5.Synonyms = new string[] { item5Synonym };
            
            List<SearcherItem> items = new List<SearcherItem>();
            items.Add(item0);
            items.Add(item1);
            items.Add(item2);
            items.Add(item3);
            items.Add(item4);
            items.Add(item5);

            m_SearchTree = SearcherTreeUtility.CreateFromFlatList(items);
        }

        [OneTimeTearDown]
        public void Cleanup()
        {
        }

        [Test]
        public void ValidateSearcherTreeUtilityTests()
        {
            Assert.AreEqual(3, m_SearchTree.Count);
            Assert.AreEqual(2, m_SearchTree[0].Children.Count);
            Assert.AreEqual(3, m_SearchTree[0].Children[0].Children.Count);
            Assert.AreEqual(1, m_SearchTree[0].Children[1].Children.Count);
            Assert.AreEqual(1, m_SearchTree[1].Children.Count);
            Assert.AreEqual(0, m_SearchTree[1].Children[0].Children.Count);
            Assert.AreEqual("Fantasy", m_SearchTree[0].Name);
            Assert.AreEqual("J. R. R. Tolkien", m_SearchTree[0].Children[0].Name);
            Assert.AreEqual("The Fellowship of the Ring", m_SearchTree[0].Children[0].Children[0].Name);
            Assert.AreEqual("The Two Towers", m_SearchTree[0].Children[0].Children[1].Name);
            Assert.AreEqual("The Return of the King", m_SearchTree[0].Children[0].Children[2].Name);
            Assert.AreEqual("Dragonlance", m_SearchTree[0].Children[1].Name);
            Assert.AreEqual("Dragons of Winter Night", m_SearchTree[0].Children[1].Children[0].Name);

            Assert.AreEqual("Health & Fitness", m_SearchTree[1].Name);
            Assert.AreEqual("Becoming a Supple Leopard", m_SearchTree[1].Children[0].Name);
            Assert.AreEqual("Some Uncategorized Book", m_SearchTree[2].Name);

            Assert.AreNotEqual("Fantasy", m_SearchTree[2].Name);
            Assert.AreNotEqual("Some Uncategorized Book", m_SearchTree[0].Children[0].Children[0].Name);

            // Change for User Data:
            Assert.AreEqual(5, m_SearchTree[0].Children[0].Children[1].UserData);
            Assert.AreEqual(2, m_SearchTree[2].UserData);
            Assert.AreEqual(null, m_SearchTree[0].UserData);
            Assert.AreEqual(null, m_SearchTree[0].Children[0].Children[2].UserData);
            
            // Synonyms and Help
            Assert.AreEqual(new string[] { item0Synonym }, m_SearchTree[0].Children[0].Children[0].Synonyms);
            Assert.AreEqual(item0Help, m_SearchTree[0].Children[0].Children[0].Help);
            Assert.AreEqual(new string[] { item1Synonym }, m_SearchTree[0].Children[0].Children[1].Synonyms);
            Assert.AreEqual(item1Help, m_SearchTree[0].Children[0].Children[1].Help);
            Assert.AreEqual(null, m_SearchTree[0].Children[0].Children[2].Synonyms);
            Assert.AreEqual("", m_SearchTree[0].Children[0].Children[2].Help);
            Assert.AreEqual(null, m_SearchTree[0].Children[1].Children[0].Synonyms);
            Assert.AreEqual("", m_SearchTree[0].Children[1].Children[0].Help);
            Assert.AreEqual(null, m_SearchTree[1].Children[0].Synonyms);
            Assert.AreEqual(item4Help, m_SearchTree[1].Children[0].Help);
            Assert.AreEqual(new string[] { item5Synonym }, m_SearchTree[2].Synonyms);
            Assert.AreEqual("", m_SearchTree[2].Help);
        }
    }
}
