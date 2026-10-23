using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Multiplayer.Center.Editor.Tests
{
    class MultiplayerCenterWindowReferencesTests
    {
        const string k_MultiplayerCenterWindowPath =
            "Packages/com.unity.multiplayer.center/Editor/MultiplayerCenterWindow/MultiplayerCenterWindow.cs";
        MonoImporter m_Importer;

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            m_Importer = (MonoImporter)AssetImporter.GetAtPath(k_MultiplayerCenterWindowPath);
        }

        [TestCaseSource(nameof(FindMultiplayerCenterWindowReferences))]
        public void WindowInstance_HasValidReferences(string fieldName)
        {
            var reference = m_Importer.GetDefaultReference(fieldName);
            Assert.That(reference, Is.Not.Null);
        }

        static IEnumerable<TestCaseData> FindMultiplayerCenterWindowReferences()
        {
            var fields = AssetDatabase.LoadAssetAtPath<MonoScript>(k_MultiplayerCenterWindowPath).GetClass()
                .GetFields(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.NonPublic |
                           BindingFlags.Public);
            foreach (var field in fields)
            {
                if ((field.IsPublic || field.IsDefined(typeof(SerializeField), true)) &&
                    (field.FieldType.IsSubclassOf(typeof(Object)) ||
                     field.FieldType == typeof(Object)))
                {
                    yield return new TestCaseData(field.Name);
                }
            }
        }
    }
}
