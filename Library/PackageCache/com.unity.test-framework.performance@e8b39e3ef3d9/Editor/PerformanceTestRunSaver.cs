using System;
using System.IO;
using Unity.PerformanceTesting.Runtime;
using UnityEngine;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEditor.TestTools.TestRunner.CommandLineTest;

namespace Unity.PerformanceTesting.Editor
{
    [Serializable]
    internal class PerformanceTestRunSaver : ScriptableObject, ICallbacks
    {
        internal const string ResultsFileName = "PerformanceTestResults.json";

        // Directory of the NUnit results file requested with -testResults. Results are saved there
        // as well, because that is the directory a command line run collects as its artifacts.
        [SerializeField]
        string testResultsDirectory;

        IncrementalRunResultsWriter m_IncrementalWriter;

        internal static string DefaultResultsPath
        {
            get { return Path.Combine(Application.persistentDataPath, ResultsFileName); }
        }

        internal string ResultsPath
        {
            get
            {
                return string.IsNullOrEmpty(testResultsDirectory)
                    ? DefaultResultsPath
                    : Path.Combine(testResultsDirectory, ResultsFileName);
            }
        }

        IncrementalRunResultsWriter IncrementalWriter
        {
            get
            {
                if (m_IncrementalWriter == null)
                {
                    m_IncrementalWriter = new IncrementalRunResultsWriter();
                }

                return m_IncrementalWriter;
            }
        }

        internal void SetTestResultsDirectory(string directoryPath)
        {
            testResultsDirectory = directoryPath;
        }

        void ICallbacks.RunStarted(ITestAdaptor testsToRun)
        {
            PerformanceTest.Active = null;
            IncrementalWriter.BeginRun();
        }

        void ICallbacks.RunFinished(ITestResultAdaptor result)
        {
            PlayerCallbacks.Saved = false;

            try
            {
                var resultWriter = new ResultsWriter();
                var xmlPath = Path.Combine(Application.persistentDataPath, "TestResults.xml");
                resultWriter.WriteResultToFile(result, xmlPath);
                var xmlParser = new TestResultXmlParser();
                var run = xmlParser.GetPerformanceTestRunFromXml(xmlPath);
                if (run == null) return;

                var resultsPath = ResultsPath;
                IncrementalRunResultsWriter.WriteResultsFile(resultsPath, run);
                if (resultsPath != DefaultResultsPath)
                {
                    IncrementalRunResultsWriter.WriteResultsFile(DefaultResultsPath, run);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.InnerException);
            }
        }

        void ICallbacks.TestStarted(ITestAdaptor test) { }

        void ICallbacks.TestFinished(ITestResultAdaptor result)
        {
            IncrementalWriter.AppendTestResults(ResultsPath, result.Output);
        }
    }
}
