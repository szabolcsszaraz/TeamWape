using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Converters;
using Unity.AI.Assistant.Utils;
using Unity.AI.Toolkit;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Editor.GraphGeneration
{
    #region Data Models

    [Serializable]
    class DependencyGraph
    {
        public string projectName;
        public string unityVersion;
        public string timestamp;
        public List<AssetDependencyInfo> assetDependencies = new List<AssetDependencyInfo>();
        public List<SceneInfo> scenes = new List<SceneInfo>();
        public List<CodeDependencyInfo> codeDependencies = new List<CodeDependencyInfo>();
    }

    [Serializable]
    class SceneInfo
    {
        public string path;
        public string name;
        public List<string> dependencies = new List<string>();
    }

    [Serializable]
    class AssetDependencyInfo
    {
        public string path;
        public string name;
        public string assetType;
        public List<string> dependencies = new List<string>();
        public HashSet<string> dependents = new HashSet<string>();
    }

    enum CodeDependencyType
    {
        InheritsFrom,
        Implements,
        Declares,
        Uses
    }

    [Serializable]
    class CodeDependencyInfo
    {
        [Newtonsoft.Json.JsonConverter(typeof(StringEnumConverter))]
        public CodeDependencyType type;
        public string from;
        public string to;
        public List<CodeReference> references = new List<CodeReference>();
    }

    [Serializable]
    class CodeReference
    {
        public string sourceFile;
        public int lineNumber;
    }

    #endregion

    static class AssistantGraphGenerator
    {
        const string GraphFolder = "AI.CoreGraph";
        // Adaptive batching: target 100ms per batch for smooth UI responsiveness
        const int k_TargetBatchMilliseconds = 100;
        const int k_MinBatchSize = 100;
        const int k_MaxBatchSize = 5000;

        static int s_GenerationInProgress;
        internal static bool s_SuppressGeneration;

        // Progress tracking — written and read on the main thread only.
        // The asset processing loop runs on main thread with yields.
        // Background thread operations (Roslyn, disk I/O) don't update these fields.
        static string s_GenerationPhase = "";
        static int s_TotalAssets;
        static int s_ProcessedAssets;
        static int s_CurrentPhasePercent;  // 0-100% within current phase
        static int s_OverallPercent;       // 0-100% across entire generation

        // Phase weights for overall progress calculation (startPercent, endPercent)
        static readonly Dictionary<string, (int start, int end)> s_PhaseWeights = new Dictionary<string, (int, int)>
        {
            { "Initializing",                 (0,   1)  },   // 1% - Setup
            { "Processing Scenes",            (1,   5)  },   // 4% - Scene dependencies
            { "Processing Assets",            (5,   80) },   // 75% - Longest phase (thousands of assets)
            { "Analyzing Code Dependencies",  (80,  88) },   // 8% - Finding C# files
            { "Restructuring Graph",          (88,  99) },   // 11% - Roslyn analysis + disk write
            { "Complete",                     (99,  100) },  // 1% - Done

            // Incremental update phases (typically much faster)
            { "Incremental Update",           (0,   80) },   // 80% - Processing changed files
            { "Incremental Complete",         (80,  100) }   // 20% - Finalizing
        };

        /// <summary>
        /// Path to the graph under the project Library folder so the backend can resolve it as unity_project_path/Library/AI.CoreGraph.
        /// </summary>
        public static string GraphRoot => Path.Combine(
            Path.GetDirectoryName(Application.dataPath), "Library", GraphFolder);

        public static bool GraphExists()
        {
            return Directory.Exists(GraphRoot)
                && File.Exists(Path.Combine(GraphRoot, "metadata.json"));
        }

        /// <summary>
        /// Gets the current generation progress information.
        /// </summary>
        /// <returns>A tuple containing (phase, processedAssets, totalAssets, currentPhasePercent, overallPercent)</returns>
        public static (string phase, int processedAssets, int totalAssets, int currentPhasePercent, int overallPercent) GetGenerationProgress()
        {
            return (s_GenerationPhase, s_ProcessedAssets, s_TotalAssets, s_CurrentPhasePercent, s_OverallPercent);
        }

        /// <summary>
        /// Updates progress tracking for current phase and calculates overall progress.
        /// </summary>
        /// <param name="phasePercent">Progress within current phase (0-100)</param>
        static void UpdateProgress(int phasePercent)
        {
            s_CurrentPhasePercent = phasePercent;

            // Calculate overall progress based on phase weights
            if (s_PhaseWeights.TryGetValue(s_GenerationPhase, out var range))
            {
                // Map phase progress (0-100%) to overall range (start-end%)
                s_OverallPercent = range.start + (int)((range.end - range.start) * phasePercent / 100.0);
            }
            else
            {
                // Unknown phase - just use phase percent
                s_OverallPercent = phasePercent;
            }
        }

        /// <summary>
        /// Filters asset paths to exclude irrelevant assets.
        /// Packages (including com.unity.*) are included because they are game-essential
        /// building blocks (Input System, Cinemachine, URP, Netcode, etc.) that developers
        /// need the AI to understand for debugging and dependency tracing.
        /// </summary>
        static bool IsRelevantAsset(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;

            // Normalize path for consistent checking (forward slashes)
            var normalizedPath = path.Replace('\\', '/');

            // Exclude the AI Assistant's own graph output
            if (normalizedPath.Contains(GraphFolder))
                return false;

            // Exclude built-in Unity resources and system folders
            if (normalizedPath.Contains("/Resources/unity_builtin_extra") ||
                normalizedPath.Contains("/unity default resources") ||
                normalizedPath.StartsWith("Library/") ||
                normalizedPath.Contains("/Library/") ||  // Catch Library anywhere in path
                normalizedPath.StartsWith("Temp/") ||
                normalizedPath.Contains("/Temp/"))
                return false;

            // Exclude package cache
            if (normalizedPath.Contains("/PackageCache/"))
                return false;

            return true;
        }

        /// <summary>
        /// Clears the in-progress flag so a subsequent open can trigger generation (e.g. when the Assistant window was closed mid-generation).
        /// </summary>
        internal static void ResetGenerationInProgress()
        {
            Interlocked.Exchange(ref s_GenerationInProgress, 0);
        }

        public static void GenerateGraphAsync(CancellationToken cancellationToken = default)
        {
            if (s_SuppressGeneration) return;

            // Check if generation already in progress
            if (Interlocked.CompareExchange(ref s_GenerationInProgress, 1, 0) != 0)
            {
                // Generation already in progress, don't restart
                InternalLog.Log("[AssistantGraphGenerator] Graph generation already in progress, not restarting");
                return;
            }

            // If graph exists, try incremental update first
            if (GraphExists())
            {
                var refreshManager = new GraphRefreshManager(GraphRoot);
                if (refreshManager.HasPendingChanges())
                {
                    InternalLog.Log("[AssistantGraphGenerator] Graph exists with pending changes, using incremental update");
                    _ = ProcessIncrementalUpdateAsync(refreshManager, cancellationToken);
                    return;
                }
                else
                {
                    // No pending changes, graph is up to date - reset flag
                    InternalLog.Log("[AssistantGraphGenerator] Graph is up to date, no generation needed");
                    Interlocked.Exchange(ref s_GenerationInProgress, 0);
                    return;
                }
            }

            // Full generation for new graphs
            _ = GenerateGraphTask(cancellationToken);
        }

        /// <summary>
        /// Process incremental graph updates for changed assets.
        /// Much faster than full regeneration (only processes changed files).
        /// </summary>
        static async Task ProcessIncrementalUpdateAsync(GraphRefreshManager refreshManager, CancellationToken cancellationToken)
        {
            bool triggeredFullRegeneration = false;
            try
            {
                InternalLog.Log("[AssistantGraphGenerator] Starting incremental graph update (background)...");

                // Track incremental update progress
                s_GenerationPhase = "Incremental Update";
                s_TotalAssets = 0;  // Unknown for incremental
                s_ProcessedAssets = 0;
                UpdateProgress(0);

                var result = await refreshManager.ProcessPendingChangesAsync();

                if (result.NeedsFullRegeneration)
                {
                    InternalLog.Log("[AssistantGraphGenerator] Incremental update requires full regeneration - triggering full generation");
                    // Trigger full regeneration - it will take over the generation flag
                    // s_GenerationInProgress is already 1, so we just call GenerateGraphTask directly
                    triggeredFullRegeneration = true;
                    _ = GenerateGraphTask(cancellationToken);
                }
                else
                {
                    s_GenerationPhase = "Incremental Complete";
                    UpdateProgress(100);
                    InternalLog.Log($"[AssistantGraphGenerator] Incremental update complete: {result.Status}");
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantGraphGenerator] Incremental update failed: {ex.Message}");
            }
            finally
            {
                // Only reset if we didn't trigger full regeneration (which will handle the flag)
                if (!triggeredFullRegeneration)
                {
                    InternalLog.Log("[AssistantGraphGenerator] Incremental update finished, setting in-progress to 0");
                    Interlocked.Exchange(ref s_GenerationInProgress, 0);

                    // Reset progress tracking
                    s_GenerationPhase = "";
                    s_TotalAssets = 0;
                    s_ProcessedAssets = 0;
                    s_CurrentPhasePercent = 0;
                    s_OverallPercent = 0;
                }
            }
        }

        static async Task GenerateGraphTask(CancellationToken cancellationToken = default)
        {
            try
            {
                InternalLog.Log("[AssistantGraphGenerator] Starting full graph generation...");

                // Initialize progress tracking
                s_GenerationPhase = "Initializing";
                s_TotalAssets = 0;
                s_ProcessedAssets = 0;
                UpdateProgress(0);

                var generator = new GraphGenerator();
                var graph = new DependencyGraph
                {
                    projectName = Application.productName,
                    unityVersion = Application.unityVersion,
                    timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    assetDependencies = new List<AssetDependencyInfo>(),
                    scenes = new List<SceneInfo>(),
                    codeDependencies = new List<CodeDependencyInfo>()
                };

                InternalLog.Log($"[AssistantGraphGenerator] Starting graph generation for project: {Application.productName}");
                InternalLog.Log($"[AssistantGraphGenerator] Unity version: {Application.unityVersion}");

                AssetDatabase.Refresh();
                await EditorTask.Yield();

                var assetDict = new Dictionary<string, AssetDependencyInfo>();

                string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");
                InternalLog.Log($"[AssistantGraphGenerator] Found {sceneGuids.Length} scene(s)");

                s_GenerationPhase = "Processing Scenes";
                s_TotalAssets = sceneGuids.Length;
                s_ProcessedAssets = 0;
                UpdateProgress(0);

                foreach (string guid in sceneGuids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                    {
                        generator.ProcessScene(path, graph, assetDict);
                        s_ProcessedAssets++;
                        if (s_TotalAssets > 0)
                            UpdateProgress((int)((float)s_ProcessedAssets / s_TotalAssets * 100));
                    }
                }

                await EditorTask.Yield();

                string[] allGuids = AssetDatabase.FindAssets("t:Object");

                // Pre-filter: Remove Unity internal packages and irrelevant assets
                var relevantPaths = allGuids
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Where(IsRelevantAsset)
                    .ToArray();

                int filteredCount = allGuids.Length - relevantPaths.Length;
                InternalLog.Log($"[AssistantGraphGenerator] Processing {relevantPaths.Length} asset(s) (filtered {filteredCount} irrelevant assets)...");

                // Debug: Check if any Library paths slipped through
                var libraryPaths = relevantPaths.Where(p => p.Replace('\\', '/').Contains("/Library/")).ToArray();
                if (libraryPaths.Length > 0)
                {
                    InternalLog.LogWarning($"[AssistantGraphGenerator] WARNING: {libraryPaths.Length} Library paths were not filtered:");
                    foreach (var libPath in libraryPaths.Take(5))
                        InternalLog.LogWarning($"  - {libPath}");
                }

                InternalLog.Log($"[AssistantGraphGenerator] Starting asset processing loop with batch size {k_MinBatchSize}, one-hop direct dependencies");

                s_GenerationPhase = "Processing Assets";
                s_TotalAssets = relevantPaths.Length;
                s_ProcessedAssets = 0;
                UpdateProgress(0);

                // Adaptive batching: adjust batch size based on processing time
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                int currentBatchSize = k_MinBatchSize;
                int processedSinceYield = 0;
                int lastLoggedPercent = 0;
                int assetsProcessedTotal = 0;

                for (int i = 0; i < relevantPaths.Length; i++)
                {
                    try
                    {
                        // Check for cancellation
                        cancellationToken.ThrowIfCancellationRequested();

                        string path = relevantPaths[i];

                        // One-hop direct dependencies only — inherently fast (1-10ms per call), no BFS or guards needed
                        var dependencies = AssetDatabase.GetDependencies(path, false).Where(dep => dep != path).ToList();

                        assetsProcessedTotal++;
                        s_ProcessedAssets = i + 1;
                        UpdateProgress((int)((float)(i + 1) / relevantPaths.Length * 100));

                        // Log when we continue after a very complex asset
                        if (dependencies.Count > 1000)
                        {
                            InternalLog.Log($"[AssistantGraphGenerator] Processed complex asset {path} with {dependencies.Count} dependencies, continuing... ({i + 1}/{relevantPaths.Length})");
                        }

                        if (assetDict.TryGetValue(path, out var existing))
                        {
                            existing.dependencies.Clear();
                            foreach (string dep in dependencies)
                            {
                                existing.dependencies.Add(dep);

                                if (!assetDict.ContainsKey(dep))
                                {
                                    assetDict[dep] = new AssetDependencyInfo
                                    {
                                        path = dep,
                                        name = Path.GetFileName(dep),
                                        assetType = generator.GetAssetType(dep),
                                        dependencies = new List<string>(),
                                        dependents = new HashSet<string>()
                                    };
                                }

                                if (!assetDict[dep].dependents.Contains(path))
                                    assetDict[dep].dependents.Add(path);
                            }
                        }
                    else
                    {
                        var assetInfo = new AssetDependencyInfo
                        {
                            path = path,
                            name = Path.GetFileName(path),
                            assetType = generator.GetAssetType(path),
                            dependencies = new List<string>(),
                            dependents = new HashSet<string>()
                        };

                        foreach (string dep in dependencies)
                        {
                            assetInfo.dependencies.Add(dep);

                            if (!assetDict.ContainsKey(dep))
                            {
                                assetDict[dep] = new AssetDependencyInfo
                                {
                                    path = dep,
                                    name = Path.GetFileName(dep),
                                    assetType = generator.GetAssetType(dep),
                                    dependencies = new List<string>(),
                                    dependents = new HashSet<string>()
                                };
                            }

                            if (!assetDict[dep].dependents.Contains(path))
                                assetDict[dep].dependents.Add(path);
                        }

                        assetDict[path] = assetInfo;
                    }
                    }
                    catch (Exception ex)
                    {
                        InternalLog.LogWarning($"[AssistantGraphGenerator] Failed to process asset {relevantPaths[i]}: {ex.Message}");
                        // Continue processing other assets
                    }

                    // Adaptive batching: yield based on elapsed time OR asset count (whichever comes first)
                    processedSinceYield++;
                    bool shouldYield = stopwatch.ElapsedMilliseconds >= k_TargetBatchMilliseconds || processedSinceYield >= currentBatchSize;

                    if (shouldYield)
                    {
                        // Log progress to console every 10% OR every 1000 assets (non-blocking)
                        float progress = (float)(i + 1) / relevantPaths.Length;
                        int currentPercent = (int)(progress * 100);
                        bool shouldLog = currentPercent >= lastLoggedPercent + 10 || (i + 1) % 1000 == 0;

                        if (shouldLog && currentPercent > lastLoggedPercent)
                        {
                            InternalLog.Log($"[AssistantGraphGenerator] Progress: Phase {currentPercent}% | Overall {s_OverallPercent}% ({i + 1}/{relevantPaths.Length} assets, batch size: {currentBatchSize}, {stopwatch.ElapsedMilliseconds}ms)");
                            lastLoggedPercent = currentPercent;
                        }

                        // Check for cancellation (non-blocking)
                        cancellationToken.ThrowIfCancellationRequested();

                        // Adjust batch size for next iteration based on processing rate
                        if (processedSinceYield < currentBatchSize * 0.5)
                            currentBatchSize = Math.Max(k_MinBatchSize, currentBatchSize / 2);   // slow assets → smaller batches
                        else if (stopwatch.ElapsedMilliseconds < k_TargetBatchMilliseconds)
                            currentBatchSize = Math.Min(k_MaxBatchSize, currentBatchSize * 2);   // fast assets → larger batches

                        await EditorTask.Yield();
                        stopwatch.Restart();
                        processedSinceYield = 0;
                    }
                }

                InternalLog.Log($"[AssistantGraphGenerator] Asset loop completed: processed {assetsProcessedTotal} assets");

                graph.assetDependencies = assetDict.Values.ToList();
                InternalLog.Log($"[AssistantGraphGenerator] Asset processing complete: {graph.scenes.Count} scene(s), {graph.assetDependencies.Count} asset(s)");
                InternalLog.Log($"[AssistantGraphGenerator] Starting C# code dependency analysis (parallel)...");

                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var graphRoot = GraphRoot;

                // Count C# files for progress tracking
                var csScriptGuids = AssetDatabase.FindAssets("t:MonoScript");
                s_GenerationPhase = "Analyzing Code Dependencies";
                s_ProcessedAssets = 0;
                s_TotalAssets = csScriptGuids.Length;
                UpdateProgress(0);

                // Fast path: discover C# files with Directory.GetFiles on background thread and write graph immediately.
                // Include both Assets and Packages so the graph covers project and package scripts (matches AssetDatabase scope).
                // In parallel, build Unity's canonical list via AssetDatabase for validation (addresses symlinks / Unity view).
                var task = Task.Run(() =>
                {
                    try
                    {
                        var assetsPath = Path.Combine(projectRoot, "Assets");
                        var pathsFromAssets = Directory.GetFiles(assetsPath, "*.cs", SearchOption.AllDirectories);
                        var packagesPath = Path.Combine(projectRoot, "Packages");
                        var pathsFromPackages = Directory.Exists(packagesPath)
                            ? Directory.GetFiles(packagesPath, "*.cs", SearchOption.AllDirectories)
                            : Array.Empty<string>();
                        var pathsFromGetFiles = pathsFromAssets.Concat(pathsFromPackages).ToList();
                        var analyzer = new CodeDependencyAnalyzer();
                        graph.codeDependencies = analyzer.AnalyzeCodeDependencies(pathsFromGetFiles);
                        GraphRestructurer.RestructureGraph(graph, graphRoot);
                        return pathsFromGetFiles;
                    }
                    catch (Exception ex)
                    {
                        // Log from background thread (may not show immediately)
                        InternalLog.LogWarning($"[AssistantGraphGenerator] Exception in background graph generation: {ex.Message}\n{ex.StackTrace}");
                        throw; // Re-throw so await can catch it
                    }
                });

                var csFilePathsFromAssetDb = new List<string>();
                int csFileIndex = 0;
                foreach (var guid in csScriptGuids)
                {
                    var assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                    {
                        var combinedPath = Path.Combine(projectRoot, assetPath);
                        if (File.Exists(combinedPath))
                            csFilePathsFromAssetDb.Add(combinedPath);
                    }
                    csFileIndex++;
                    s_ProcessedAssets = csFileIndex;
                    if (s_TotalAssets > 0)
                        UpdateProgress((int)((float)csFileIndex / s_TotalAssets * 100));
                }

                InternalLog.Log("[AssistantGraphGenerator] Waiting for background graph restructuring task to complete...");

                // Wait for task with periodic heartbeat logging (since background thread logs might not show)
                var timeout = System.Diagnostics.Stopwatch.StartNew();
                while (!task.IsCompleted)
                {
                    await EditorTask.Delay(5000); // Wait 5 seconds
                    if (!task.IsCompleted)
                    {
                        InternalLog.Log($"[AssistantGraphGenerator] Still waiting for graph restructuring... ({timeout.Elapsed.TotalSeconds:F0}s elapsed)");
                    }
                }

                List<string> pathsFromGetFiles;
                try
                {
                    pathsFromGetFiles = await task; // Get result (will throw if background task threw)
                    InternalLog.Log($"[AssistantGraphGenerator] Background graph restructuring completed in {timeout.Elapsed.TotalSeconds:F0}s");
                }
                catch (Exception ex)
                {
                    InternalLog.LogWarning($"[AssistantGraphGenerator] Background graph generation failed: {ex.Message}");
                    throw;
                }

                if (!PathSetsEqual(pathsFromGetFiles, csFilePathsFromAssetDb))
                {
                    InternalLog.Log("[AssistantGraphGenerator] C# file list differed from AssetDatabase (e.g. symlinks); regenerating graph with Unity asset list.");
                    s_GenerationPhase = "Restructuring Graph";
                    UpdateProgress(0);

                    // Invalidate graph so GraphExists() returns false during regeneration
                    var metadataPath = Path.Combine(graphRoot, GraphGenerationConstants.MetadataFile);
                    try
                    {
                        if (File.Exists(metadataPath))
                            File.Delete(metadataPath);
                    }
                    catch (Exception ex)
                    {
                        InternalLog.LogWarning($"[AssistantGraphGenerator] Could not delete metadata.json before regeneration: {ex.Message}");
                    }

                    var regenerateTask = Task.Run(() =>
                    {
                        var analyzer = new CodeDependencyAnalyzer();
                        graph.codeDependencies = analyzer.AnalyzeCodeDependencies(csFilePathsFromAssetDb);
                        GraphRestructurer.RestructureGraph(graph, graphRoot);
                    });

                    // Wait with heartbeat logging
                    var timeout2 = System.Diagnostics.Stopwatch.StartNew();
                    while (!regenerateTask.IsCompleted)
                    {
                        await EditorTask.Delay(5000);
                        if (!regenerateTask.IsCompleted)
                        {
                            InternalLog.Log($"[AssistantGraphGenerator] Still regenerating graph... ({timeout2.Elapsed.TotalSeconds:F0}s elapsed)");
                        }
                    }

                    await regenerateTask; // Ensure exceptions are thrown
                    InternalLog.Log($"[AssistantGraphGenerator] Graph regeneration completed in {timeout2.Elapsed.TotalSeconds:F0}s");
                    UpdateProgress(100);
                }

                s_GenerationPhase = "Complete";
                UpdateProgress(100);
                InternalLog.Log($"[AssistantGraphGenerator] Graph generation complete: {graphRoot}");

                // Verify graph was actually written
                if (GraphExists())
                {
                    InternalLog.Log($"[AssistantGraphGenerator] Verified: Graph files exist and are accessible");
                }
                else
                {
                    InternalLog.LogWarning($"[AssistantGraphGenerator] WARNING: Generation completed but GraphExists() returns false!");
                }
            }
            catch (OperationCanceledException)
            {
                InternalLog.Log("[AssistantGraphGenerator] Graph generation was cancelled");
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"[AssistantGraphGenerator] Background generation failed: {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                InternalLog.Log("[AssistantGraphGenerator] Generation task finally block executing, setting in-progress to 0");
                Interlocked.Exchange(ref s_GenerationInProgress, 0);

                // Reset progress tracking
                s_GenerationPhase = "";
                s_TotalAssets = 0;
                s_ProcessedAssets = 0;
                s_CurrentPhasePercent = 0;
                s_OverallPercent = 0;
            }
        }

        static string NormalizePath(string path)
        {
            return path?.Replace('\\', '/').TrimEnd('/') ?? "";
        }

        static bool PathSetsEqual(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count != b.Count) return false;
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var p in a)
                set.Add(NormalizePath(p));
            foreach (var p in b)
                if (!set.Contains(NormalizePath(p))) return false;
            return true;
        }

    #region GraphGenerator - Phase 1, main thread (from full_graph_generation.cs)

    class GraphGenerator
    {
        public void ProcessScene(string scenePath, DependencyGraph graph, Dictionary<string, AssetDependencyInfo> assetDict)
        {
            var sceneInfo = new SceneInfo
            {
                path = scenePath,
                name = Path.GetFileNameWithoutExtension(scenePath),
                dependencies = new List<string>()
            };

            // One-hop direct dependencies only for scene → asset edges
            string[] dependencies = AssetDatabase.GetDependencies(scenePath, false);

            foreach (string dep in dependencies)
            {
                if (dep == scenePath) continue;

                sceneInfo.dependencies.Add(dep);

                if (!assetDict.ContainsKey(dep))
                {
                    assetDict[dep] = new AssetDependencyInfo
                    {
                        path = dep,
                        name = Path.GetFileName(dep),
                        assetType = GetAssetType(dep),
                        dependencies = new List<string>(),
                        dependents = new HashSet<string>()
                    };
                }

                if (!assetDict[dep].dependents.Contains(scenePath))
                    assetDict[dep].dependents.Add(scenePath);
            }

            graph.scenes.Add(sceneInfo);
        }

        /// <summary>
        /// Returns the asset type using AssetDatabase.GetMainAssetTypeAtPath (no load). When the type is not
        /// available (e.g. not yet imported), falls back to an extension-based mapping aligned with Unity's
        /// common importers and FindAssets filters. See Unity Scripting Reference: AssetDatabase.FindAssets.
        /// </summary>
        public string GetAssetType(string path)
        {
            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            if (type != null) return type.Name;

            return AssetTypeUtils.GetAssetTypeFromPath(path);
        }
    }

    #endregion
    }
}
