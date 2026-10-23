using System;
using System.Linq;
#if GLTFAST_AVAILABLE
using GLTFast;
#endif
using Unity.AI.Generators.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Mesh.Services.Utilities
{
    class GltfImporterProxy
    {
        static readonly Type k_GltfImporterType;

        readonly AssetImporter m_GltfImporter;

        static GltfImporterProxy()
        {
            // glTFast is an optional dependency. Absence is a valid state; callers gate on IsGltfImporter.
            var editorAssembly = AssemblyUtils.GetLoadedAssemblies().FirstOrDefault(a => a.GetName().Name == "glTFast.Editor");
            k_GltfImporterType = editorAssembly?.GetType("GLTFast.Editor.GltfImporter");
        }

        public GltfImporterProxy(AssetImporter importer)
        {
            if (importer == null) throw new ArgumentNullException(nameof(importer));
            if (!IsGltfImporter(importer)) throw new ArgumentException("Importer is not a GltfImporter.", nameof(importer));

            m_GltfImporter = importer;
        }

        public static bool IsGltfImporter(AssetImporter importer)
        {
            return k_GltfImporterType != null && k_GltfImporterType.IsInstanceOfType(importer);
        }

#if GLTFAST_AVAILABLE
        public void SetSceneObjectCreation(SceneObjectCreation value)
        {
            var serializedObject = new SerializedObject(m_GltfImporter);

            // The property name is derived directly from the .meta file's structure.
            const string propertyPath = "instantiationSettings.sceneObjectCreation";
            var property = serializedObject.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError($"Could not find SerializedProperty '{propertyPath}' on GltfImporter. The property name may have changed in a new version of glTFast.");
                return;
            }

            property.enumValueIndex = (int)value;
            if (serializedObject.ApplyModifiedProperties())
            {
                m_GltfImporter.SaveAndReimport();
            }
        }
#endif
    }
}
