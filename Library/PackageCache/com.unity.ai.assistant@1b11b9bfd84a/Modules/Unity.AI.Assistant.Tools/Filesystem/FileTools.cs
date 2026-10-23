using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    class FileTools
    {
        const string k_FindFilesFunctionId = "Unity.FindFiles";
        const string k_GetFileContentFunctionId = "Unity.GetFileContent";
        const string k_GrepFunctionId = "Unity.Grep";

        internal const int k_FindFilesMaxResults = 50;
        const int k_MaxContentLength = 1000;
        const int k_GrepProcessTimeoutMs = 10000;

        static string s_RipgrepBasePath;

        static string RipgrepBasePath
        {
            get
            {
                if (s_RipgrepBasePath == null)
                {
                    var packageInfo = PackageInfo.FindForAssembly(typeof(FileTools).Assembly);
                    s_RipgrepBasePath = packageInfo != null
                        ? Path.Combine(packageInfo.resolvedPath, "ThirdParty~", "ripgrep")
                        : Path.GetFullPath("Packages/com.unity.ai.assistant/ThirdParty~/ripgrep");
                }

                return s_RipgrepBasePath;
            }
        }

        static readonly string[] k_SearchFolders = { "Assets" };

        static string[] ResolveSearchPaths(string projectRoot)
        {
            return k_SearchFolders
                .Select(p => Path.Combine(projectRoot, p))
                .Where(Directory.Exists)
                .ToArray();
        }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        static bool s_RipgrepPermissionSet;
#endif

        [Serializable]
        public struct FileMatch
        {
            [Description("The relative path to the file containing the match")]
            public string FilePath;

            [Description("The first line number of the MatchingContent block (only set when searchPattern is provided)")]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? StartLineNumber;

            [Description("The last line number of the MatchingContent block (only set when searchPattern is provided)")]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public int? EndLineNumber;

            [Description("The found content, including context lines (only set when searchPattern is provided)")]
            [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
            public string MatchingContent;
        }

        [Serializable]
        public struct SearchFileContentOutput
        {
            [Description("The matching results")]
            public List<FileMatch> Matches;

            public string Info;
        }

        [AgentTool(
            "Search for content within files and return a list of matching file with the found content, including some context.",
            k_FindFilesFunctionId)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent | AssistantMode.Ask | AssistantMode.Plan,
            tags: FunctionCallingUtilities.k_SmartContextTag)]
        public static async Task<SearchFileContentOutput> FindFiles(
            ToolExecutionContext context,
            [ToolParameter(
                "Regex pattern to search for in file contents.\n" +
                "Examples:\n" +
                "  \"TODO\": Match any line containing 'TODO'\n" +
                "  \"public\\s+class\\s+\\w+\": Match class declarations in C#\n" +
                "Leave empty to not filter by content (files will still be filtered by 'nameRegex')."
            )]
            string searchPattern,
	        [ToolParameter(
		        "Regex pattern applied to the relative file path (including filename + extension).\n" +
		        "Examples:\n" +
		        "  \".*Program\\.cs$\": Match a specific filename 'Program.cs'\n" +
		        "  \".*Controllers/.*\": Match all files under a 'Controllers' folder\n" +
		        "  \".*\\.txt$\": Match all files with the .txt extension\n" +
		        "  \".*Test.*\": Match any file path containing 'Test'\n" +
                "Leave empty to include all files BUT try to use this field as much as possible to limit the number of results."
	        )]
            string nameRegex = "",
	        [ToolParameter("Number of context lines to show around matches (Defaults to 2)")]
            int contextLines = 2,
	        [ToolParameter("Index of the first match to return (for pagination, defaults to 0 to get the first page)")]
            int startIndex = 0,
            [ToolParameter("Internal parameter: set to false to run synchronously (for testing). Defaults to true for async execution.")]
            bool runOnBackgroundThread = true
        )
        {
            var projectPath = Directory.GetCurrentDirectory();

            var searchPaths = k_SearchFolders.Select(folder => Path.Combine(projectPath, folder)).ToList();
            foreach (var folder in searchPaths)
            {
                await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Read, Path.Combine(projectPath, folder));
            }

	        Regex searchRegex = null;
	        Regex fileRegex = null;
	        try
	        {
		        if (!string.IsNullOrEmpty(searchPattern))
			        searchRegex = new Regex(searchPattern, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(3));

		        if (!string.IsNullOrWhiteSpace(nameRegex))
			        fileRegex = new Regex(nameRegex, RegexOptions.IgnoreCase | RegexOptions.Compiled, TimeSpan.FromSeconds(3));
	        }
	        catch (ArgumentException ex)
	        {
		        throw new ArgumentException($"Invalid regex: {ex.Message}", ex);
	        }

	        // Run file search - on background thread in production, synchronously in tests to avoid deadlocks
	        if (runOnBackgroundThread)
	        {
	            return await Task.Run(() => FindFilesCore(
	                searchPaths, projectPath, searchRegex, fileRegex,
	                contextLines, startIndex
	            )).ConfigureAwait(false);
	        }
	        else
	        {
	            // Run synchronously for tests - avoids deadlock with Unity's test runner
	            return FindFilesCore(
	                searchPaths, projectPath, searchRegex, fileRegex,
	                contextLines, startIndex
	            );
	        }
        }

        /// <summary>
        /// Core synchronous implementation of file search.
        /// Extracted to allow both async (via Task.Run) and sync execution.
        /// </summary>
        [ToolPermissionIgnore]
        static SearchFileContentOutput FindFilesCore(
            List<string> searchPaths,
            string projectPath,
            Regex searchRegex,
            Regex fileRegex,
            int contextLines,
            int startIndex)
        {
            const int maxResults = k_FindFilesMaxResults;
            var results = new SearchFileContentOutput { Matches = new List<FileMatch>(), Info = "" };

            // Step 1: Collect all candidate files (fast filtering by path)
            // Always excludes: Library folder and .meta files
            var allFiles = searchPaths
                .Where(Directory.Exists)
                .SelectMany(searchPath =>
                {
                    try
                    {
                        return Directory.EnumerateFiles(searchPath, "*.*", SearchOption.AllDirectories);
                    }
                    catch
                    {
                        return Enumerable.Empty<string>();
                    }
                })
                .Select(file => new { File = file, RelativePath = Path.GetRelativePath(projectPath, file) })
                .Where(x => !x.RelativePath.StartsWith("Library" + Path.DirectorySeparatorChar))
                .Where(x => !x.File.EndsWith(".meta", StringComparison.OrdinalIgnoreCase)) // Always exclude .meta files
                .Where(x => fileRegex == null || fileRegex.IsMatch(x.RelativePath))
                .ToList();

            // Step 2: If no content search, just return file paths (no content reading needed)
            if (searchRegex == null)
            {
                var fileMatches = allFiles
                    .Skip(startIndex)
                    .Take(maxResults)
                    .Select(x => new FileMatch { FilePath = x.RelativePath })
                    .ToList();

                results.Matches = fileMatches;
                if (allFiles.Count > startIndex + maxResults)
                    results.Info = $"Showing {maxResults} of {allFiles.Count} files (limit: {maxResults}). Use startIndex={startIndex + maxResults} to fetch next page.";
                return results;
            }

            // Step 3: Content search - sequential but with fast File.ReadAllText
            var matchList = new List<FileMatch>();

            foreach (var x in allFiles)
            {
                // Early exit if we have enough results
                if (matchList.Count >= maxResults + startIndex)
                    break;

                try
                {
                    // Check if file is binary by looking for null bytes in first 8KB
                    // Binary files can't meaningfully match text patterns
                    if (IsBinaryFile(x.File))
                        continue;

                    // Read entire file at once (faster than line-by-line)
                    var content = File.ReadAllText(x.File);

                    // Quick check: skip file if no match at all
                    try
                    {
                        if (!searchRegex.IsMatch(content))
                            continue;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        continue;
                    }

                    // Find matching lines with context
                    var lines = content.Split('\n');
                    for (int i = 0; i < lines.Length; i++)
                    {
                        if (matchList.Count >= maxResults + startIndex)
                            break;

                        try
                        {
                            if (!searchRegex.IsMatch(lines[i]))
                                continue;
                        }
                        catch (RegexMatchTimeoutException)
                        {
                            // Skip this line if regex times out
                            continue;
                        }

                        // Build context
                        int startLine = Math.Max(0, i - contextLines);
                        int endLine = Math.Min(lines.Length - 1, i + contextLines);

                        var contextContent = string.Join("\n",
                            lines.Skip(startLine).Take(endLine - startLine + 1));

                        if (contextContent.Length > k_MaxContentLength)
                            contextContent = contextContent.Substring(0, k_MaxContentLength) + "\n... [truncated]";

                        matchList.Add(new FileMatch
                        {
                            FilePath = x.RelativePath,
                            StartLineNumber = startLine + 1,  // First line in the context block
                            EndLineNumber = endLine + 1,      // Last line in the context block
                            MatchingContent = contextContent
                        });

                        // Skip ahead to avoid overlapping contexts
                        i += contextLines;
                    }
                }
                catch
                {
                    // Skip files that can't be read
                }
            }

            // Step 4: Apply pagination and return results
            results.Matches = matchList
                .Skip(startIndex)
                .Take(maxResults)
                .ToList();

            // Note: matchList.Count >= limit means we hit the limit and there may be more matches
            if (matchList.Count >= startIndex + maxResults)
                results.Info = $"Showing {results.Matches.Count} matches (limit: {maxResults}). There may be more results. Use startIndex={startIndex + results.Matches.Count} to fetch next page.";

            return results;
        }

        /// <summary>
        /// Checks if a file is binary by looking for null bytes in the first 8KB.
        /// This is the same heuristic used by grep and other text search tools.
        /// </summary>
        static bool IsBinaryFile(string filePath)
        {
            const int bytesToCheck = 8192; // 8KB sample
            try
            {
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var buffer = new byte[Math.Min(bytesToCheck, stream.Length)];
                var bytesRead = stream.Read(buffer, 0, buffer.Length);

                // Array.IndexOf is optimized and faster than manual loop
                return Array.IndexOf(buffer, (byte)0, 0, bytesRead) >= 0;
            }
            catch
            {
                return false; // If we can't read it, assume it's not binary
            }
        }

        [AgentTool(
            "Returns the text content of a file.",
            k_GetFileContentFunctionId)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent | AssistantMode.Ask | AssistantMode.Plan,
            tags: FunctionCallingUtilities.k_SmartContextTag)]
        public static async Task<string> GetFileContent(
            ToolExecutionContext context,
            [ToolParameter("The relative path to the file.")]
            string filePath,
            [ToolParameter("The first line number to read (1-based, inclusive). Defaults to 1.")]
            int startLine = 1,
            [ToolParameter("The last line number to read (1-based, inclusive). If -1, reads until the end of the file. Defaults to -1.")]
            int endLine = -1
            )
        {
            if (string.IsNullOrEmpty(filePath))
                throw new ArgumentException("File path cannot be null or empty.");

            var projectPath = Directory.GetCurrentDirectory();
            var resolvedPath = Path.IsPathRooted(filePath) ? filePath : Path.Combine(projectPath, filePath);
            var fullPath = Path.GetFullPath(resolvedPath);

            // Prevent path traversal outside the project directory
            var projectRoot = Path.GetFullPath(projectPath);
            if (!fullPath.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullPath, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{filePath}' resolves outside the project directory. File operations are restricted to the project scope.");
            }

            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"File at path '{filePath}' not found");

            await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Read, fullPath);

            if (IsBinaryFile(fullPath))
                return $"File '{filePath}' appears to be a binary file and cannot be read as text. " +
                       "Only text files (scripts, configs, markdown, etc.) are supported.";

            if (FileUtils.ExceedsMaxReadSize(fullPath, out var sizeMB))
                return $"File '{filePath}' is too large to read in full ({sizeMB:F1} MB). " +
                       "Ask the user which part of the file is relevant.";

            var content = await File.ReadAllTextAsync(fullPath);
            if (string.IsNullOrEmpty(content))
                return content;

            if (startLine < 1)
                startLine = 1;

            // Split into lines, preserving line endings on each line
            var lineStarts = new List<int> { 0 };
            for (var i = 0; i < content.Length; i++)
            {
                if (content[i] == '\n')
                {
                    lineStarts.Add(i + 1);
                }
            }

            // If the file ends with a newline, the last entry in lineStarts points to
            // content.Length which would produce an empty phantom line. Remove it.
            if (lineStarts.Count > 1 && lineStarts[lineStarts.Count - 1] == content.Length)
                lineStarts.RemoveAt(lineStarts.Count - 1);

            var totalLines = lineStarts.Count;

            if (startLine > totalLines)
                return string.Empty;

            var effectiveEndLine = (endLine == -1 || endLine > totalLines) ? totalLines : endLine;

            if (startLine > effectiveEndLine)
                return string.Empty;

            // Build output with line numbers
            var sb = new System.Text.StringBuilder();
            for (var lineNum = startLine; lineNum <= effectiveEndLine; lineNum++)
            {
                var lineStartIdx = lineStarts[lineNum - 1];
                int lineEndIdx;

                if (lineNum < totalLines)
                {
                    lineEndIdx = lineStarts[lineNum];
                }
                else
                {
                    lineEndIdx = content.Length;
                }

                var lineContent = content.Substring(lineStartIdx, lineEndIdx - lineStartIdx);
                sb.Append($"{lineNum}: {lineContent}");
            }

            return sb.ToString();
        }

        [AgentTool(
            "Search file contents using ripgrep (rg), or list files by path pattern. " +
            "Searches within project Assets. " +
            "By default searches only .cs files. Use --glob or --type to search other file types. " +
            "Use -l to list only matching file paths (much less output than showing matching lines). " +
            "Use the path parameter to restrict searches to a specific directory.",
            k_GrepFunctionId)]
        [AgentToolSettings(assistantMode: AssistantMode.Agent | AssistantMode.Ask | AssistantMode.Plan,
            mcp: McpAvailability.Available, tags:
            FunctionCallingUtilities.k_SmartContextTag)]
        public static async Task<string> Grep(
            ToolExecutionContext context,
            [ToolParameter(
                "Ripgrep (rg) arguments, written exactly as on the command line.\n" +
                "Applied automatically: --color never, -n, --heading, --iglob \"!*.meta\", " +
                "default --type cs (overridable with explicit --glob/--type).\n" +
                "Tips for efficient searches:\n" +
                "  Use -l to get only file paths: -l \"IAbility\"\n" +
                "  Use --type to search specific file types: --type yaml \"damage\"\n" +
                "  Use --glob to include non-code files: --glob \"*.prefab\" \"Player\"\n" +
                "  Use -w for whole-word matching: -w \"Health\"\n" +
                "  Avoid short or generic patterns (1-2 chars) — they produce too many results."
            )]
            string args = "",
            [ToolParameter(
                "File or directory path to restrict the search scope. " +
                "When set, only this path is searched instead of the whole project. " +
                "Can be relative (e.g. Assets/Scripts/Combat) or absolute. " +
                "Strongly recommended when you know which directory to search — " +
                "reduces noise and speeds up results."
            )]
            string path = ""
        )
        {
            if (string.IsNullOrWhiteSpace(args))
                throw new ArgumentException("Provide ripgrep arguments (e.g. a search pattern or --files with a glob).");

            var projectRoot = Path.GetDirectoryName(Application.dataPath);

            string[] searchPaths;
            if (!string.IsNullOrWhiteSpace(path))
            {
                var fullPath = Path.IsPathRooted(path) ? path : Path.Combine(projectRoot, path);
                if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
                    return $"No matches found: path '{path}' does not exist in this project.";
                await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Read, fullPath);
                searchPaths = new[] { fullPath };
            }
            else
            {
                searchPaths = ResolveSearchPaths(projectRoot);
                foreach (var p in searchPaths)
                    await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Read, p);
            }

            // Security: ripgrep accepts paths as positional arguments, so an agent
            // can embed paths inside `args` (e.g. `args="pattern /etc/passwd"`) and
            // ripgrep will walk them alongside `searchPaths` — bypassing the
            // permission check above, which only sees the explicit `path` parameter.
            // Permission-check every path-shaped token in `args` so it routes
            // through the same project/external Read policy as `path`. Removing
            // this loop reopens the bypass.
            foreach (var argPath in GrepUtility.ExtractPathLikeTokens(args, projectRoot))
                await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Read, argPath);

            var rgPath = GetRipgrepExecutablePath();
            if (string.IsNullOrEmpty(rgPath) || !File.Exists(rgPath))
                throw new InvalidOperationException($"Ripgrep executable not found at: {rgPath}");

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            EnsureRipgrepExecutePermission(rgPath);
#endif

            var rgArgs = GrepUtility.BuildArguments(args, searchPaths);

            var psi = new ProcessStartInfo
            {
                FileName = rgPath,
                Arguments = rgArgs,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8
            };

            InternalLog.Log($"[Grep] Running: {rgPath} {rgArgs}");

            var process = Process.Start(psi);
            if (process == null)
                throw new InvalidOperationException("Failed to start ripgrep process.");

            try
            {
                var stdoutTask = process.StandardOutput.ReadToEndAsync();
                var stderrTask = process.StandardError.ReadToEndAsync();

                var completed = await Task.Run(() => process.WaitForExit(k_GrepProcessTimeoutMs)).ConfigureAwait(false);
                if (!completed)
                {
                    process.Kill();
                    throw new TimeoutException($"Search timed out after {k_GrepProcessTimeoutMs / 1000} seconds.");
                }

                var stdout = await stdoutTask.ConfigureAwait(false);
                var stderr = await stderrTask.ConfigureAwait(false);

                switch (process.ExitCode)
                {
                    case 0:
                        var truncated = GrepUtility.TruncateContentOutput(stdout, GrepUtility.DefaultMaxOutputChars);
                        return GrepUtility.StripProjectRoot(truncated, projectRoot);

                    case 1:
                        return "No matches found.";

                    case 2:
                        var hint = stderr.Contains("regex parse error")
                            ? " Hint: if your pattern contains regex metacharacters " +
                              "(e.g. ( ) [ ] { } . * + ? | ^ $ \\), use -F for literal " +
                              "string matching. Example: -F \"url(\" instead of \"url(\"."
                            : "";
                        throw new InvalidOperationException($"Ripgrep error: {stderr}{hint}");

                    default:
                        throw new InvalidOperationException(
                            $"Ripgrep exited with code {process.ExitCode}: {stderr}");
                }
            }
            finally
            {
                process.Dispose();
            }
        }

        static string GetRipgrepExecutablePath()
        {
#if UNITY_EDITOR_WIN
            return Path.Combine(RipgrepBasePath, "rg_win.exe");
#elif UNITY_EDITOR_OSX
            return RuntimeInformation.OSArchitecture == Architecture.Arm64
                ? Path.Combine(RipgrepBasePath, "rg_mac_arm64")
                : Path.Combine(RipgrepBasePath, "rg_mac_x64");
#elif UNITY_EDITOR_LINUX
            return Path.Combine(RipgrepBasePath, "rg_linux");
#else
            throw new PlatformNotSupportedException("Unsupported platform for ripgrep.");
#endif
        }

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        static void EnsureRipgrepExecutePermission(string rgPath)
        {
            if (s_RipgrepPermissionSet)
                return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = "/bin/chmod",
                    Arguments = $"+x \"{rgPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                process?.WaitForExit(5000);
                s_RipgrepPermissionSet = true;
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"Failed to set execute permission on {rgPath}: {ex.Message}");
            }
        }
#endif
    }
}
