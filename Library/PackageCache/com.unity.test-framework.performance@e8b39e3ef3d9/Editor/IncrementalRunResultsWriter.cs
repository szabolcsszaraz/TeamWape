using System;
using System.IO;
using Unity.PerformanceTesting.Data;
using UnityEditor;
using UnityEngine;

namespace Unity.PerformanceTesting.Editor
{
    // Keeps a complete performance results file on disk while a run is still in progress, so that
    // results already collected survive a crash, a test timeout or any other premature shutdown.
    class IncrementalRunResultsWriter
    {
        // The results file is replaced by moving this file over it. A shutdown between removing the
        // previous file and the move landing therefore leaves the complete results here and nothing
        // at the results path, so whoever recovers the results of a run that went down has to look
        // here when the results file itself is missing.
        internal const string TempFileExtension = ".tmp";

        const string k_RunTokenKey = "Unity.PerformanceTesting.IncrementalResults.RunToken";
        const string k_WrittenRunTokenKey = "Unity.PerformanceTesting.IncrementalResults.WrittenRunToken";

        Run m_Run;
        bool m_RunStateInitialized;
        bool m_WriteFailureLogged;

        internal void BeginRun()
        {
            m_Run = null;
            m_RunStateInitialized = true;
            m_WriteFailureLogged = false;

            // The results file of an earlier run is left in place until this run has something to
            // replace it with, and is identified as not belonging to this run by the token.
            SessionState.SetString(k_RunTokenKey, Guid.NewGuid().ToString("N"));
            SessionState.EraseString(k_WrittenRunTokenKey);
        }

        internal void AppendTestResults(string resultsPath, string testOutput)
        {
            if (string.IsNullOrEmpty(resultsPath) || string.IsNullOrEmpty(testOutput))
                return;

            try
            {
                RestoreRunStateFromDisk(resultsPath);

                if (!TestResultsParser.TryAppendPerformanceRunData(testOutput, ref m_Run))
                    return;

                WriteResultsFile(resultsPath, m_Run);
                SessionState.SetString(k_WrittenRunTokenKey, SessionState.GetString(k_RunTokenKey, string.Empty));
            }
            catch (Exception e)
            {
                // Only reported once because the run is still able to write its results when it finishes.
                if (m_WriteFailureLogged)
                    return;

                m_WriteFailureLogged = true;
                Debug.LogWarning($"Failed to save in-progress performance results to '{resultsPath}'.\n{e}");
            }
        }

        internal static void WriteResultsFile(string resultsPath, Run run)
        {
            var json = JsonUtility.ToJson(run, true);
            CreateDirectoryIfNecessary(resultsPath);

            // Staged through a temporary file so that a shutdown part way through the write cannot
            // leave a truncated, unparsable results file behind. File.Replace is not dependable on
            // every file system the results are written to, so the previous file is removed first.
            // The temporary file is complete by the time the removal happens, which is what lets a
            // reader treat it as the results of the run when the results file is not there.
            var tempPath = GetTempFilePath(resultsPath);
            File.WriteAllText(tempPath, json);

            if (File.Exists(resultsPath))
            {
                File.Delete(resultsPath);
            }

            File.Move(tempPath, resultsPath);
        }

        internal static string GetTempFilePath(string resultsPath)
        {
            return resultsPath + TempFileExtension;
        }

        // A domain reload discards the accumulated run, so continue from the file on disk rather
        // than replacing it with only the results gathered since the reload.
        void RestoreRunStateFromDisk(string resultsPath)
        {
            if (m_RunStateInitialized)
                return;

            m_RunStateInitialized = true;

            if (!WrittenResultsBelongToCurrentRun())
                return;

            // A replacement that never landed leaves the results of this run in the temporary file
            // alone, so that file stands in for the results file when it is the only one present.
            var pathToRestoreFrom = File.Exists(resultsPath) ? resultsPath : GetTempFilePath(resultsPath);
            if (!File.Exists(pathToRestoreFrom))
                return;

            try
            {
                m_Run = JsonUtility.FromJson<Run>(File.ReadAllText(pathToRestoreFrom));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to read the in-progress performance results file '{pathToRestoreFrom}'.\n{e}");
            }
        }

        static bool WrittenResultsBelongToCurrentRun()
        {
            var runToken = SessionState.GetString(k_RunTokenKey, string.Empty);
            return !string.IsNullOrEmpty(runToken)
                && runToken == SessionState.GetString(k_WrittenRunTokenKey, string.Empty);
        }

        static void CreateDirectoryIfNecessary(string filePath)
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }
        }
    }
}
