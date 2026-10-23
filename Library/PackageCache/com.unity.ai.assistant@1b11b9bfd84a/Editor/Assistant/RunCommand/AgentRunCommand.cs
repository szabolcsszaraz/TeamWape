using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.Utils;
using UnityEngine;
using Object = UnityEngine.Object;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    class AgentRunCommand
    {
        IRunCommand m_ActionInstance;
        IReadonlyRunCommand m_ReadonlyActionInstance;
        private RunCommandMetadata m_Metadata;

        public string Script { get; set; }
        public CompilationErrors CompilationErrors { get; set; }

        public bool Unsafe => m_Metadata?.IsUnsafe ?? true;
        public bool HasWriteOperations => m_Metadata?.HasWriteOperations ?? false;

        public bool CompilationSuccess;

        /// <summary>
        /// Human-readable rejection reason set during the namespace authorization check,
        /// which runs after Roslyn compilation but before script execution. Distinct from
        /// <see cref="CompilationErrors"/>, which holds Roslyn diagnostics produced during compilation.
        /// </summary>
        public string UnauthorizedNamespaceError { get; set; }

        /// <summary>
        /// Guidance about deprecated APIs used by the script, produced by <see cref="ObsoleteApiAnalyzer"/>.
        /// Populated whether or not compilation succeeded: an error-severity deprecation fails the build,
        /// while a warning-severity one runs fine and would otherwise be reported nowhere.
        /// Empty when the script uses no deprecated API.
        /// Its line numbers count <see cref="Script"/> when compilation failed, matching
        /// <see cref="CompilationErrors"/>, and the script as submitted when it succeeded, since that is the
        /// only text the agent holds on the execution path.
        /// </summary>
        public string DeprecatedApiWarning { get; set; }

        /// <exclude />
        public CSharpCompilation Compilation { get; set; }

        public void Initialize(CSharpCompilation compilation)
        {
            Compilation = compilation;

            m_Metadata = RunCommandCodeAnalyzer.AnalyzeCommandAndExtractMetadata(Compilation);
        }

        public bool Execute(out ExecutionResult executionResult, string title)
        {
            executionResult = new ExecutionResult(title);

            if (m_ActionInstance == null)
                return false;

            executionResult.Start();

            try
            {
                m_ActionInstance.Execute(executionResult);

                // Unsafe actions usually mean deleting things - so we need to update the project view afterwards
                if (Unsafe)
                {
                    AssetDatabase.Refresh();
                }
            }
            catch (Exception e)
            {
                executionResult.LogError(e.ToString());
            }

            executionResult.End();

            return true;
        }

        public bool ExecuteReadonly(out ReadonlyExecutionResult executionResult, string title)
        {
            executionResult = new ReadonlyExecutionResult(title);

            if (m_ReadonlyActionInstance == null)
                return false;

            executionResult.Start();

            try
            {
                m_ReadonlyActionInstance.Execute(executionResult);
            }
            catch (Exception e)
            {
                executionResult.LogError(e.ToString());
            }
            finally
            {
                executionResult.End();
            }

            return true;
        }

        public bool HasUnauthorizedNamespaceUsage()
        {
            return RunCommandCodeAnalyzer.HasUnauthorizedNamespaceUsage(Script);
        }

        public void SetInstance(IRunCommand commandInstance)
        {
            m_ActionInstance = commandInstance;
        }

        public void SetReadonlyInstance(IReadonlyRunCommand commandInstance)
        {
            m_ReadonlyActionInstance = commandInstance;
        }
    }
}
