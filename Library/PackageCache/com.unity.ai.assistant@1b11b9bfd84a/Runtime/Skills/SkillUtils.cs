using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Skills
{
    static class SkillUtils
    {
        static readonly Regex k_UnityPackageVersionRegex =
            new(@"^(>=?|<=?|\^|~|==)?\d+\.\d+\.\d+(-[a-zA-Z]+(\.\d+)?)?$", RegexOptions.Compiled);
        // Matches a single version constraint part (same pattern as package version)
        static readonly Regex k_EditorVersionConstraintPartRegex =
            new(@"^(>=?|<=?|\^|~|==)?\d+\.\d+\.\d+$", RegexOptions.Compiled);
        static readonly Regex k_ValidSkillNameRegex =
            new(@"^[A-Za-z0-9\-]+$", RegexOptions.Compiled); // Only alphanumeric and hyphen
        static readonly Regex k_ValidUnityPackageNameRegex =
            new(@"^[a-z0-9_\-]+(?:\.[a-z0-9_\-]+){2,}$", RegexOptions.Compiled); // At least three dot-separated lowercase segments

        internal static readonly HashSet<string> k_CommonFrontmatterFields = new() {
            "name", "description", "required_packages", "tools", "metadata", "enabled", "required_editor_version"
        };

        class YamlFrontmatter
        {
            public string name;
            public string description;
            public Dictionary<string, string> required_packages;
            public List<string> tools;
            public bool enabled;
            public string required_editor_version;
        }
        
        internal static readonly string CommonFrontmatterFieldNames = string.Join(", ", k_CommonFrontmatterFields);

        /// <summary>
        /// Creates a SkillDefinition by loading a SKILL.md file and scanning the folder for resources.
        /// The skill file YAML frontmatter is used to define the metadata.
        /// </summary>
        /// <param name="skillFile">Absolute path to a SKILL.md file</param>
        /// <returns>A fully populated SkillDefinition</returns>
        /// <exception cref="InvalidOperationException">Thrown when the skill file is invalid or cannot be parsed</exception>
        /// <exception cref="FileNotFoundException">Thrown when the SKILL.md file isn't found</exception>
        internal static SkillDefinition CreateSkillFromFile(string skillFile)
        {
            var skillFolderPath = Path.GetDirectoryName(skillFile);

            try
            {
                var skillMdPath = Path.Combine(skillFolderPath, "SKILL.md");
                var content = GetSkillFileContent(skillMdPath);

                // Parse YAML frontmatter to extract name and description
                var frontmatter = ExtractInfoFromYamlFrontmatter(content);

                if (string.IsNullOrEmpty(frontmatter.name) || string.IsNullOrEmpty(frontmatter.description))
                {
                    InternalLog.LogWarning($"[SkillUtils.FromFolder] Failed to parse name/description from: {skillMdPath}");
                    throw new InvalidOperationException($"Failed to parse name/description from: {skillMdPath}");
                }

                var skill = new SkillDefinition()
                    .WithName(frontmatter.name)
                    .WithDescription(frontmatter.description)
                    .WithPath(skillFile)
                    .WithContent(content)
                    .SetEnabled(frontmatter.enabled);

                if (!string.IsNullOrEmpty(frontmatter.required_editor_version))
                {
                    skill = skill.WithRequiredEditorVersion(frontmatter.required_editor_version);
                }

                // Parse required_packages string (format: "pkg1: version1, pkg2: version2")
                if (frontmatter.required_packages?.Count > 0)
                {
                    foreach (var entry in frontmatter.required_packages)
                    {
                        skill = skill.WithRequiredPackage(entry.Key, entry.Value);
                    }
                }

                // Parse tools (tool names from YAML list)
                if (frontmatter.tools?.Count > 0)
                {
                    foreach (var toolName in frontmatter.tools)
                    {
                        skill = skill.WithTool(toolName);
                    }
                }

                // Collect resource files up to one folder deep (skill folder + immediate subfolders).
                // Deeper nesting is not part of the convention.
                var allFiles = FindResourceFiles(skillFolderPath);
                foreach (var file in allFiles)
                {
                    if (!string.Equals(file, skillMdPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // make the path relative to the skill folder
                        var relativePath = Path.GetRelativePath(skillFolderPath, file);
                        try
                        {
                            var resource = new FileSkillResource(file);
                            skill = skill.WithResource(relativePath, resource);
                        }
                        catch (Exception ex)
                        {
                            InternalLog.LogError($"[SkillUtils.FromFolder] Error adding resource: {ex.Message}");
                        }
                    }
                }

                return skill;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (FileNotFoundException)
            {
                throw;
            }
            catch (ArgumentException ex)
            {
                throw new InvalidOperationException($"Invalid skill data in '{skillFolderPath}': {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[SkillUtils.FromFolder] Error scanning folder '{skillFolderPath}': {ex.Message}");
                throw new InvalidOperationException($"Error scanning skill file: {ex.Message}", ex);
            }
        }

        private static string GetSkillFileContent(string skillMdPath)
        {
            if (!File.Exists(skillMdPath))
                throw new FileNotFoundException($"SKILL.md not found at: {skillMdPath}");
                
            var fileInfo = new FileInfo(skillMdPath);
            if (fileInfo.Length == 0)
                throw new FileLoadException($"SKILL.md file is empty: {skillMdPath}");

            // Validation and the strict full-file read live in the shared helper, so corrupt UTF-8
            // beyond the sampled 4 KB is caught rather than silently replaced with U+FFFD.
            // Unlike resources, SKILL.md must be saved without a BOM.
            return TextFileUtils.ReadUtf8TextFile(skillMdPath, allowBom: false);
        }

        static YamlFrontmatter ExtractInfoFromYamlFrontmatter(string content)
        {
            var frontmatter = GetFrontmatterFromSkillFile(content);

            // Extract the "name" field from the frontmatter
            var name = ExtractFrontmatterScalarField(frontmatter, "name");

            // Extract the "description" field from the frontmatter
            var description = ExtractFrontmatterScalarField(frontmatter, "description", allowMultilineFold: true);

            // Extract the "packages" field from the frontmatter, as a dictionary
            var packageDict = ExtractFrontmatterMappingField(frontmatter, "required_packages", true);

            // Extract the "tools" field from the frontmatter, as a list
            var toolsList = ExtractFrontmatterListField(frontmatter, "tools", true);

            // Extract the "enabled" field from the frontmatter
            var enabledValue = ExtractFrontmatterScalarField(frontmatter, "enabled", true);
            var enabledAsBool = true;

            if (!string.IsNullOrEmpty(enabledValue))
            {
                // Variations of 'true' and 'false' are modern standard; throw if the value is simply unexpected
                if (!bool.TryParse(enabledValue, out enabledAsBool))
                {
                    throw new InvalidOperationException($"Invalid 'enabled' field value in YAML frontmatter: '{enabledValue}'. Expected 'True' or 'False', or variations like 'true'/'TRUE'.");
                }
            }

            // Extract the "required_editor_version" field from the frontmatter
            var requiredEditorVersion = ExtractFrontmatterScalarField(frontmatter, "required_editor_version", isOptional: true, requireQuotedIfOperator: true);

            var frontmatterResult = new YamlFrontmatter()
            {
                name = name,
                description = description,
                required_packages = packageDict,
                tools = toolsList,
                enabled = enabledAsBool,
                required_editor_version = requiredEditorVersion
            };

            return frontmatterResult;
        }

        private static string GetFrontmatterFromSkillFile(string content)
        {
            // Match YAML frontmatter block (content between --- delimiters at start of file)
            var frontmatterPattern = @"^---\s*\n(.*?)\n---";
            var frontmatterMatch = Regex.Match(content, frontmatterPattern, RegexOptions.Singleline);

            if (!frontmatterMatch.Success)
            {
                throw new InvalidOperationException("Missing or invalid YAML frontmatter (must start with --- and end with ---)");
            }

            var frontmatter = frontmatterMatch.Groups[1].Value;
            return frontmatter;
        }

        /// <summary>
        /// Validates that every top-level field in the YAML frontmatter block is in the known set.
        /// Returns any field name like "fieldname:" we didn't whitelist.
        /// </summary>
        /// <returns>A list of unknown field names found in the frontmatter (without the colon), or null if there was nothing to return.</returns>
        internal static List<string> GetUncommonFrontmatterFields(string skillFileContent, string skillFilePath)
        {
            try
            {
                string frontmatter = GetFrontmatterFromSkillFile(skillFileContent);

                List<string> uncommonFields = new List<string>();

                var lines = frontmatter.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

                // Top-level field lines aren't necessarily at column 0 — the whole block may be
                // uniformly indented (see FromFolder_UniformlyIndentedBlock_* tests). The base
                // indentation is whatever the least-indented non-blank line uses; anything more
                // indented than that is a nested value (list item, mapping entry, folded
                // continuation) and should be skipped, mirroring how the field extractors treat
                // indentation as relative to the field's own indent rather than an absolute column.
                var nonBlankLines = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
                if (nonBlankLines.Count == 0)
                    return uncommonFields;

                int baseIndent = nonBlankLines.Min(CountLeadingWhitespace);

                foreach (var line in lines)
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;

                    // More indented than the block's base level: nested value — skip.
                    if (CountLeadingWhitespace(line) > baseIndent)
                        continue;

                    var colonIdx = line.IndexOf(':');
                    if (colonIdx <= 0)
                        continue;

                    var fieldName = line.Substring(0, colonIdx).Trim();
                    if (!k_CommonFrontmatterFields.Contains(fieldName))
                        uncommonFields.Add(fieldName);
                }

                return uncommonFields;
            }
            catch (Exception ex)
            {
                InternalLog.LogError($"[SkillUtils.GetUncommonFrontmatterFields] Error loading or validating frontmatter in '{skillFilePath}': {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Extract a simple scalar field from YAML frontmatter, return as a string.
        /// YAML example: A "name" scalar starts with "name:" followed in the same line by a space and value like "my_skill_name".
        /// When <paramref name="allowMultilineFold"/> is true, also supports a folded multi-line scalar:
        /// "fieldname:" with no same-line value, followed by indented lines joined with spaces, ending at
        /// a blank line or a line indented no more than the field's own line. Only enable this for prose fields (e.g. description) — fields with
        /// strict single-token/format requirements (name, enabled, required_editor_version) should stay
        /// same-line-only so malformed indentation fails fast instead of silently folding into garbage.
        /// When <paramref name="requireQuotedIfOperator"/> is true, values containing version operators must be surrounded by quotes.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a required field is missing or empty, or when an operator-prefixed value is not properly quoted</exception>
        static string ExtractFrontmatterScalarField(string frontmatter, string fieldName, bool isOptional = false, bool requireQuotedIfOperator = false, bool allowMultilineFold = false)
        {
            var lines = frontmatter.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var fieldLinePattern = $@"^[ \t]*{fieldName}[ \t]*:(.*)$";

            for (int i = 0; i < lines.Length; i++)
            {
                var match = Regex.Match(lines[i], fieldLinePattern);
                if (!match.Success)
                    continue;

                int fieldIndent = CountLeadingWhitespace(lines[i]);
                var rawValue = match.Groups[1].Value.Trim();

                if (!string.IsNullOrEmpty(rawValue))
                {
                    var value = rawValue.Trim('"', '\'');
                    if (requireQuotedIfOperator)
                        AssertVersionConstraintIsQuoted(rawValue, value, $"{fieldName}: \"{value}\"");

                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
                else if (allowMultilineFold)
                {
                    // No value on the field's own line: fold indented continuation lines into
                    // a single value, the same way ExtractFrontmatterListField/MappingField already
                    // treat indentation as "belongs to the field above".
                    var folded = new List<string>();
                    for (int j = i + 1; j < lines.Length; j++)
                    {
                        var line = lines[j];
                        if (string.IsNullOrWhiteSpace(line))
                            break;

                        int leadingSpaces = CountLeadingWhitespace(line);
                        if (leadingSpaces <= fieldIndent)
                            break;

                        folded.Add(line.Trim());
                    }

                    if (folded.Count > 0)
                    {
                        var foldedRaw = string.Join(" ", folded);
                        var value = foldedRaw.Trim('"', '\'');
                        // Mirrors the same-line branch above so a folded value can't bypass the version-quote check.
                        if (requireQuotedIfOperator)
                            AssertVersionConstraintIsQuoted(foldedRaw, value, $"{fieldName}: \"{value}\"");

                        if (!string.IsNullOrWhiteSpace(value))
                            return value;
                    }
                }

                if (!isOptional)
                    throw new InvalidOperationException($"Empty '{fieldName}' field value in YAML frontmatter");

                return null;
            }

            if (!isOptional)
                throw new InvalidOperationException($"Missing '{fieldName}' field in YAML frontmatter");

            return null;
        }

        /// <summary>
        /// Extract a list field from YAML frontmatter, return as a C# list of strings.
        /// YAML example: A "tools" list starts with "tools:" followed by indented list items like "  - Unity.Profiler.Initialize".
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a required field is missing or has invalid format</exception>
        static List<string> ExtractFrontmatterListField(string frontmatter, string fieldName, bool optional = false)
        {
            var lines = frontmatter.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int listStartIdx = -1;
            int fieldIndent = 0;

            // Find the field line (e.g., tools:)
            var fieldMarker = fieldName + ":";
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.Trim();
                if (trimmed == fieldMarker)
                {
                    fieldIndent = CountLeadingWhitespace(line);
                    listStartIdx = i + 1;
                    break;
                }
            }

            if (listStartIdx == -1)
            {
                if (optional)
                {
                    return null;
                }
                throw new InvalidOperationException($"Field '{fieldName}' not found as a list in frontmatter.");
            }

            // Parse indented lines that start with "- "
            var list = new List<string>();
            for (int i = listStartIdx; i < lines.Length; i++)
            {
                var line = lines[i];

                // End of this list block, we don't expect empty lines
                if (string.IsNullOrWhiteSpace(line))
                    break;

                int leadingSpaces = CountLeadingWhitespace(line);

                // End of this list block, we expect only valid entries with the correct indentation
                if (leadingSpaces <= fieldIndent)
                    break;

                var trimmed = line.Trim();

                // Check if this is a list item (starts with "- ")
                if (trimmed.StartsWith("- "))
                {
                    var value = trimmed.Substring(2).Trim().Trim('"', '\'');
                    if (string.IsNullOrEmpty(value))
                    {
                        throw new InvalidOperationException($"Empty list item in '{fieldName}' in YAML frontmatter: '{line}'. Expected format: '  - item_value'.");
                    }
                    list.Add(value);
                }
                else
                {
                    // End of this list block, invalid line without list marker
                    throw new InvalidOperationException($"List '{fieldName}' in YAML frontmatter stopped at invalid line: '{line}'. Expected format: '  - item_value'.");
                }
            }
            return list;
        }

        /// <summary>
        /// Extract a mapping field from YAML frontmatter, return as a C# dictionary.
        /// YAML example: A "packages" mapping starts with "packages:" followed by indented key/value lines like "  com.unity.2d.tooling: 1.0.0".
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when a required field is missing or has invalid format</exception>
        static Dictionary<string, string> ExtractFrontmatterMappingField(string frontmatter, string fieldName, bool optional = false)
        {
            var lines = frontmatter.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            int dictStartIdx = -1;
            int fieldIndent = 0;

            // Find the field line (e.g., required_packages:)
            var fieldMarker = fieldName + ":";
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;
                var trimmed = line.Trim();
                if (trimmed == fieldMarker)
                {
                    fieldIndent = CountLeadingWhitespace(line);
                    dictStartIdx = i + 1;
                    break;
                }
            }
            
            if (dictStartIdx == -1)
            {
                if (optional)
                {
                    return null;
                }
                throw new InvalidOperationException($"Field '{fieldName}' not found as a mapping in frontmatter.");
            }
            
            // Parse indented lines of key/value pairs
            var dict = new Dictionary<string, string>();
            for (int i = dictStartIdx; i < lines.Length; i++)
            {
                var line = lines[i];

                // End of this mapping block, we don't expect empty lines
                if (string.IsNullOrWhiteSpace(line))
                    break;
                
                int leadingSpaces = CountLeadingWhitespace(line);

                // End of this mapping block, we expect only valid entries with the correct indentation
                if (leadingSpaces <= fieldIndent)
                    break;
                
                var trimmed = line.Trim();

                if (trimmed.StartsWith("- "))
                    throw new InvalidOperationException($"Mapping '{fieldName}' in YAML frontmatter contains a list marker at line: '{line.Trim()}'. Use the mapping format '  key: value' instead.");

                int colonIdx = trimmed.IndexOf(':');
                if (colonIdx > 0)
                {
                    var key = trimmed.Substring(0, colonIdx).Trim().Trim('"', '\'');
                    var rawValue = trimmed.Substring(colonIdx + 1).Trim();
                    var value = rawValue.Trim('"', '\'');

                    AssertVersionConstraintIsQuoted(rawValue, value, $"{key}: \"{value}\"");

                    if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(value))
                    {
                        throw new InvalidOperationException($"Invalid key/value pair in mapping '{fieldName}' in YAML frontmatter: '{line}'. Both key and value must be non-empty, ex.: '  package.name: version'.");
                    }

                    dict[key] = value;
                }
                else
                {
                    // End of this mapping block, invalid line without key/value pair
                    throw new InvalidOperationException($"Mapping '{fieldName}' in YAML frontmatter stopped at invalid line: '{line}'. A format like '  package.name: version' is expected.");
                }
            }
            return dict;
        }

        /// <summary>
        /// Scans <paramref name="absolutePath"/> for SKILL.md files <paramref name="depth"/> folder levels deep
        /// and loads each as a tagged <see cref="SkillDefinition"/>. Parse failures are collected in <paramref name="errors"/>.
        /// </summary>
        internal static void LoadSkillsFromFolder(string absolutePath, string tag, List<SkillDefinition> skills,
            List<SkillFileIssue> errors, int depth = 1)
        {
            var skillFiles = FindSkillFiles(absolutePath, depth);
            InternalLog.Log($"[SkillUtils] Found {skillFiles.Count} SKILL.md file(s) under: {absolutePath}");
            LoadSkillFiles(skillFiles, tag, skills, errors);
        }

        /// <summary>
        /// Parses each SKILL.md path into a <see cref="SkillDefinition"/> tagged with <paramref name="tag"/>.
        /// Parse failures are collected in <paramref name="errors"/> rather than thrown.
        /// </summary>
        internal static void LoadSkillFiles(IEnumerable<string> absolutePaths, string tag,
            List<SkillDefinition> skills, List<SkillFileIssue> errors)
        {
            foreach (var skillFile in absolutePaths)
            {
                try
                {
                    skills.Add(CreateSkillFromFile(skillFile).WithTag(tag));
                }
                catch (Exception ex)
                {
                    var skillName = TryGetSkillName(skillFile) ?? "(unknown skill name)";
                    errors.Add(new SkillFileIssue(skillName, skillFile, ex.Message, SkillFileIssue.ErrorLevel.Critical));
                }
            }
        }

        /// <summary>
        /// Attempts to read the skill name from a SKILL.md file without throwing.
        /// Returns null if the file cannot be read or the name field is missing or unparseable.
        /// Reads the file directly (bypassing text-file validation) so the name is still
        /// available for error reporting even when validation fails.
        /// </summary>
        static string TryGetSkillName(string skillFile)
        {
            try
            {
                // Cap at 4096 bytes so binary or newline-free files can't stall or allocate
                // unboundedly in the error path. Frontmatter always fits within this limit.
                using var fs = new FileStream(skillFile, FileMode.Open, FileAccess.Read);
                var len = (int)Math.Min(fs.Length, 4096);
                if (len == 0) return null;
                var buffer = new byte[len];
                var readCount = fs.Read(buffer, 0, len);
                // Skip a UTF-8 BOM if present so the frontmatter regex matches the leading ---.
                var bomOffset = readCount >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF ? 3 : 0;
                var sample = System.Text.Encoding.UTF8.GetString(buffer, bomOffset, readCount - bomOffset);

                // Confine the name: search to the frontmatter region; accept a missing closing
                // --- so files with large frontmatter still report a name on failure.
                var opening = Regex.Match(sample, @"^---[ \t]*\r?\n");
                if (!opening.Success) return null;
                var afterOpening = sample.Substring(opening.Length);
                var closing = Regex.Match(afterOpening, @"^---[ \t]*(\r?\n|$)", RegexOptions.Multiline);
                var frontmatterRegion = closing.Success ? afterOpening.Substring(0, closing.Index) : afterOpening;
                var nameMatch = Regex.Match(frontmatterRegion, @"^[ \t]*name[ \t]*:[ \t]*(.+?)[ \t]*$", RegexOptions.Multiline);
                return nameMatch.Success ? nameMatch.Groups[1].Value.Trim('"', '\'') : null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Returns SKILL.md paths exactly <paramref name="depth"/> folder levels below <paramref name="root"/>
        /// (default 1: immediate subfolders, e.g. root/skill-name/SKILL.md).
        /// </summary>
        static List<string> FindSkillFiles(string root, int depth)
        {
            var results = new List<string>();
            CollectSkillFilesRecursive(root, depth, results);
            return results;
        }

        static void CollectSkillFilesRecursive(string directory, int depth, List<string> results)
        {
            if (!Directory.Exists(directory))
                return;

            if (depth <= 0)
            {
                var candidate = Path.Combine(directory, "SKILL.md");
                if (File.Exists(candidate))
                    results.Add(candidate);
                return;
            }

            foreach (var subdir in Directory.GetDirectories(directory))
                CollectSkillFilesRecursive(subdir, depth - 1, results);
        }
        
        /// <summary>
        /// Returns all files in <paramref name="skillFolderPath"/> and its immediate subfolders.
        /// Ignores .meta files.
        /// </summary>
        static string[] FindResourceFiles(string skillFolderPath)
        {
            var files = new List<string>(Directory.GetFiles(skillFolderPath, "*", SearchOption.TopDirectoryOnly));
            foreach (var subdir in Directory.GetDirectories(skillFolderPath))
                files.AddRange(Directory.GetFiles(subdir, "*", SearchOption.TopDirectoryOnly));
            files.RemoveAll(f => Path.GetExtension(f).Equals(".meta", StringComparison.OrdinalIgnoreCase));
            return files.ToArray();
        }
        
        public static bool IsValidUnityPackageName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            return k_ValidUnityPackageNameRegex.IsMatch(name);
        }

        public static bool IsValidUnityPackageVersion(string version)
        {
            if (string.IsNullOrWhiteSpace(version))
                return false;
            var parts = version.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                    return false;
                if (!k_UnityPackageVersionRegex.IsMatch(trimmed))
                    return false;
            }
            return true;
        }

        public static bool IsValidEditorVersionConstraint(string constraint)
        {
            if (string.IsNullOrWhiteSpace(constraint))
                return false;
            var parts = constraint.Split(',');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.Length == 0)
                    return false;
                if (!k_EditorVersionConstraintPartRegex.IsMatch(trimmed))
                    return false;
            }
            return true;
        }

        public static bool IsValidSkillName(string toolId)
        {
            // Only allow alphanumeric and hyphen, underscore is not allowed
            if (string.IsNullOrEmpty(toolId))
                return false;
            return k_ValidSkillNameRegex.IsMatch(toolId);
        }

        static int CountLeadingWhitespace(string line) => line.TakeWhile(char.IsWhiteSpace).Count();

        static bool HasVersionOperator(string value) =>
            value.StartsWith(">=", StringComparison.Ordinal) ||
            value.StartsWith("<=", StringComparison.Ordinal) ||
            value.StartsWith("==", StringComparison.Ordinal) ||
            value.StartsWith(">", StringComparison.Ordinal) ||
            value.StartsWith("<", StringComparison.Ordinal) ||
            value.StartsWith("^", StringComparison.Ordinal) ||
            value.StartsWith("~", StringComparison.Ordinal);

        static bool IsQuotedString(string value) =>
            value.Length >= 2 &&
            ((value[0] == '"' && value[value.Length - 1] == '"') ||
             (value[0] == '\'' && value[value.Length - 1] == '\''));

        /// <summary>
        /// Throws if <paramref name="value"/> (already stripped of surrounding quotes) starts with a version
        /// operator but <paramref name="rawValue"/> was not properly quoted in the YAML source.
        /// </summary>
        static void AssertVersionConstraintIsQuoted(string rawValue, string value, string useHint)
        {
            if (!HasVersionOperator(value) || IsQuotedString(rawValue))
                return;

            var hasPartialQuote = rawValue.Length > 0 &&
                (rawValue[0] == '"' || rawValue[0] == '\'' ||
                 rawValue[rawValue.Length - 1] == '"' || rawValue[rawValue.Length - 1] == '\'');

            var message = hasPartialQuote
                ? $"Version constraint '{rawValue}' has unmatched quotes - both an opening and closing quote are required. Use: {useHint}"
                : $"Version constraint '{rawValue}' must be quoted in YAML frontmatter - operators like >= are reserved YAML characters. Use: {useHint}";

            throw new InvalidOperationException(message);
        }
    }
}
