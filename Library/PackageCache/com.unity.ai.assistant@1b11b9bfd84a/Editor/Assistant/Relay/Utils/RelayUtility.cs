using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Unity.AI.Assistant.Editor.Utils
{
    class RelayUtility
    {
        /// <summary>
        /// Returns the expected file name of the zip file required by mac systems. If a valid system architecture is
        /// not detected, returns an empty string
        /// </summary>
        public static bool TryGetMacZipFileName(out string path)
        {
            string arch = RuntimeInformation.OSArchitecture switch
            {
                Architecture.Arm64 => "arm64",
                Architecture.X64 => "x64",
                _ => string.Empty
            };

            if (arch == string.Empty)
            {
                path = string.Empty;
                return false;
            }

            path = $"relay_mac_{arch}";
            return true;
        }

        /// <summary>
        /// Given a source directory, zip file name and a target directory, this function extracts the given zip file
        /// from the path {sourceDir}/{sourceZipFileName} to the {targetDir} using ditto, which preserves macOS
        /// extended attributes, permissions and symlinks within the app bundle.
        /// </summary>
        public static void UnzipAndSetMacAppPermissions(string sourceDir, string sourceZipFileName, string targetDir)
        {
            var zipPath = Path.Combine(sourceDir, sourceZipFileName);
            var startInfo = new ProcessStartInfo
            {
                FileName = "ditto",
                Arguments = $"-xk \"{zipPath}\" \"{targetDir}\"",
                UseShellExecute = false,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            string errorOutput = process?.StandardError.ReadToEnd();
            process?.WaitForExit();

            if (process == null || process.ExitCode != 0)
                throw new Exception($"Failed to extract macOS relay app bundle via ditto: {errorOutput}");
        }
    }
}
