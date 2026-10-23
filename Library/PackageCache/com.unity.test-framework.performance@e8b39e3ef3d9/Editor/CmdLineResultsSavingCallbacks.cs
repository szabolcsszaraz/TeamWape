using System;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace Unity.PerformanceTesting.Editor
{
    [Serializable]
    class CmdLineResultsSavingCallbacks : ScriptableObject, ICallbacks
    {
        [SerializeField]
        string resultsLocation;

        IncrementalRunResultsWriter m_IncrementalWriter;

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
                var performanceTestRun = TestResultsParser.GetPerformanceTestRunData(result);
                if (performanceTestRun == null)
                {
                    return;
                }

                Debug.LogFormat(LogType.Log, LogOption.NoStacktrace, null, "Saving performance results to: {0}", resultsLocation);
                IncrementalRunResultsWriter.WriteResultsFile(resultsLocation, performanceTestRun);
            }
            catch (Exception e)
            {
                Debug.LogError("Saving performance results file failed.");
                Debug.LogException(e);
            }
        }

        void ICallbacks.TestStarted(ITestAdaptor test) { }

        void ICallbacks.TestFinished(ITestResultAdaptor result)
        {
            IncrementalWriter.AppendTestResults(resultsLocation, result.Output);
        }

        public void SetResultsLocation(string perfTestResultsPath)
        {
            resultsLocation = perfTestResultsPath;
        }
    }
}
