using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Utils;
using Unity.AI.Assistant.Utils.Perf;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Search.Editor.Knowledge
{
    /// <summary>
    /// AssetPostprocessor that feeds asset changes into the AssetKnowledgeQueue
    /// for later processing by the knowledge generation system.
    /// </summary>
    class AssetChangeDetector : AssetPostprocessor
    {
        const int k_DependencyBatchSize = 100;

        static async Task OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths,
            bool didDomainReload)
        {
            if (!AssetKnowledgeSettings.SearchUsable)
                return;

            InternalLog.Log("[AssetChangeDetector] Starting asset change detection for knowledge processing...",
                LogFilter.Search);

            var sw = System.Diagnostics.Stopwatch.StartNew();

            if (AssetKnowledgeSettings.RunAsync)
                await Task.Yield();

            // Do not run this during Unity's loading screen, we do not want to block the main thread there:
            if (EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
            {
                await Task.Yield();
            }

            KnowledgeQueue.instance.EnqueueByPath(importedAssets, deletedAssets);

            var assetToDeps = await BuildDependencyMap();

            // Find assets depending on imported and deleted assets and enqueue them for processing:
            var dependenciesToImport = new HashSet<string>();
            dependenciesToImport.UnionWith(FindReferencingAssets(importedAssets, assetToDeps));
            dependenciesToImport.UnionWith(FindReferencingAssets(deletedAssets, assetToDeps));

            if (dependenciesToImport.Count > 0)
            {
                // Remove already processed assets:
                dependenciesToImport.ExceptWith(importedAssets);
                dependenciesToImport.ExceptWith(deletedAssets);

                // Force process these assets because the hash may not change but the embeddings could be affected:
                KnowledgeQueue.instance.EnqueueModifiedByPath(dependenciesToImport.ToArray(), true);
            }

            sw.Stop();

            InternalLog.Log(
                $"[AssetChangeDetector] Finished asset change detection for knowledge processing. (Time taken: {sw.Elapsed})",
                LogFilter.Search);
        }

        static async Task<Dictionary<string, string[]>> BuildDependencyMap()
        {
            var startTicks = AssistantPerf.Now;

            if (AssetKnowledgeSettings.RunAsync)
                await Task.Yield();

            string[] allAssetPaths;
            using (AssistantPerf.Measure(PerfProbe.AssetDependencyMapBuild))
            {
                // Cache dependencies for all assets
                allAssetPaths = AssetDatabase.FindAssets("t:GameObject t:Material t:Texture t:prefab", new[] { "Assets" })
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .ToArray();
            }

            var assetToDeps = new Dictionary<string, string[]>();

            for (var batchStart = 0; batchStart < allAssetPaths.Length; batchStart += k_DependencyBatchSize)
            {
                if (AssetKnowledgeSettings.RunAsync)
                    await Task.Yield();

                var batchEnd = Math.Min(batchStart + k_DependencyBatchSize, allAssetPaths.Length);

                using (AssistantPerf.Measure(PerfProbe.AssetDependencyMapBuild))
                {
                    for (var i = batchStart; i < batchEnd; i++)
                    {
                        var assetPath = allAssetPaths[i];
                        assetToDeps[assetPath] = AssetDatabase.GetDependencies(assetPath, true);
                    }
                }
            }

            AssistantPerf.MarkElapsed("search.dependency_map_wall_clock",
                "assets=" + allAssetPaths.Length + (AssetKnowledgeSettings.RunAsync ? ";async" : ";sync"), startTicks);

            return assetToDeps;
        }

        /// <summary>
        /// Finds all assets that reference any of the given asset paths.
        /// </summary>
        static IEnumerable<string> FindReferencingAssets(IEnumerable<string> assetPaths,
            Dictionary<string, string[]> assetToDeps)
        {
            var targetSet = new HashSet<string>(assetPaths);

            foreach (var kvp in assetToDeps)
            {
                var assetPath = kvp.Key;
                var deps = kvp.Value;

                if (deps.Any(d => targetSet.Contains(d)))
                {
                    yield return assetPath;
                }
            }
        }
    }
}