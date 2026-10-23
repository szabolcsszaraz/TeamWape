using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.GraphGeneration
{
    /// <summary>
    /// Restructures a DependencyGraph into the AI.CoreGraph/ folder structure.
    /// Ported 1:1 from restructure.py. Runs on a background thread (pure file I/O).
    /// </summary>
    static class GraphRestructurer
    {
        internal static string NormalizeId(string pathOrId)
        {
            if (string.IsNullOrEmpty(pathOrId))
            {
                InternalLog.LogWarning("[GraphRestructurer] NormalizeId received null or empty path");
                return "unknown";
            }

            return pathOrId
                .Replace("/", "_")
                .Replace("\\", "_")
                .Replace(" ", "_")
                .Replace(".", "_")
                .Replace("-", "_");
        }

        static void WriteJson(string filePath, object data)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                // Write directly to file stream to avoid background thread StringWriter/StringBuilder issues
                using (var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var streamWriter = new StreamWriter(fileStream, System.Text.Encoding.UTF8))
                using (var jsonWriter = new JsonTextWriter(streamWriter))
                {
                    jsonWriter.Formatting = Formatting.Indented;
                    jsonWriter.Indentation = 2;
                    jsonWriter.IndentChar = ' ';

                    var serializer = new JsonSerializer
                    {
                        NullValueHandling = NullValueHandling.Include,
                        DefaultValueHandling = DefaultValueHandling.Include,
                        StringEscapeHandling = StringEscapeHandling.Default,
                        Formatting = Formatting.Indented
                    };

                    serializer.Serialize(jsonWriter, data);

                    // Ensure everything is flushed
                    jsonWriter.Flush();
                    streamWriter.Flush();
                    fileStream.Flush();
                }

                var fileInfo = new FileInfo(filePath);
                InternalLog.Log($"[GraphRestructurer] Wrote {Path.GetFileName(filePath)} ({fileInfo.Length} bytes) in {sw.ElapsedMilliseconds}ms");
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[GraphRestructurer] Failed to write JSON to {Path.GetFileName(filePath)}: {ex.Message}\n{ex.StackTrace}");

                // Fallback: try simple approach without formatting
                try
                {
                    InternalLog.Log($"[GraphRestructurer] Attempting fallback: unformatted JSON...");
                    var json = JsonConvert.SerializeObject(data, new JsonSerializerSettings
                    {
                        NullValueHandling = NullValueHandling.Include
                    });
                    File.WriteAllText(filePath, json);
                    InternalLog.Log($"[GraphRestructurer] Fallback succeeded for {Path.GetFileName(filePath)}");
                }
                catch (Exception ex2)
                {
                    InternalLog.LogWarning($"[GraphRestructurer] Fallback also failed: {ex2.Message}");
                    throw;
                }
            }
        }

        static void WriteNodeDir(string outputDir, string dirName, string fileName, object data)
        {
            var dir = Path.Combine(outputDir, dirName);
            Directory.CreateDirectory(dir);

            // Add diagnostics for the data being written
            if (data == null)
            {
                InternalLog.LogWarning($"[GraphRestructurer] WriteNodeDir: data is NULL for {dirName}/{fileName}");
            }
            else if (data is System.Collections.ICollection collection)
            {
                InternalLog.Log($"[GraphRestructurer] WriteNodeDir: writing {collection.Count} items to {dirName}/{fileName}");
            }

            var fullPath = Path.Combine(dir, fileName);

            try
            {
                WriteJson(fullPath, data);
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[GraphRestructurer] FAILED to write {dirName}/{fileName}: {ex.Message}");
                throw;
            }
        }

        public static void RestructureGraph(DependencyGraph graph, string outputDir)
        {
            InternalLog.Log($"[GraphRestructurer] Starting graph restructuring (scenes: {graph.scenes.Count}, assets: {graph.assetDependencies.Count}, code deps: {graph.codeDependencies.Count})");

            var nodeIds = new Dictionary<string, string>();

            // --- Scene nodes ---
            var sceneNodes = new List<Dictionary<string, object>>();
            foreach (var scene in graph.scenes)
            {
                var nodeId = $"scene_{NormalizeId(scene.path)}";
                nodeIds[scene.path] = nodeId;
                sceneNodes.Add(new Dictionary<string, object>
                {
                    { "id", nodeId },
                    { "type", "scene" },
                    { "path", scene.path },
                    { "name", scene.name },
                    { "direct_dependencies_count", scene.dependencies?.Count ?? 0 }
                });
            }

            // --- Asset nodes ---
            var assetNodes = new List<Dictionary<string, object>>();
            foreach (var asset in graph.assetDependencies)
            {
                if (asset.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)) continue;

                var assetType = !string.IsNullOrEmpty(asset.assetType)
                    ? asset.assetType
                    : AssetTypeUtils.GetAssetTypeFromPath(asset.path);
                var nodeId = $"asset_{NormalizeId(asset.path)}";
                nodeIds[asset.path] = nodeId;
                assetNodes.Add(new Dictionary<string, object>
                {
                    { "id", nodeId },
                    { "type", "asset" },
                    { "asset_type", assetType },
                    { "path", asset.path },
                    { "name", asset.name },
                    { "direct_dependencies_count", asset.dependencies?.Count ?? 0 },
                    { "direct_dependents_count", asset.dependents?.Count ?? 0 }
                });
            }

            // --- Asset type nodes ---
            var typeDescriptions = AssetTypeUtils.GetAssetTypeDescriptions();
            var typeCounts = new Dictionary<string, int>();
            foreach (var asset in graph.assetDependencies)
            {
                var assetType = !string.IsNullOrEmpty(asset.assetType)
                    ? asset.assetType
                    : AssetTypeUtils.GetAssetTypeFromPath(asset.path);
                typeCounts[assetType] = typeCounts.GetValueOrDefault(assetType) + 1;
            }
            var assetTypeNodes = new List<Dictionary<string, object>>();
            foreach (var kvp in typeCounts)
            {
                assetTypeNodes.Add(new Dictionary<string, object>
                {
                    { "id", $"assetType_{NormalizeId(kvp.Key)}" },
                    { "type", "assetType" },
                    { "name", kvp.Key },
                    { "description", typeDescriptions.GetValueOrDefault(kvp.Key, $"Assets of type {kvp.Key}") },
                    { "asset_count", kvp.Value }
                });
            }

            // --- Project node ---
            var projectNode = new Dictionary<string, object>
            {
                { "id", "project_root" },
                { "type", "project" },
                { "name", graph.projectName ?? "" },
                { "unity_version", graph.unityVersion ?? "" },
                { "description", $"Central project node for {graph.projectName}. Connects to all scenes, asset types, and tool categories." },
                { "scene_count", sceneNodes.Count },
                { "asset_count", assetNodes.Count },
                { "tool_count", 0 },
                { "asset_type_count", assetTypeNodes.Count },
                { "tool_category_count", 0 }
            };

            // --- scene_directlyDependsOn_asset edges ---
            var sceneAssetEdges = new List<Dictionary<string, object>>();
            foreach (var scene in graph.scenes)
            {
                if (!nodeIds.TryGetValue(scene.path, out var sceneId)) continue;
                foreach (var dep in scene.dependencies ?? new List<string>())
                {
                    if (nodeIds.TryGetValue(dep, out var assetId))
                    {
                        sceneAssetEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", sceneId }, { "dst_id", assetId },
                            { "relation_type", "directlyDependsOn" }, { "src_type", "scene" }, { "dst_type", "asset" }
                        });
                    }
                }
            }

            // --- asset_directlyDependsOn_asset edges ---
            var assetAssetEdges = new List<Dictionary<string, object>>();
            foreach (var asset in graph.assetDependencies)
            {
                if (!nodeIds.TryGetValue(asset.path, out var assetId)) continue;
                foreach (var dep in asset.dependencies ?? new List<string>())
                {
                    if (nodeIds.TryGetValue(dep, out var depId))
                    {
                        assetAssetEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", assetId }, { "dst_id", depId },
                            { "relation_type", "directlyDependsOn" }, { "src_type", "asset" }, { "dst_type", "asset" }
                        });
                    }
                }
            }

            // --- asset_directlyReferencedBy_scene edges ---
            var assetSceneEdges = new List<Dictionary<string, object>>();
            foreach (var asset in graph.assetDependencies)
            {
                if (!nodeIds.TryGetValue(asset.path, out var assetId)) continue;
                foreach (var dependent in asset.dependents ?? Enumerable.Empty<string>())
                {
                    if (dependent.EndsWith(".unity") && nodeIds.TryGetValue(dependent, out var sceneId))
                    {
                        assetSceneEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", assetId }, { "dst_id", sceneId },
                            { "relation_type", "directlyReferencedBy" }, { "src_type", "asset" }, { "dst_type", "scene" }
                        });
                    }
                }
            }

            // --- assetType_include_asset edges ---
            var typeAssetEdges = new List<Dictionary<string, object>>();
            foreach (var asset in graph.assetDependencies)
            {
                if (!nodeIds.TryGetValue(asset.path, out var assetId)) continue;
                var assetType = !string.IsNullOrEmpty(asset.assetType)
                    ? asset.assetType
                    : AssetTypeUtils.GetAssetTypeFromPath(asset.path);
                typeAssetEdges.Add(new Dictionary<string, object>
                {
                    { "src_id", $"assetType_{NormalizeId(assetType)}" }, { "dst_id", assetId },
                    { "relation_type", "includes" }, { "src_type", "assetType" }, { "dst_type", "asset" }
                });
            }

            // --- project edges ---
            var projectSceneEdges = sceneNodes.Select(s => new Dictionary<string, object>
            {
                { "src_id", "project_root" }, { "dst_id", s["id"] },
                { "relation_type", "has" }, { "src_type", "project" }, { "dst_type", "scene" }
            }).ToList();

            var projectAssetTypeEdges = assetTypeNodes.Select(a => new Dictionary<string, object>
            {
                { "src_id", "project_root" }, { "dst_id", a["id"] },
                { "relation_type", "contains" }, { "src_type", "project" }, { "dst_type", "assetType" }
            }).ToList();

            // --- Code dependency edges ---
            var codeDeps = graph.codeDependencies;

            var inheritEdges = new List<Dictionary<string, object>>();
            var implementEdges = new List<Dictionary<string, object>>();
            var declareEdges = new List<Dictionary<string, object>>();
            var usesEdges = new List<Dictionary<string, object>>();

            var assetPathToId = new Dictionary<string, string>();
            foreach (var kvp in nodeIds)
            {
                assetPathToId[kvp.Key] = kvp.Value;
            }

            // Build a class name to ID mapping for dep.to lookups (heuristic: class name = file stem)
            var classNameToId = new Dictionary<string, string>();
            foreach (var kvp in nodeIds)
            {
                var stem = Path.GetFileNameWithoutExtension(kvp.Key);
                if (!classNameToId.ContainsKey(stem))
                {
                    classNameToId[stem] = kvp.Value;
                }
            }

            foreach (var dep in codeDeps)
            {
                if (dep.type == CodeDependencyType.InheritsFrom)
                {
                    var fromId = assetPathToId.GetValueOrDefault(dep.from);
                    var toId = classNameToId.GetValueOrDefault(dep.to);
                    if (fromId != null && toId != null)
                    {
                        inheritEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", fromId }, { "dst_id", toId },
                            { "relation_type", "inheritsFrom" }, { "src_type", "asset" }, { "dst_type", "asset" }
                        });
                    }
                }
                else if (dep.type == CodeDependencyType.Implements)
                {
                    var fromId = assetPathToId.GetValueOrDefault(dep.from);
                    var toId = classNameToId.GetValueOrDefault(dep.to);
                    if (fromId != null && toId != null)
                    {
                        implementEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", fromId }, { "dst_id", toId },
                            { "relation_type", "implements" }, { "src_type", "asset" }, { "dst_type", "asset" }
                        });
                    }
                }
                else if (dep.type == CodeDependencyType.Declares)
                {
                    var fromId = assetPathToId.GetValueOrDefault(dep.from);
                    var toId = classNameToId.GetValueOrDefault(dep.to);
                    if (fromId != null && toId != null)
                    {
                        declareEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", fromId }, { "dst_id", toId },
                            { "relation_type", "declares" }, { "src_type", "asset" }, { "dst_type", "asset" }
                        });
                    }
                }

                else if (dep.type == CodeDependencyType.Uses)
                {
                    var fromId = assetPathToId.GetValueOrDefault(dep.from);
                    var toId = classNameToId.GetValueOrDefault(dep.to);
                    if (fromId != null && toId != null)
                    {
                        usesEdges.Add(new Dictionary<string, object>
                        {
                            { "src_id", fromId }, { "dst_id", toId },
                            { "relation_type", "uses" }, { "src_type", "asset" }, { "dst_type", "asset" }
                        });
                    }
                }
            }

            // --- Compute direct_dependencies_count and direct_dependents_count from edge lists ---

            // Outgoing (dependencies): asset as src in asset_directlyDependsOn_asset, inheritsFrom, implements, declares, uses
            // Incoming (dependents): asset as dst in asset_directlyDependsOn_asset, scene_directlyDependsOn_asset, inheritsFrom, implements, declares, uses
            var outgoingCount = new Dictionary<string, int>();
            var incomingCount = new Dictionary<string, int>();
            foreach (var edge in assetAssetEdges)
            {
                var src = edge["src_id"] as string;
                var dst = edge["dst_id"] as string;
                if (src != null) outgoingCount[src] = outgoingCount.GetValueOrDefault(src, 0) + 1;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var edge in sceneAssetEdges)
            {
                var dst = edge["dst_id"] as string;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var edge in inheritEdges)
            {
                var src = edge["src_id"] as string;
                var dst = edge["dst_id"] as string;
                if (src != null) outgoingCount[src] = outgoingCount.GetValueOrDefault(src, 0) + 1;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var edge in implementEdges)
            {
                var src = edge["src_id"] as string;
                var dst = edge["dst_id"] as string;
                if (src != null) outgoingCount[src] = outgoingCount.GetValueOrDefault(src, 0) + 1;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var edge in declareEdges)
            {
                var src = edge["src_id"] as string;
                var dst = edge["dst_id"] as string;
                if (src != null) outgoingCount[src] = outgoingCount.GetValueOrDefault(src, 0) + 1;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var edge in usesEdges)
            {
                var src = edge["src_id"] as string;
                var dst = edge["dst_id"] as string;
                if (src != null) outgoingCount[src] = outgoingCount.GetValueOrDefault(src, 0) + 1;
                if (dst != null) incomingCount[dst] = incomingCount.GetValueOrDefault(dst, 0) + 1;
            }
            foreach (var node in assetNodes)
            {
                var id = node["id"] as string;
                if (id != null)
                {
                    node["direct_dependencies_count"] = outgoingCount.GetValueOrDefault(id, 0);
                    node["direct_dependents_count"] = incomingCount.GetValueOrDefault(id, 0);
                }
            }

            // --- Write everything to disk ---
            InternalLog.Log($"[GraphRestructurer] Writing graph files to disk at {outputDir}...");
            InternalLog.Log($"[GraphRestructurer] Edge counts: sceneAsset={sceneAssetEdges.Count}, assetAsset={assetAssetEdges.Count}, assetScene={assetSceneEdges.Count}, typeAsset={typeAssetEdges.Count}, inherit={inheritEdges.Count}, implement={implementEdges.Count}, declare={declareEdges.Count}, uses={usesEdges.Count}");

            WriteNodeDir(outputDir, GraphGenerationConstants.NodesSceneDir, GraphGenerationConstants.ScenesFile, sceneNodes);
            WriteNodeDir(outputDir, GraphGenerationConstants.NodesAssetDir, GraphGenerationConstants.AssetsFile, assetNodes);
            WriteNodeDir(outputDir, GraphGenerationConstants.NodesToolDir, GraphGenerationConstants.ToolsFile, new List<object>());
            WriteNodeDir(outputDir, GraphGenerationConstants.NodesAssetTypeDir, GraphGenerationConstants.AssetTypesFile, assetTypeNodes);
            WriteNodeDir(outputDir, GraphGenerationConstants.NodesToolCategoryDir, GraphGenerationConstants.ToolCategoriesFile, new List<object>());
            WriteNodeDir(outputDir, GraphGenerationConstants.NodesProjectDir, GraphGenerationConstants.ProjectFile, new List<object> { projectNode });

            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesSceneDirectlyDependsOnAssetDir, GraphGenerationConstants.DependenciesFileName, sceneAssetEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetDirectlyDependsOnAssetDir, GraphGenerationConstants.DependenciesFileName, assetAssetEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetDirectlyReferencedBySceneDir, GraphGenerationConstants.ReferencesFileName, assetSceneEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetTypeIncludeAssetDir, GraphGenerationConstants.TypeMembershipFileName, typeAssetEdges);

            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesToolCategoryIncludeToolDir, GraphGenerationConstants.CategoryMembershipFileName, new List<object>());
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesProjectCanUseToolCategoryDir, GraphGenerationConstants.CanUseFileName, new List<object>());

            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesProjectHasSceneDir, GraphGenerationConstants.HasFileName, projectSceneEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesProjectContainsAssetTypeDir, GraphGenerationConstants.ContainsFileName, projectAssetTypeEdges);

            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetInheritsFromAssetDir, GraphGenerationConstants.InheritanceFileName, inheritEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetImplementsAssetDir, GraphGenerationConstants.InterfaceImplementationFileName, implementEdges);
            WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetDeclaresAssetDir, GraphGenerationConstants.FieldDeclarationsFileName, declareEdges);
            if (usesEdges.Count > 0)
                WriteNodeDir(outputDir, GraphGenerationConstants.EdgesAssetUsesAssetDir, GraphGenerationConstants.TypeUsageFileName, usesEdges);

            // metadata.json written last as completion signal
            var metadata = new Dictionary<string, object>
            {
                { "total_scenes", sceneNodes.Count },
                { "total_assets", assetNodes.Count },
                { "total_tools", 0 },
                { "total_asset_types", assetTypeNodes.Count },
                { "total_tool_categories", 0 },
                { "total_projects", 1 },
                { "total_scene_directlyDependsOn_asset_edges", sceneAssetEdges.Count },
                { "total_asset_directlyDependsOn_asset_edges", assetAssetEdges.Count },
                { "total_asset_directlyReferencedBy_scene_edges", assetSceneEdges.Count },
                { "total_assetType_includes_asset_edges", typeAssetEdges.Count },
                { "total_toolCategory_includes_tool_edges", 0 },
                { "total_project_canUse_toolCategory_edges", 0 },
                { "total_project_has_scene_edges", projectSceneEdges.Count },
                { "total_project_contains_assetType_edges", projectAssetTypeEdges.Count },
                { "total_asset_inheritsFrom_asset_edges", inheritEdges.Count },
                { "total_asset_implements_asset_edges", implementEdges.Count },
                { "total_asset_declares_asset_edges", declareEdges.Count },
                { "total_asset_uses_asset_edges", usesEdges.Count },
                { "project_name", graph.projectName ?? "" },
                { "unity_version", graph.unityVersion ?? "" }
            };

            // Ensure output directory exists before writing metadata
            Directory.CreateDirectory(outputDir);

            var metadataPath = Path.Combine(outputDir, "metadata.json");
            InternalLog.Log($"[GraphRestructurer] Writing metadata.json to {metadataPath}");

            try
            {
                WriteJson(metadataPath, metadata);
                InternalLog.Log($"[GraphRestructurer] Successfully wrote metadata.json ({new FileInfo(metadataPath).Length} bytes)");

                // Verify file is readable immediately
                if (File.Exists(metadataPath))
                {
                    var content = File.ReadAllText(metadataPath);
                    InternalLog.Log($"[GraphRestructurer] Verified metadata.json is readable ({content.Length} chars)");
                }
                else
                {
                    InternalLog.LogWarning($"[GraphRestructurer] WARNING: metadata.json was written but File.Exists returns false!");
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[GraphRestructurer] FAILED to write metadata.json: {ex.Message}");
                throw;
            }

            InternalLog.Log($"[GraphRestructurer] RestructureGraph completed successfully");
        }
    }
}
