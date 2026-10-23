using System;
using System.IO;
using UnityEditor;
using UnityEditor.TestRunner.CommandLineParser;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Unity.PerformanceTesting.Editor
{
    [InitializeOnLoad]
    class TestRunnerInitializer
    {
        static TestRunnerInitializer()
        {
            var args = GetCmdLineArguments();
            var resultsHandler = args.IsCmdLineRun && !string.IsNullOrEmpty(args.PerfTestResults)
                ? CreateCmdLineResultsHandler(args)
                : CreateDefaultResultsHandler(args);

            var api = ScriptableObject.CreateInstance<TestRunnerApi>();
            api.RegisterCallbacks(resultsHandler);
        }

        static ICallbacks CreateCmdLineResultsHandler(CmdLineArguments cmdLineArguments)
        {
            var callbacks = ScriptableObject.CreateInstance<CmdLineResultsSavingCallbacks>();
            callbacks.SetResultsLocation(cmdLineArguments.PerfTestResults);
            return callbacks;
        }

        static ICallbacks CreateDefaultResultsHandler(CmdLineArguments cmdLineArguments)
        {
            var callbacks = ScriptableObject.CreateInstance<PerformanceTestRunSaver>();
            callbacks.SetTestResultsDirectory(GetDirectoryOrNull(cmdLineArguments.TestResults));
            return callbacks;
        }

        static string GetDirectoryOrNull(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            try
            {
                return Path.GetDirectoryName(Path.GetFullPath(filePath));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Could not determine the test results directory from '{filePath}'.\n{e}");
                return null;
            }
        }

        static CmdLineArguments GetCmdLineArguments()
        {
            var isCmdLineTestRun = false;
            string resultFilePath = null;
            string testResultsFilePath = null;

            var optionSet = new CommandLineOptionSet(
                new CommandLineOption("runTests", () => { isCmdLineTestRun = true; }),
                new CommandLineOption("runEditorTests", () => { isCmdLineTestRun = true; }),
                new CommandLineOption("perfTestResults", filePath => { resultFilePath = filePath; }),
                new CommandLineOption("testResults", filePath => { testResultsFilePath = filePath; })
            );
            optionSet.Parse(Environment.GetCommandLineArgs());

            return new CmdLineArguments(isCmdLineTestRun, resultFilePath, testResultsFilePath);
        }

        class CmdLineArguments
        {
            public bool IsCmdLineRun;
            public string PerfTestResults;
            public string TestResults;

            public CmdLineArguments(bool isCmdLineRun, string perfTestResults, string testResults)
            {
                IsCmdLineRun = isCmdLineRun;
                PerfTestResults = perfTestResults;
                TestResults = testResults;
            }
        }
    }
}
