using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;

namespace Unity.GraphAuthoring.Editor.ProviderSystem
{
    internal class ShaderReflectionAssetPostProcessor : AssetPostprocessor
    {
        internal static void PopulateFromFiles(ProviderLibrary lib)
        {
            foreach (var guid in AssetDatabase.FindAssetGUIDs("t: ShaderInclude"))
                AnalyzeFile(lib, guid);
        }

        internal static bool AnalyzeFile(ProviderLibrary lib, GUID assetID)
        {
            lib.ClearByAssetID(assetID);

            var shaderInclude = AssetDatabase.LoadAssetByGUID<ShaderInclude>(assetID);
            if (shaderInclude == null)
                return false;

            var reflection = shaderInclude.Reflection;
            if (reflection == null)
                return false;

            foreach (var func in reflection.ReflectedFunctions)
                lib.TryAdd(new ReflectedFunctionProvider(assetID, func));

            return true;
        }

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            // This asset post processor isn't meaningful unless the ProviderLibrary is already initialized.
            if (!ProviderLibrary.IsInitialized || !ProviderLibrary.TryGetInstance(out var lib))
                return;

            var modifiedFiles = new List<GUID>();

            // ProviderLibrary tracks GUIDs whereas this hook only provides paths;
            // for deleted assets, we have no way of determining their GUIDs, so we can just test which are orphaned.
            foreach (var assetID in new List<GUID>(lib.GetTrackedAssetIDs()))
            {
                if (!string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(assetID)))
                    continue;

                lib.ClearByAssetID(assetID);
                modifiedFiles.Add(assetID);
            }

            foreach (string path in importedAssets)
            {
                // We are really looking for ShaderInclude assets.
                if (Path.GetExtension(path).ToLower() != ".hlsl")
                    continue;

                var assetID = AssetDatabase.GUIDFromAssetPath(path);
                if (AnalyzeFile(lib, assetID))
                    modifiedFiles.Add(assetID);
            }
            if (modifiedFiles.Count > 0)
                ProviderLibrary.NotifyProvidersReloaded(modifiedFiles);
        }
    }
}
