using System;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.Editor.Checkpoint.Git
{
    static class GitInstanceResolver
    {
#if UNITY_EDITOR_WIN
        const string k_GitExecutable = "git.exe";
        const string k_GitLFSExecutable = "git-lfs.exe";
#elif UNITY_EDITOR_OSX
        const string k_GitExecutable = "git";
        const string k_GitLFSExecutable = "git-lfs";

        // Common paths where git-lfs may be installed on macOS (e.g. via Homebrew).
        // These are prepended to PATH so git and git-lfs can be found when running as a subprocess.
        static readonly string[] k_MacOSExtraPaths = { "/opt/homebrew/bin", "/usr/local/bin" };
#else
        const string k_GitExecutable = "git";
        const string k_GitLFSExecutable = "git-lfs";
#endif

        const int k_ValidationTimeoutMs = 10000;

        public static string ResolvePath(GitInstanceConfig config)
        {
            return config.Type switch
            {
                GitInstanceType.System => GetSystemGitPath(),
                GitInstanceType.Custom => config.CustomPath ?? string.Empty,
                _ => GetSystemGitPath()
            };
        }

        /// <summary>
        /// Finds the first valid git instance type, checking in order: System, Custom.
        /// Returns null if no valid instance is found.
        /// </summary>
        public static GitInstanceType? FindFirstValidInstance(string customPath = null)
        {
            if (ValidateGitInstance(GetSystemGitPath()).IsValid)
                return GitInstanceType.System;

            if (!string.IsNullOrEmpty(customPath) && ValidateGitInstance(customPath).IsValid)
                return GitInstanceType.Custom;

            return null;
        }

        public static string GetSystemGitPath()
        {
            return k_GitExecutable;
        }

        public static (bool found, string version, string path) DetectSystemGit()
        {
            try
            {
                var result = RunGitCommand(k_GitExecutable, "--version");
                if (result.success)
                {
                    var version = ParseGitVersion(result.output);
                    return (true, version, k_GitExecutable);
                }
            }
            catch (Exception ex)
            {
                InternalLog.LogWarning($"System git detection failed: {ex.Message}");
            }

            return (false, null, null);
        }

        public static GitValidationResult ValidateGitInstance(string gitPath)
        {
            if (string.IsNullOrEmpty(gitPath))
            {
                return GitValidationResult.GitNotFound(gitPath);
            }

            if (!gitPath.Equals(k_GitExecutable, StringComparison.OrdinalIgnoreCase))
            {
                if (!File.Exists(gitPath))
                {
                    return GitValidationResult.GitNotFound(gitPath);
                }
            }

            var gitResult = RunGitCommand(gitPath, "--version");
            if (!gitResult.success)
            {
                if (!string.IsNullOrEmpty(gitResult.error) && gitResult.error.IndexOf("Cannot find the specified file", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return GitValidationResult.GitNotFound(gitPath);
                }
                
                return GitValidationResult.Error(gitPath, $"Git execution failed: {gitResult.error}");
            }

            var gitVersion = ParseGitVersion(gitResult.output);
            if (string.IsNullOrEmpty(gitVersion))
            {
                return GitValidationResult.Error(gitPath, "Could not parse Git version");
            }
            
            var lfsResultDirect = RunGitCommand(k_GitLFSExecutable, "version");
            var lfsResultParam = RunGitCommand(gitPath, "lfs version");
            if (!lfsResultDirect.success && !lfsResultParam.success)
            {
                return GitValidationResult.LfsMissing(gitPath, gitVersion);
            }

            var lfsOutput = lfsResultDirect.success ? lfsResultDirect.output : lfsResultParam.output;
            var lfsVersion = ParseLfsVersion(lfsOutput);
            if (string.IsNullOrEmpty(lfsVersion))
            {
                return GitValidationResult.LfsMissing(gitPath, gitVersion);
            }

            return GitValidationResult.Valid(gitPath, gitVersion, lfsVersion);
        }

        public static GitValidationResult ValidateConfig(GitInstanceConfig config)
        {
            var path = ResolvePath(config);
            return ValidateGitInstance(path);
        }

        static (bool success, string output, string error) RunGitCommand(string gitPath, string arguments)
        {
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = gitPath,
                    Arguments = arguments,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                ConfigureEnvironment(startInfo);

                using var process = Process.Start(startInfo);
                if (process == null)
                    return (false, null, "Failed to start process");

                var output = process.StandardOutput.ReadToEnd();
                var error = process.StandardError.ReadToEnd();

                if (!process.WaitForExit(k_ValidationTimeoutMs))
                {
                    try { process.Kill(); }
                    catch { }
                    return (false, null, "Command timed out");
                }

                return (process.ExitCode == 0, output.Trim(), error.Trim());
            }
            catch (Exception ex)
            {
                return (false, null, ex.Message);
            }
        }

        static void ConfigureEnvironment(ProcessStartInfo startInfo)
        {
#if UNITY_EDITOR_OSX
            // On macOS, Unity-spawned processes don't inherit the shell's PATH, which may include
            // directories like /opt/homebrew/bin where git-lfs is installed via Homebrew. Without
            // this, LFS detection fails even when git-lfs is correctly installed. We prepend the
            // common installation paths so both git and its git-lfs subcommand can be found.
            var currentPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            var extraPaths = string.Join(":", k_MacOSExtraPaths);
            startInfo.Environment["PATH"] = $"{extraPaths}:{currentPath}";
#endif
        }

        static string ParseGitVersion(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            var match = Regex.Match(output, @"git version (\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }

        static string ParseLfsVersion(string output)
        {
            if (string.IsNullOrEmpty(output))
            {
                return null;
            }

            var match = Regex.Match(output, @"git-lfs/(\d+\.\d+\.\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
    }
}
