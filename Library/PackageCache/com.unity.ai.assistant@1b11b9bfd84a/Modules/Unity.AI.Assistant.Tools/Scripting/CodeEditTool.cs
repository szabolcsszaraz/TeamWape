using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Bridge.Editor;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.Editor.Analytics;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.FunctionCalling;
using Unity.AI.Assistant.Utils;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class CodeEditTool
    {
        internal const string k_FunctionId = "Unity.CodeEdit";

        enum FileType
        {
            CSharp,
            Manifest,
            Shader,
            Other,
            Uss
        }

        [Serializable]
        public struct CodeEditOutput
        {
            [JsonProperty("result")]
            public string Result;

            [JsonProperty("compilationOutput")]
            public string CompilationOutput;
        }

        [AgentTool(
            "Edit scripts (csharp, uxml, uss etc.) using precise string replacement or save scripts with source code. Editing existing script requires exact literal text matching. For creating new scripts, use empty oldString. File paths can be relative to Unity project root (e.g., \"Assets/Scripts/Player.cs\") or absolute. Automatically validates C# compilation after edits.",
            k_FunctionId)]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_CodeEditTag)]
        public static async Task<CodeEditOutput> SaveCode(
            ToolExecutionContext context,
            [ToolParameter("Path to the file to modify or create. Can be relative to Unity project root (e.g., \"Assets/Scripts/Player.cs\") or absolute.")]
            string filePath,
            [ToolParameter("Short description of the changes being made")]
            string description,
            [ToolParameter("Exact literal text to replace oldString with. For new files, this becomes the entire file content. Include proper whitespace, indentation, and ensure resulting code is correct.")]
            string newString,
            [ToolParameter("Exact literal text to replace. Must include sufficient context (3+ lines) to uniquely identify the location. Match whitespace and indentation precisely. For new files ONLY, use empty string otherwise a valid value has to be supplied.")]
            string oldString = "",
            [ToolParameter("Number of occurrences expected to be replaced. Defaults to 1.")]
            int expectedOccurrences = 1)
        {
            try
            {
                // Resolve file path (handle relative paths from Unity project root)
                var resolvedPath = ResolvePath(filePath);
                ValidatePathSecurity(resolvedPath);

                // Read original file content or empty string for new files
                var originalCode = "";
                var isNewFile = false;

                if (File.Exists(resolvedPath))
                {
                    await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Modify, resolvedPath);
                    if (FileUtils.ExceedsMaxReadSize(resolvedPath, out var sizeMB))
                        return new CodeEditOutput { Result = $"File '{filePath}' is too large to edit ({sizeMB:F1} MB). Only files under {AssistantConstants.MaxGetFileContentSizeMB} MB are supported." };
                    originalCode = await File.ReadAllTextAsync(resolvedPath);
                }
                else
                {
                    await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, resolvedPath);
                    isNewFile = true;
                    // Ensure parent directory exists for new files
                    var directory = Path.GetDirectoryName(resolvedPath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }
                }

                // Handle file creation: if new file and oldString is empty, treat as file creation
                string modifiedCode;
                if (isNewFile)
                {
                    if (!string.IsNullOrEmpty(oldString))
                        throw new InvalidOperationException("Cannot specify oldString when creating a new file. Use empty oldString for file creation.");

                    modifiedCode = newString;
                }
                else
                {
                    if (string.IsNullOrEmpty(oldString))
                        throw new InvalidOperationException("File already exists and no oldString was provided. oldString is required for editing existing files.");

                    var lineEnding = originalCode.Contains("\r\n") ? "\r\n" : "\n";
                    
                    var flexPattern = BuildFlexibleWhitespacePattern(oldString);
                    var flexRegex = new Regex(flexPattern, RegexOptions.Multiline);
                    var candidates = flexRegex.Matches(originalCode);
                    
                    var normalizedOld = NormalizeForMatch(oldString);
                    int strictMatchCount = 0;

                    foreach (Match match in candidates)
                    {
                        if (NormalizeForMatch(match.Value) == normalizedOld)
                        {
                            strictMatchCount++;
                        }
                    }

                    if (strictMatchCount == expectedOccurrences)
                    {
                        var normalizedNew = NormalizeLineEndings(newString);
                        
                        modifiedCode = flexRegex.Replace(originalCode, match => 
                        {
                            if (NormalizeForMatch(match.Value) == normalizedOld)
                            {
                                var adapted = AdaptIndentation(normalizedNew, match.Value);
                                return lineEnding == "\r\n" ? Regex.Replace(adapted, @"(?<!\r)\n", "\r\n") : adapted;
                            }
                            
                            return match.Value;
                        });
                    }
                    else
                    {
                        if (candidates.Count > 0)
                        {
                            throw new InvalidOperationException(
                                $"The specified oldString was not found with the expected indentation (fuzzy match found {strictMatchCount} matches, expected {expectedOccurrences}). " +
                                $"However, similar block(s) were found with different indentation. Please retry using this exact text for 'oldString':\n\n" +
                                candidates[0].Value);
                        }

                        throw new InvalidOperationException(
                            "The specified oldString was not found in the file. Ensure the text matches exactly, including whitespace and indentation.");
                    }
                }

                await File.WriteAllTextAsync(resolvedPath, modifiedCode);

                AssetDatabase.Refresh();

                var fileType = GetFileType(resolvedPath);

                switch (fileType)
                {
                    case FileType.Other:
                    {
                        var outputMessage = isNewFile
                            ? $"The file was successfully created and saved at {Path.GetFileName(resolvedPath)}"
                            : $"The file was successfully edited and saved at {Path.GetFileName(resolvedPath)}";

                        return new CodeEditOutput { Result = outputMessage, CompilationOutput = string.Empty };
                    }
                    case FileType.Uss:
                    {
                        var compilationOutput = UssValidator.ValidateUss(modifiedCode);

                        var resultMessage = (isNewFile
                                                ? $"The file was successfully created and saved at {Path.GetFileName(resolvedPath)}"
                                                : $"The file was successfully edited and saved at {Path.GetFileName(resolvedPath)}")
                                            + (string.IsNullOrEmpty(compilationOutput)
                                                ? ", and USS validation passed."
                                                : ", but it contains USS import errors that need to be fixed.");

                        return new CodeEditOutput
                        {
                            Result = resultMessage,
                            CompilationOutput = compilationOutput
                        };
                    }
                    case FileType.Shader:
                    {
                        // Check if project is compiling for shaders as well
                        var compilationResult = await ProjectScriptCompilation.RequestProjectCompilation();

                        var loadPath = GetRelativeAssetPath(resolvedPath);
                        var shader = AssetDatabase.LoadAssetAtPath<Shader>(loadPath);
                        if (shader != null)
                        {
                            if (ShaderUtil.ShaderHasError(shader))
                            {
                                var shaderMessages = ShaderUtil.GetShaderMessages(shader);
                                var errorMessage = string.Join("\n", System.Linq.Enumerable.Select(shaderMessages, m => $"{m.severity}: {m.message} (line {m.line})"));

                                var shaderErrorCount = System.Linq.Enumerable.Count(shaderMessages, m => m.severity == UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error);
                                AIAssistantAnalytics.ReportGatewayCompileResultEvent("fail", "shader", shaderErrorCount);

                                var shaderCompilationMessage = (isNewFile
                                    ? $"The file was successfully created and saved at {Path.GetFileName(resolvedPath)}"
                                    : $"The file was successfully edited and saved at {Path.GetFileName(resolvedPath)}")
                                    + ", but it now contains compilation errors that need to be fixed.";

                                return new CodeEditOutput
                                {
                                    Result = shaderCompilationMessage,
                                    CompilationOutput = errorMessage
                                };
                            }    
                        }
                        else
                        {
                            AIAssistantAnalytics.ReportGatewayCompileResultEvent("fail", "shader", 0);
                            return new CodeEditOutput
                            {
                                Result = $"The file was saved, however the shader could not be loaded at {Path.GetFileName(resolvedPath)}",
                                CompilationOutput = $"AssetDatabase.LoadAssetAtPath<Shader>({loadPath}) returned null"
                            };
                        }

                        AIAssistantAnalytics.ReportGatewayCompileResultEvent("pass", "shader", 0);

                        var successMessage = (isNewFile
                            ? $"The file was successfully created and saved at {Path.GetFileName(resolvedPath)}"
                            : $"The file was successfully edited and saved at {Path.GetFileName(resolvedPath)}")
                            + ", and it compiled successfully.";

                        return new CodeEditOutput { Result = successMessage, CompilationOutput = string.Empty };
                    }
                    default:
                    {
                        // Check if project is compiling for C# and manifest files
                        var compilationResult = await ProjectScriptCompilation.RequestProjectCompilation();
                        var compilationOutput = compilationResult.ErrorMessage;
                        var compilationMessage = isNewFile
                            ? $"The file was successfully created and saved at {Path.GetFileName(resolvedPath)}"
                            : $"The file was successfully edited and saved at {Path.GetFileName(resolvedPath)}";

                        if (compilationResult.Success)
                        {
                            compilationMessage += ", and it compiled successfully.";
                        }
                        else
                        {
                            compilationMessage += ", but it now contains compilation errors that need to be fixed.";
                        }

                        var csharpErrorCount = compilationResult.Success || string.IsNullOrEmpty(compilationResult.ErrorMessage)
                            ? 0
                            : System.Linq.Enumerable.Count(compilationResult.ErrorMessage.Split('\n'), l => l.Contains("): error "));
                        AIAssistantAnalytics.ReportGatewayCompileResultEvent(
                            compilationResult.Success ? "pass" : "fail",
                            "csharp",
                            csharpErrorCount);
                        
                        if (modifiedCode.Contains("Input", StringComparison.OrdinalIgnoreCase))
                        {
                            try
                            {
                                var inputSystemAnalysis = InputSystemAnalyzer.Analyze(modifiedCode);
                                if (inputSystemAnalysis != null)
                                    compilationOutput += "\n\n" + inputSystemAnalysis;
                            }
                            catch (Exception e)
                            {
                                InternalLog.LogException(e);
                            }
                        }

                        var result = new CodeEditOutput
                        {
                            Result = compilationMessage,
                            CompilationOutput = compilationOutput
                        };

                        if (compilationResult.Success)
                            ProjectScriptCompilation.ForceDomainReload();

                        return result;        
                    }
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogException(ex);
                throw;
            }
        }

        static string ResolvePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
                return Path.GetFullPath(filePath);

            var projectPath = Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectPath, filePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        static void ValidatePathSecurity(string resolvedPath)
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var fullResolved = Path.GetFullPath(resolvedPath);

            if (!fullResolved.StartsWith(projectRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullResolved, projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{resolvedPath}' resolves outside the project directory. File operations are restricted to the project scope.");
            }
        }

        static FileType GetFileType(string filePath)
        {
            if (filePath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase))
                return FileType.CSharp;

            if (filePath.EndsWith("manifest.json", StringComparison.OrdinalIgnoreCase))
                return FileType.Manifest;

            if (filePath.EndsWith(".shader", StringComparison.OrdinalIgnoreCase))
                return FileType.Shader;

            if (filePath.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                return FileType.Uss;

            return FileType.Other;
        }

        internal static string NormalizeLineEndings(string s)
        {
            return s.Replace("\r\n", "\n").Replace("\r", "\n");
        }

        internal static string NormalizeForMatch(string s)
        {
            s = NormalizeLineEndings(s);
            s = s.Replace("\t", "    ");
            s = Regex.Replace(s, @"[ \t]+$", "", RegexOptions.Multiline);
            return s;
        }

        internal static string BuildFlexibleWhitespacePattern(string oldString)
        {
            var normalized = oldString.Replace("\r\n", "\n").Replace("\r", "\n");
            var lines = normalized.Split('\n');
            var patternLines = new string[lines.Length];

            for (int i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim(' ', '\t');
                patternLines[i] = trimmed.Length == 0
                    ? @"^[ \t]*"
                    : @"^[ \t]*" + Regex.Escape(trimmed) + @"[ \t]*";
            }

            return string.Join(@"\r?\n", patternLines);
        }

        internal static string AdaptIndentation(string replacement, string matchedOriginal)
        {
            var lines = replacement.Split('\n');
            if (lines.Length == 0) return replacement;

            string targetIndentation = "";
            foreach (var line in matchedOriginal.Split('\n'))
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    targetIndentation = Regex.Match(line, @"^([ \t]*)").Groups[1].Value;
                    break;
                }
            }

            string refIndentation = "";
            foreach (var line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    refIndentation = Regex.Match(line, @"^([ \t]*)").Groups[1].Value;
                    break;
                }
            }

            if (targetIndentation == refIndentation)
                return replacement;

            for (var i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i]))
                    continue;

                var lineContent = lines[i].TrimStart(' ', '\t');
                var lineIndent = lines[i].Substring(0, lines[i].Length - lineContent.Length);

                if (lineIndent.StartsWith(refIndentation))
                {
                    // At or deeper than ref: swap ref prefix for target, keep excess.
                    lines[i] = targetIndentation + lineIndent.Substring(refIndentation.Length) + lineContent;
                }
                else
                {
                    // Shallower than ref: proportionally trim target indentation.
                    var shortfall = refIndentation.Length - lineIndent.Length;
                    var trimmedLen = Math.Max(0, Math.Min(targetIndentation.Length, targetIndentation.Length - shortfall));
                    lines[i] = targetIndentation.Substring(0, trimmedLen) + lineContent;
                }
            }

            return string.Join("\n", lines);
        }

        static string GetRelativeAssetPath(string absolutePath)
        {
            var projectPath = Directory.GetCurrentDirectory();
            if (absolutePath.StartsWith(projectPath, StringComparison.OrdinalIgnoreCase))
            {
                var relativePath = absolutePath.Substring(projectPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relativePath.Replace(Path.DirectorySeparatorChar, '/');
            }
            return absolutePath;
        }
    }
}
