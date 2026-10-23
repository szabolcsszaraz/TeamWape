using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Editor.Utils;
using Unity.AI.Assistant.Utils;
using Unity.AI.MCP.Editor.Settings;
using Unity.AI.MCP.Editor.Settings.Utilities;
using Unity.AI.Tracing;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.MCP.Editor.Helpers
{
    /// <summary>
    /// Copies the relay binary from the package's RelayApp~ directory to ~/.unity/relay/
    /// so that MCP clients can reference a stable, well-known executable location.
    /// Runs automatically once per editor session and only updates when the bundled
    /// version is newer than the installed binary (detected via relay --version).
    /// </summary>
    [InitializeOnLoad]
    static class ServerInstaller
    {
        const string k_SessionStateKey = "ServerInstaller.CheckedThisSession";
        const string k_FallbackVersion = "0.0.0";

        static ServerInstaller()
        {
            if (SessionState.GetBool(k_SessionStateKey, false))
                return;
            InstallOrUpdateRelay();
            SessionState.SetBool(k_SessionStateKey, true);
        }

        internal static void InstallOrUpdateRelay()
        {
            try
            {
                string sourceDir = Path.GetFullPath(MCPConstants.relayAppPath);
                if (!Directory.Exists(sourceDir))
                {
                    McpLog.Warning($"Relay app directory not found at {sourceDir}");
                    return;
                }

                string targetDir = MCPConstants.RelayBaseDirectory;
                string bundledVersion = ReadBundledVersion(Path.Combine(sourceDir, "relay.json"));
                string installedVersion = ReadInstalledVersion();

                if (!IsNewerVersion(bundledVersion, installedVersion))
                {
                    McpLog.Log($"Relay is up to date (bundled: {bundledVersion}, installed: {installedVersion})");
                    return;
                }

                if (!Directory.Exists(targetDir))
                    Directory.CreateDirectory(targetDir);

                CopyRelayFiles(sourceDir, targetDir);

                McpLog.Log($"Relay installed to {targetDir} (version {bundledVersion})");
            }
            catch (Exception ex)
            {
                McpLog.Warning($"Could not install relay: {ex.Message}");
            }
        }

        static string ReadBundledVersion(string relayJsonPath)
        {
            try
            {
                if (!File.Exists(relayJsonPath))
                    return k_FallbackVersion;

                string json = File.ReadAllText(relayJsonPath);
                var jsonObj = JObject.Parse(json);
                return jsonObj["version"]?.ToString() ?? k_FallbackVersion;
            }
            catch
            {
                return k_FallbackVersion;
            }
        }

        static string ReadInstalledVersion()
        {
            try
            {
                string binaryPath = MCPConstants.InstalledServerMainFile;
                if (!File.Exists(binaryPath))
                    return k_FallbackVersion;

                var result = ProcessUtils.Execute(binaryPath, "--version", timeoutMs: 5000);
                if (!result.Success || string.IsNullOrEmpty(result.Output))
                    return k_FallbackVersion;

                return ParseVersionFromOutput(result.Output);
            }
            catch
            {
                return k_FallbackVersion;
            }
        }

        static string ParseVersionFromOutput(string output)
        {
            const string prefix = "Version: ";
            foreach (string line in output.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return trimmed.Substring(prefix.Length).Trim();
            }
            return k_FallbackVersion;
        }

        static bool IsNewerVersion(string packageVersion, string installedVersion)
        {
            try
            {
                var pkgBase = new Version(CleanVersion(packageVersion));
                var instBase = new Version(CleanVersion(installedVersion));

                int cmp = pkgBase.CompareTo(instBase);
                if (cmp != 0)
                    return cmp > 0;

                // Base versions equal — compare build numbers from pre-release tag
                return ExtractBuildNumber(packageVersion) > ExtractBuildNumber(installedVersion);
            }
            catch
            {
                return true;
            }
        }

        static int ExtractBuildNumber(string version)
        {
            // Parse "X.Y.Z-build.N" → N, or 0 if no tag
            int dashIndex = version.IndexOf('-');
            if (dashIndex < 0) return 0;

            string tag = version.Substring(dashIndex + 1);
            int lastDot = tag.LastIndexOf('.');
            if (lastDot >= 0 && int.TryParse(tag.Substring(lastDot + 1), out int n))
                return n;

            return 0;
        }

        static string CleanVersion(string version)
        {
            int dashIndex = version.IndexOf('-');
            return dashIndex >= 0 ? version.Substring(0, dashIndex) : version;
        }

        static void CopyRelayFiles(string sourceDir, string targetDir)
        {
            // Clean up .old files from previous rename-on-locked-binary operations
            CleanupOldFiles(targetDir);

            // Windows
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                CopyToTargetDir(Path.Combine(sourceDir, "relay_win.exe"));

            // Linux
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                CopyToTargetDir(Path.Combine(sourceDir, "relay_linux"));
                SetExecutable(Path.Combine(targetDir, "relay_linux"));
            }

            // Mac
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (!RelayUtility.TryGetMacZipFileName(out string zipName))
                {
                    Trace.Warn("Failed to get a relay zip name for the current platform. This should never happen.");
                    return;
                }

                try
                {
                    RelayUtility.UnzipAndSetMacAppPermissions(sourceDir, zipName, targetDir);
                }
                catch (Exception e)
                {
                    Trace.Exception(e);
                }
            }

            void CopyToTargetDir(string path)
            {
                if (!File.Exists(path))
                {
                    Trace.Warn($"Failed to copy file {path} to targetDir {targetDir} because original file does not exist");
                    return;
                }

                string fileName = Path.GetFileName(path);
                string targetPath = Path.Combine(targetDir, fileName);

                try
                {
                    File.Copy(path, targetPath, true);
                }
                catch (IOException) when (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    // Binary is likely locked by a running relay process.
                    // Windows allows renaming a running exe, so rename it out of the way and copy the new one.
                    try
                    {
                        string oldPath = targetPath + ".old";
                        if (File.Exists(oldPath))
                            File.Delete(oldPath);
                        File.Move(targetPath, oldPath);
                        File.Copy(path, targetPath, true);
                    }
                    catch (IOException)
                    {
                        string processName = Path.GetFileNameWithoutExtension(fileName);
                        var pids = System.Diagnostics.Process.GetProcessesByName(processName).Select(p => p.Id);
                        McpLog.Warning($"Cannot update relay binary at {targetPath}: file is locked by process(es) [{string.Join(", ", pids)}]. Will retry next editor session.");
                        throw;
                    }
                }
            }
        }

        static void CleanupOldFiles(string targetDir)
        {
            try
            {
                foreach (string oldFile in Directory.GetFiles(targetDir, "*.old"))
                {
                    try { File.Delete(oldFile); }
                    catch { /* Still in use, ignore */ }
                }
            }
            catch
            {
                // Target directory may not exist yet
            }
        }

        static void CopyFile(string sourcePath, string targetDir, string fileName)
        {
            string targetPath = Path.Combine(targetDir, fileName);
            File.Copy(sourcePath, targetPath, true);
        }

        static void SetExecutable(string filePath)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "chmod",
                    Arguments = $"+x \"{filePath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var process = System.Diagnostics.Process.Start(startInfo);
                process?.WaitForExit(5000);
            }
            catch
            {
                // chmod not available on this platform
            }
        }
    }
}
