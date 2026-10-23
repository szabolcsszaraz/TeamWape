using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Generators.Asset;
using Unity.AI.Toolkit;
using Unity.AI.Toolkit.Asset;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.AI.Generators.IO.Utilities
{
    /// <summary>
    /// Configures model importers (FBX via ModelImporter, GLB via glTFast) with
    /// standard settings for AI-generated 3D models.
    /// </summary>
    static class ModelImportConfiguration
    {
        static readonly Type k_GltfImporterType;

        static ModelImportConfiguration()
        {
            var editorAssembly = GetLoadedAssemblies().FirstOrDefault(a => a.GetName().Name == "glTFast.Editor");
            if (editorAssembly != null)
                k_GltfImporterType = editorAssembly.GetType("GLTFast.Editor.GltfImporter");
        }

        /// <summary>
        /// Configures any supported model importer (FBX or GLB) with standard settings.
        /// </summary>
        public static void ConfigureImporter(AssetImporter importer)
        {
            if (importer is ModelImporter modelImporter)
                ConfigureFbxImporter(modelImporter);
            else if (IsGltfImporter(importer))
                ConfigureGlbImporter(importer);
        }

        /// <summary>
        /// Configures a ModelImporter for AI-generated FBX files.
        /// </summary>
        public static void ConfigureFbxImporter(ModelImporter modelImporter)
        {
            modelImporter.importCameras = false;
            modelImporter.importLights = false;
            modelImporter.importAnimation = true;
            modelImporter.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            modelImporter.materialSearch = ModelImporterMaterialSearch.RecursiveUp;
#if UNITY_6000_6_OR_NEWER
#pragma warning disable 618 // TODO: ModelImporterMaterialLocation.External deprecated on 6000.6+; migrate to InPrefab + material extraction (tracked separately).
#endif
            modelImporter.materialLocation = ModelImporterMaterialLocation.External;
#if UNITY_6000_6_OR_NEWER
#pragma warning restore 618
#endif
            modelImporter.importBlendShapes = true;
            modelImporter.importNormals = ModelImporterNormals.Import;
            modelImporter.importTangents = ModelImporterTangents.CalculateMikk;
            modelImporter.meshCompression = ModelImporterMeshCompression.Off;
            modelImporter.optimizeMeshPolygons = true;
            modelImporter.isReadable = false;
            modelImporter.meshOptimizationFlags = MeshOptimizationFlags.Everything;

            // Bake axis conversion into mesh data so imported models have (0,0,0) rotation
            // and (1,1,1) scale. This handles the Blender -90° X rotation and the
            // (100,100,100) root scale from cm-to-m conversion.
            modelImporter.bakeAxisConversion = true;
        }

        /// <summary>
        /// Configures a glTFast importer for AI-generated GLB files.
        /// Uses reflection since glTFast is an optional dependency.
        /// </summary>
        public static void ConfigureGlbImporter(AssetImporter importer)
        {
            if (!IsGltfImporter(importer))
                return;

            var serializedObject = new SerializedObject(importer);
            const string propertyPath = "instantiationSettings.sceneObjectCreation";
            var property = serializedObject.FindProperty(propertyPath);

            if (property == null)
            {
                Debug.LogError($"Could not find SerializedProperty '{propertyPath}' on GltfImporter. The property name may have changed in a new version of glTFast.");
                return;
            }

            // SceneObjectCreation enum: 0=Never, 1=Always, 2=WhenMultipleRootNodes
            property.enumValueIndex = 1;
            if (serializedObject.ApplyModifiedProperties())
                importer.SaveAndReimport();
        }

        /// <summary>
        /// Returns true if the given importer is a ModelImporter (FBX) or a glTFast GltfImporter (GLB).
        /// </summary>
        public static bool IsModelImporter(AssetImporter importer)
        {
            return importer is ModelImporter || IsGltfImporter(importer);
        }

        /// <summary>
        /// Returns true if the given importer is a glTFast GltfImporter.
        /// </summary>
        public static bool IsGltfImporter(AssetImporter importer)
        {
            return k_GltfImporterType != null && k_GltfImporterType.IsInstanceOfType(importer);
        }

        /// <summary>
        /// Configures a temporarily imported model asset and returns an instantiated GameObject.
        /// The caller owns the returned instance and must destroy it when done.
        /// The caller also owns the TemporaryAsset.Scope lifetime (this method does not dispose it).
        /// </summary>
        public static async Task<GameObject> ConfigureAndInstantiateModelAsync(TemporaryAsset.Scope temporaryAsset)
        {
            var asset = temporaryAsset.assets[0].asset;
            var assetPath = asset.GetPath();

            // Wait for AssetImporter to be ready
            var assetImporter = AssetImporter.GetAtPath(assetPath);
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(300));
            try
            {
                while (assetImporter == null)
                {
                    cts.Token.ThrowIfCancellationRequested();
                    assetImporter = AssetImporter.GetAtPath(assetPath);
                    if (assetImporter == null)
                        await EditorTask.Yield();
                }
            }
            catch (OperationCanceledException)
            {
                Debug.LogError($"Timed out waiting for ModelImporter at path: {assetPath}");
                return null;
            }

            ConfigureImporter(assetImporter);

            AssetDatabase.WriteImportSettingsIfDirty(assetPath);
            ExecuteWithTempDisabledErrorPause(() => AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate));

            ConfigureExtractedTextures(assetPath);

            var importedGameObject = asset.GetObject<GameObject>();
            if (!importedGameObject)
            {
                Debug.LogError("No GameObject found in the imported model.");
                return null;
            }

            var instance = Object.Instantiate(importedGameObject);
            instance.hideFlags = HideFlags.HideAndDontSave;

            EnsureMeshComponents(instance, assetPath);

            return instance;
        }

        public static void ConfigureExtractedTextures(string modelPath)
        {
            var assetsDir = Path.GetDirectoryName(modelPath);
            if (string.IsNullOrEmpty(assetsDir))
                return;

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D", new[] { assetsDir });
            foreach (var guid in textureGuids)
            {
                var texPath = AssetDatabase.GUIDToAssetPath(guid);
                var fileName = Path.GetFileNameWithoutExtension(texPath).ToLower();

                if (fileName.Contains("normal") || fileName.Contains("_norm") || fileName.Contains("_nrm") || fileName.EndsWith("_n"))
                {
                    var texImporter = AssetImporter.GetAtPath(texPath) as TextureImporter;
                    if (texImporter != null && texImporter.textureType != TextureImporterType.NormalMap)
                    {
                        texImporter.textureType = TextureImporterType.NormalMap;
                        texImporter.sRGBTexture = false;
                        texImporter.SaveAndReimport();
                    }
                }
            }
        }

        static void EnsureMeshComponents(GameObject gameObject, string assetPath)
        {
            var meshAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath)
                .OfType<Mesh>()
                .ToList();

            var meshRenderers = gameObject.GetComponentsInChildren<MeshRenderer>();
            for (var i = 0; i < meshRenderers.Length; i++)
            {
                var renderer = meshRenderers[i];
                var meshFilter = renderer.GetComponent<MeshFilter>();

                if (meshFilter == null)
                    meshFilter = renderer.gameObject.AddComponent<MeshFilter>();

                if (meshFilter.sharedMesh == null && meshAssets.Count > 0)
                {
                    var meshToAssign = i < meshAssets.Count ? meshAssets[i] : meshAssets[0];
                    meshFilter.sharedMesh = meshToAssign;
                }
            }

            var allMeshFilters = gameObject.GetComponentsInChildren<MeshFilter>();
            if (allMeshFilters.Length == 0 && meshAssets.Count > 0)
            {
                var meshFilter = gameObject.AddComponent<MeshFilter>();
                gameObject.AddComponent<MeshRenderer>();
                meshFilter.sharedMesh = meshAssets[0];
            }
        }

        public static void ExecuteWithTempDisabledErrorPause(Action actionToExecute)
        {
            var isPaused = EditorApplication.isPaused;
            try { actionToExecute(); }
            finally { EditorApplication.isPaused = isPaused; }
        }

        static Assembly[] GetLoadedAssemblies()
        {
#if UNITY_6000_5_OR_NEWER
            return UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies().ToArray();
#else
            return AppDomain.CurrentDomain.GetAssemblies();
#endif
        }
    }
}
