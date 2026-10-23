using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Unity.AI.Assistant.Tools.Editor
{
    /// <summary>
    /// Pure utility methods for building ripgrep arguments and
    /// post-processing ripgrep output. Extracted from Grep so the logic
    /// can be tested without spawning a process or an agent tool context.
    /// </summary>
    static class GrepUtility
    {
        internal const int DefaultMaxOutputChars = 8192;
        internal const int DefaultMaxOutputLines = 80;

        static readonly string[] k_DefaultExcludeGlobs =
        {
            "*.fbx",
            "*.obj",
        };

        // Compiled once at type-init; reused for every Grep invocation.
        static readonly Regex k_ColorFlagRegex = new(@"--color(?:\s+|=)\w+", RegexOptions.Compiled);
        static readonly Regex k_BareSeparatorRegex = new(@"(?<=\s|^)--(?=\s|$)", RegexOptions.Compiled);
        static readonly Regex k_TripleDashPatternRegex = new(@"(-e\s+)?""(---[^""]*)""", RegexOptions.Compiled);
        static readonly Regex k_ExtraWhitespaceRegex = new(@"\s{2,}", RegexOptions.Compiled);
        static readonly Regex k_FilesFlagRegex = new(@"(^|\s)--files(\s|$)", RegexOptions.Compiled);
        static readonly Regex k_ShortFileFilterRegex = new(@"(^|\s)-[a-zA-Z]*[tg]\s", RegexOptions.Compiled);

        /// <summary>
        /// Builds the complete ripgrep argument string from user-provided
        /// rg arguments and one or more project search paths.
        /// Enforces project-scoping and output safety while letting the
        /// caller pass any standard ripgrep flags.
        /// </summary>
        internal static string BuildArguments(
            string userArgs,
            params string[] searchPaths)
        {
            var quotedPaths = FormatSearchPaths(searchPaths);
            bool isFileListing = IsFileListingMode(userArgs);

            var args = new StringBuilder();

            // Enforced flags (not overridable)
            args.Append("--color never ");
            args.Append("--iglob \"!*.meta\" ");

            // Sensible defaults for content search (placed before user args so they can be overridden)
            if (!isFileListing)
            {
                args.Append("-n --heading ");

                if (!HasFileFilter(userArgs))
                {
                    // Default to C# files only — prevents noisy matches in
                    // .shadergraph, .prefab, .unity, .asset files.
                    // Override with explicit --glob, --iglob, or --type.
                    args.Append("--type cs ");

                    foreach (var glob in k_DefaultExcludeGlobs)
                    {
                        args.Append($"--glob \"!{glob}\" ");
                    }
                }
            }

            // User-provided ripgrep arguments (pattern included)
            var sanitized = SanitizeUserArgs(userArgs);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                args.Append(sanitized);
                args.Append(' ');
            }

            // Restrict search to project directories
            args.Append("-- ");
            args.Append(quotedPaths);

            return args.ToString().TrimEnd();
        }

        /// <summary>
        /// Removes flags that conflict with our enforced settings, strips
        /// bare <c>--</c> separators (we add our own before search paths), and
        /// applies <see cref="ProtectTripleDashPatterns"/>.
        /// </summary>
        internal static string SanitizeUserArgs(string userArgs)
        {
            if (string.IsNullOrWhiteSpace(userArgs))
                return "";

            var sanitized = k_ColorFlagRegex.Replace(userArgs, "");
            sanitized = k_BareSeparatorRegex.Replace(sanitized, "");
            sanitized = ProtectTripleDashPatterns(sanitized);
            sanitized = k_ExtraWhitespaceRegex.Replace(sanitized, " ");

            return sanitized.Trim();
        }

        /// <summary>
        /// ripgrep parses any token starting with <c>--</c> as a long-form flag
        /// and rejects unknown ones. Patterns starting with <c>---</c> are common
        /// in Unity YAML scenes/prefabs (e.g. <c>"--- !u!29 &amp;1"</c>), so we
        /// prepend <c>-e</c> to force ripgrep to treat the next token as a search
        /// pattern. Tokens already preceded by <c>-e</c> are left untouched.
        /// </summary>
        static string ProtectTripleDashPatterns(string input)
        {
            return k_TripleDashPatternRegex.Replace(
                input,
                m => m.Groups[1].Success ? m.Value : "-e \"" + m.Groups[2].Value + "\"");
        }

        /// <summary>
        /// Returns true when the user args contain <c>--files</c>, indicating
        /// a file-listing operation rather than a content search.
        /// </summary>
        internal static bool IsFileListingMode(string userArgs)
        {
            if (string.IsNullOrWhiteSpace(userArgs))
                return false;

            return k_FilesFlagRegex.IsMatch(userArgs);
        }

        /// <summary>
        /// Returns true when the user args contain file-filtering flags (--glob, --iglob,
        /// --type, or their short forms). Used to decide whether to add default exclude globs.
        /// </summary>
        internal static bool HasFileFilter(string userArgs)
        {
            if (string.IsNullOrWhiteSpace(userArgs))
                return false;

            return userArgs.Contains("--glob") || userArgs.Contains("--iglob") ||
                   userArgs.Contains("--type") ||
                   k_ShortFileFilterRegex.IsMatch(userArgs);
        }

        static string FormatSearchPaths(string[] searchPaths)
        {
            return string.Join(" ", searchPaths.Select(p => $"\"{EscapeQuotes(p)}\""));
        }

        static string EscapeQuotes(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            var escaped = value.Replace("\"", "\\\"");

            // Windows MSVC CRT argument parsing treats backslashes before a
            // closing double-quote as escape characters.  Double any trailing
            // backslashes so they are interpreted as literal characters and
            // don't swallow the closing quote (e.g. -e "pattern\\").
            int trailing = 0;
            for (var i = escaped.Length - 1; i >= 0 && escaped[i] == '\\'; i--)
                trailing++;

            if (trailing > 0)
                escaped += new string('\\', trailing);

            return escaped;
        }

        /// <summary>
        /// Strips the project root prefix from all absolute paths in ripgrep output
        /// so that paths appear relative.
        /// </summary>
        internal static string StripProjectRoot(string stdout, string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot) || string.IsNullOrEmpty(stdout))
                return stdout;

            return stdout.Replace(projectRoot + "/", "")
                         .Replace(projectRoot + "\\", "");
        }

        /// <summary>
        /// Truncates content-mode output to <paramref name="maxChars"/>,
        /// cutting at the last newline boundary so partial lines are not emitted.
        /// Also enforces a maximum line count via <see cref="DefaultMaxOutputLines"/>.
        /// </summary>
        internal static string TruncateContentOutput(string stdout, int maxChars)
        {
            if (string.IsNullOrEmpty(stdout))
                return stdout;

            var totalLines = CountLines(stdout);
            var totalChars = stdout.Length;

            // Apply line limit first, then char limit
            string result = stdout;
            bool truncated = false;

            if (totalLines > DefaultMaxOutputLines)
            {
                result = TruncateToMaxLines(result, DefaultMaxOutputLines, "");
                truncated = true;
            }

            if (result.Length > maxChars)
            {
                var truncateAt = result.LastIndexOf('\n', maxChars);
                result = truncateAt > 0
                    ? result.Substring(0, truncateAt)
                    : result.Substring(0, maxChars);
                truncated = true;
            }

            if (!truncated)
                return stdout;

            var shownLines = CountLines(result);
            return result +
                $"\n\n[Results truncated: showing {shownLines} of {totalLines} lines. " +
                "Your search is too broad. To reduce results: " +
                "use -l to list only file paths, " +
                "use a more specific pattern, " +
                "use --glob \"*.cs\" to filter by file type, " +
                "or use the path parameter to restrict to a directory.]";
        }

        static int CountLines(string text)
        {
            if (string.IsNullOrEmpty(text))
                return 0;
            int count = 1;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] == '\n')
                    count++;
            }
            return count;
        }

        /// <summary>
        /// Keeps only the first <paramref name="maxLines"/> lines from
        /// <paramref name="text"/>. If lines were dropped, appends
        /// <paramref name="truncationMessage"/>.
        /// </summary>
        internal static string TruncateToMaxLines(string text, int maxLines, string truncationMessage)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            int count = 0;
            int pos = 0;
            while (pos < text.Length && count < maxLines)
            {
                int nextNewline = text.IndexOf('\n', pos);
                if (nextNewline < 0)
                {
                    count++;
                    pos = text.Length;
                    break;
                }

                count++;
                pos = nextNewline + 1;
            }

            if (pos >= text.Length)
                return text.TrimEnd('\n', '\r');

            var truncatedText = text.Substring(0, pos).TrimEnd('\n', '\r');
            return truncatedText + "\n\n" + truncationMessage;
        }

        /// <summary>
        /// Yields every path-shaped or filesystem-existing token found in
        /// <paramref name="userArgs"/>, resolved against <paramref name="projectRoot"/>.
        /// FileTools.Grep feeds the result into the same permission system that
        /// already gates the explicit <c>path</c> parameter, so any path the
        /// agent embeds inside <c>args</c> goes through the same Read check —
        /// closing the bypass where ripgrep was happy to walk additional path
        /// positionals (e.g. <c>"pattern /etc/passwd"</c>) that the wrapper
        /// never asked the user about.
        ///
        /// A token is yielded if any of:
        ///   - It begins with <c>/</c>, <c>\</c>, <c>~</c>, or a drive letter (X:);
        ///   - It is or contains a parent-directory traversal (.., ../, ..\);
        ///   - The path it resolves to exists on disk.
        ///
        /// Values immediately following <c>-e</c> / <c>--regexp</c> (and
        /// <c>--regexp=VALUE</c>) are the search pattern, never paths, and are
        /// excluded so a literal-string regex like <c>-e "/var/log"</c> does
        /// not trigger a spurious external-read prompt.
        /// </summary>
        internal static IEnumerable<string> ExtractPathLikeTokens(string userArgs, string projectRoot)
        {
            if (string.IsNullOrWhiteSpace(userArgs) || string.IsNullOrEmpty(projectRoot))
                yield break;

            var tokens = TokenizeArgs(userArgs).ToList();
            for (int i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];

                if ((token == "-e" || token == "--regexp") && i + 1 < tokens.Count)
                {
                    i++;
                    continue;
                }

                string candidate;
                if (token.StartsWith("--") && token.Contains("="))
                {
                    var eqIdx = token.IndexOf('=');
                    var flag = token.Substring(0, eqIdx);
                    if (flag == "--regexp")
                        continue;
                    candidate = token.Substring(eqIdx + 1);
                    if (string.IsNullOrEmpty(candidate))
                        continue;
                }
                else if (token.StartsWith("-"))
                {
                    continue;
                }
                else
                {
                    candidate = token;
                }

                string fullPath;
                try
                {
                    var expanded = candidate.StartsWith("~")
                        ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) +
                          candidate.Substring(1)
                        : candidate;
                    fullPath = Path.IsPathRooted(expanded)
                        ? Path.GetFullPath(expanded)
                        : Path.GetFullPath(Path.Combine(projectRoot, expanded));
                }
                catch
                {
                    continue;
                }

                if (IsObviouslyPathShaped(candidate) ||
                    File.Exists(fullPath) ||
                    Directory.Exists(fullPath))
                {
                    yield return fullPath;
                }
            }
        }

        static bool IsObviouslyPathShaped(string token)
        {
            if (string.IsNullOrEmpty(token))
                return false;
            var c = token[0];
            if (c == '/' || c == '\\' || c == '~')
                return true;
            if (token.Length >= 2 && token[1] == ':' &&
                ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')))
                return true;
            if (token == ".." || token.StartsWith("../") || token.StartsWith("..\\"))
                return true;
            if (token.Contains("/../") || token.Contains("\\..\\"))
                return true;
            return false;
        }

        // Quote-aware whitespace tokenizer. Double-quoted strings group as a
        // single token; single quotes are literal. Sufficient for the rg argv
        // shapes the agent emits — does not handle backslash-escaped quotes.
        internal static IEnumerable<string> TokenizeArgs(string args)
        {
            if (string.IsNullOrEmpty(args))
                yield break;

            var sb = new StringBuilder();
            bool inQuotes = false;
            for (int i = 0; i < args.Length; i++)
            {
                char c = args[i];
                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (!inQuotes && char.IsWhiteSpace(c))
                {
                    if (sb.Length > 0)
                    {
                        yield return sb.ToString();
                        sb.Clear();
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            if (sb.Length > 0)
                yield return sb.ToString();
        }
    }
}
