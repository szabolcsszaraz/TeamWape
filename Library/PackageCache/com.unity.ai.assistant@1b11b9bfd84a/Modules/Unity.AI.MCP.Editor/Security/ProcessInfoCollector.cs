using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Unity.AI.MCP.Editor.Models;
using UnityEngine;

namespace Unity.AI.MCP.Editor.Security
{
    /// <summary>
    /// Collects process information including executable identity and parent process chain.
    /// </summary>
    static class ProcessInfoCollector
    {
        #if UNITY_EDITOR_OSX
        // Mac-specific: Use libproc to get executable path and working directory
        [DllImport("/usr/lib/libproc.dylib", SetLastError = true)]
        static extern int proc_pidpath(int pid, StringBuilder buffer, uint buffersize);

        [DllImport("/usr/lib/libproc.dylib", SetLastError = true)]
        static extern int proc_pidinfo(int pid, int flavor, ulong arg, byte[] buffer, int buffersize);

        const int PROC_PIDVNODEPATHINFO = 9;
        const int PROC_PIDVNODEPATHINFO_SIZE = 2352;
        const int VNODE_INFO_SIZE = 152; // offset to path within vnode_info_path
        #endif

        #if UNITY_EDITOR_WIN
        const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
        const int ERROR_INSUFFICIENT_BUFFER = 122;

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern bool QueryFullProcessImageName(IntPtr hProcess, int flags, StringBuilder exeName, ref int size);
        #endif

        /// <summary>
        /// Collect process information for a given PID
        /// </summary>
        public static ProcessInfo CollectProcessInfo(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                using (process)
                {
                    DateTime startTime = process.StartTime;
                    string executablePath = GetExecutablePath(pid);

                    if (string.IsNullOrEmpty(executablePath))
                    {
                        return new ProcessInfo
                        {
                            ProcessId = pid,
                            ProcessName = process.ProcessName ?? "unknown",
                            StartTime = startTime,
                            WorkingDirectory = GetWorkingDirectory(pid),
                            Identity = null
                        };
                    }

                    return new ProcessInfo
                    {
                        ProcessId = pid,
                        ProcessName = GetProcessDisplayName(executablePath, process.ProcessName),
                        StartTime = startTime,
                        WorkingDirectory = GetWorkingDirectory(pid),
                        Identity = ExecutableIdentityCollector.CollectIdentity(executablePath)
                    };
                }
            }
            catch (Exception ex)
            {
                if (!IsProcessRunning(pid))
                {
                    // Process exited during collection — expected race condition
                    return new ProcessInfo
                    {
                        ProcessId = pid,
                        ProcessName = "unknown",
                        StartTime = DateTime.MinValue,
                        Identity = null
                    };
                }

                UnityEngine.Debug.LogWarning($"Failed to collect process info for PID {pid}: {ex.Message}");
                return new ProcessInfo
                {
                    ProcessId = pid,
                    ProcessName = "unknown",
                    StartTime = DateTime.MinValue,
                    Identity = null
                };
            }
        }

        /// <summary>
        /// Collect complete connection information including server and client processes
        /// </summary>
        public static ConnectionInfo CollectConnectionInfo(int serverPid, ValidationConfig config)
        {
            var connectionInfo = new ConnectionInfo
            {
                ConnectionId = Guid.NewGuid().ToString(),
                Timestamp = DateTime.UtcNow,
                Server = CollectProcessInfo(serverPid)
            };

            // Collect parent (client) information if enabled
            if (config.CollectParentInfo)
            {
                var (parentPid, parentPath, chainDepth) = FindMcpClient(serverPid, config.MaxParentChainDepth);
                if (parentPid.HasValue && parentPid.Value > 1)
                {
                    connectionInfo.Client = CollectProcessInfo(parentPid.Value);
                    connectionInfo.ClientChainDepth = chainDepth;
                }
            }

            return connectionInfo;
        }

        /// <summary>
        /// Get the executable filename as the initial display name.
        /// The proper name arrives later via <c>set_client_info</c> MCP command.
        /// </summary>
        static string GetProcessDisplayName(string executablePath, string osProcessName)
        {
            return Path.GetFileName(executablePath);
        }

        /// <summary>
        /// Get executable path for a process ID
        /// </summary>
        static string GetExecutablePath(int pid)
        {
            try
            {
                Process process = Process.GetProcessById(pid);
                using (process)
                {
                    #if UNITY_EDITOR_OSX
                    // On Mac, use proc_pidpath for more reliable path retrieval
                    var sb = new StringBuilder(4096);
                    int ret = proc_pidpath(pid, sb, (uint)sb.Capacity);
                    if (ret > 0)
                    {
                        return sb.ToString();
                    }
                    // Fallback to Process.MainModule
                    return GetMainModulePathSafe(process);
                    #elif UNITY_EDITOR_WIN
                    // On Windows, use QueryFullProcessImageName to avoid MainModule
                    // enumerating a foreign process's module list (which can throw for
                    // processes mid-startup/exit or with a bitness/privilege mismatch).
                    IntPtr handle = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
                    if (handle != IntPtr.Zero)
                    {
                        try
                        {
                            // Extended-length Windows paths can reach 32767 chars; start
                            // small and grow once if the buffer was too small.
                            foreach (int capacity in new[] { 1024, 32767 })
                            {
                                var sb = new StringBuilder(capacity);
                                int len = sb.Capacity;
                                if (QueryFullProcessImageName(handle, 0, sb, ref len))
                                    return sb.ToString();
                                if (Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
                                    break;
                            }
                        }
                        finally
                        {
                            CloseHandle(handle);
                        }
                    }
                    // Fallback to Process.MainModule
                    return GetMainModulePathSafe(process);
                    #else
                    return GetMainModulePathSafe(process);
                    #endif
                }
            }
            catch (Exception ex)
            {
                if (!IsProcessRunning(pid))
                    return null; // Process exited during collection — expected race condition

                UnityEngine.Debug.LogWarning($"Failed to get executable path for PID {pid}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the process's main-module file path, or null if it can't be read.
        /// </summary>
        /// <remarks>
        /// The <see cref="Process.MainModule"/> getter throws when a foreign process's
        /// module list can't be enumerated; this swallows that and returns null.
        /// </remarks>
        static string GetMainModulePathSafe(Process process)
        {
            try
            {
                return process.MainModule?.FileName;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Get the current working directory of a process by PID.
        /// Uses platform-specific APIs: proc_pidinfo on macOS, /proc on Linux.
        /// </summary>
        static string GetWorkingDirectory(int pid)
        {
            try
            {
                #if UNITY_EDITOR_OSX
                var buffer = new byte[PROC_PIDVNODEPATHINFO_SIZE];
                int ret = proc_pidinfo(pid, PROC_PIDVNODEPATHINFO, 0, buffer, buffer.Length);
                if (ret <= 0) return null;

                // CWD path starts at offset VNODE_INFO_SIZE (152) within pvi_cdir
                int nullIndex = Array.IndexOf<byte>(buffer, 0, VNODE_INFO_SIZE, 1024);
                if (nullIndex < 0) nullIndex = VNODE_INFO_SIZE + 1024;
                if (nullIndex <= VNODE_INFO_SIZE) return null;
                return Encoding.UTF8.GetString(buffer, VNODE_INFO_SIZE, nullIndex - VNODE_INFO_SIZE);
                #elif UNITY_EDITOR_LINUX
                // /proc/<pid>/cwd is a symlink to the process's working directory
                byte[] buf = new byte[4096];
                IntPtr n = readlink($"/proc/{pid}/cwd", buf, (IntPtr)buf.Length);
                long len = n.ToInt64();
                if (len > 0 && len < buf.Length)
                    return Encoding.UTF8.GetString(buf, 0, (int)len);
                return null;
                #else
                return null;
                #endif
            }
            catch (Exception)
            {
                return null;
            }
        }

        #if UNITY_EDITOR_LINUX
        [DllImport("libc", SetLastError = true)]
        static extern IntPtr readlink(string pathname, byte[] buf, IntPtr bufsiz);
        #endif

        static bool IsProcessRunning(int pid)
        {
            try
            {
                using var p = Process.GetProcessById(pid);
                return !p.HasExited;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Walk up the process tree to find the actual MCP client, skipping intermediate shells
        /// </summary>
        static (int? parentPid, string parentPath, int depth) FindMcpClient(int serverPid, int maxDepth)
        {
            int currentPid = serverPid;

            for (int depth = 0; depth < maxDepth; depth++)
            {
                if (!ParentProcessHelper.TryGetParentInfo(currentPid, out int ppid, out string ppath))
                {
                    // Could not get parent info
                    return (null, null, depth);
                }

                if (ppid <= 1)
                {
                    // Reached init/launchd (PID 1) - parent probably exited
                    return (null, null, depth);
                }

                // Check if this is a shell or launcher (keep walking up if so)
                string processName = Path.GetFileNameWithoutExtension(ppath ?? "").ToLowerInvariant();

                bool isShell = processName.Contains("sh") ||      // sh, bash, zsh, dash, etc.
                               processName.Contains("cmd") ||      // cmd.exe
                               processName.Contains("powershell") || // powershell.exe
                               processName.Contains("pwsh") ||     // pwsh (PowerShell Core)
                               processName.Contains("conhost");    // conhost.exe

                // Note: We intentionally do NOT include "terminal" in the shell check because
                // WindowsTerminal.exe, Terminal.app, etc. are the actual MCP clients, not shells to skip

                // The relay is Unity's own transport binary and must never be reported AS the
                // client. On some platforms the Bun single-file executable re-execs itself,
                // producing a relay -> relay parent chain; without skipping it the walk would
                // stop at the relay ("relay app") instead of the real external client that
                // spawned it, destabilising the connection identity (UUM-142530).
                bool isRelay = IsRelayProcess(ppath);

                if ((!isShell && !isRelay) || ppath == "unknown")
                {
                    // Found a real MCP client (not a shell or the relay itself)
                    return (ppid, ppath, depth + 1);
                }

                // Keep walking up the chain (skipping shells and relay processes)
                currentPid = ppid;
            }

            // Reached max depth without finding a non-shell parent
            return (null, null, maxDepth);
        }

        /// <summary>
        /// Exact set of Unity's relay binary filenames (without extension).
        /// Used an exact, case-insensitive match — NOT a "relay_" prefix — because a
        /// prefix wildcard would let any process named e.g. relay_malicious.exe pass
        /// <see cref="IsRelayProcess"/> and be skipped during the trust walk, letting a
        /// malicious intermediary hide itself and impersonate the real client
        /// (UUM-142530 security review).
        /// </summary>
        static readonly HashSet<string> k_RelayFileNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "relay_win", "relay_linux", "relay_mac_arm64", "relay_mac_x64"
        };

        /// <summary>
        /// True if the executable is one of Unity's known relay binaries
        /// (relay_win / relay_linux / relay_mac_arm64 / relay_mac_x64), compared as an
        /// exact, case-insensitive filename match. The full path is intentionally ignored
        /// because the Bun single-file executable can re-exec from a temp directory while
        /// keeping its original filename.
        /// </summary>
        internal static bool IsRelayProcess(string exePath)
        {
            if (string.IsNullOrEmpty(exePath) || exePath == "unknown")
                return false;

            return k_RelayFileNames.Contains(Path.GetFileNameWithoutExtension(exePath));
        }
    }
}
