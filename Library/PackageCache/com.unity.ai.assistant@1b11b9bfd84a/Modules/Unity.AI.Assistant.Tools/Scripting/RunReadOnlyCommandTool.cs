using System;
using System.Linq;
using System.Threading.Tasks;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor.Backend.Socket.Tools;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class RunReadOnlyCommandTool
    {
        internal const string k_FunctionId = "Unity.RunReadOnlyCommand";
        internal const string k_CodeRequiredMessage = "Code parameter cannot be empty.";
        internal const string k_CommandBuildFailedMessage = "Failed to build agent command.";
        internal const string k_WriteOperationsDetectedMessage = "This command contains write operations which are not allowed in read-only mode. Detected operations that create, modify, or delete objects/assets. Rewrite the script to only read and inspect data.";
        internal const string k_UnsafeOperationsDetectedMessage = "This command contains unsafe operations which are not allowed in read-only mode. Rewrite the script to only read and inspect data.";
        internal const string k_CommandExecutionFailedMessage = "Execution failed:\n{0}";

        [AgentTool(
            "Execute a read-only C# script in the Unity environment. The script will be compiled and executed in read-only mode, returning the results. Write operations are rejected.",
            k_FunctionId)]
        [AgentToolSettings(assistantMode: AssistantMode.Ask | AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_CodeExecutionTag)]
        public static async Task<RunCommandTool.ExecutionOutput> ExecuteReadOnlyCommand(
            ToolExecutionContext context,
            [ToolParameter("The C# script code to execute. Should implement IReadonlyRunCommand interface.")]
            string code,
            [ToolParameter("Title for the execution command")]
            string title)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException(k_CodeRequiredMessage);

            var agentCommand = RunCommandUtils.BuildRunCommand(code);
            if (agentCommand == null || !agentCommand.CompilationSuccess)
                throw new InvalidOperationException(
                    RunCommandUtils.WithDeprecationGuidance(k_CommandBuildFailedMessage, agentCommand));

            // The rejections below all send the agent back to rewrite the script, so any deprecation it also
            // tripped over travels with them rather than waiting for the next attempt to surface it.
            if (agentCommand.HasWriteOperations)
                throw new InvalidOperationException(
                    RunCommandUtils.WithDeprecationGuidance(k_WriteOperationsDetectedMessage, agentCommand));

            if (agentCommand.Unsafe)
                throw new InvalidOperationException(
                    RunCommandUtils.WithDeprecationGuidance(k_UnsafeOperationsDetectedMessage, agentCommand));

            await context.Permissions.CheckCodeExecution(code);

            var executionResult = RunCommandUtils.ExecuteReadonly(agentCommand, title);
            var formattedLogs = FormatLogs(executionResult);

            if (!executionResult.SuccessfullyStarted)
            {
                var logs = string.IsNullOrEmpty(formattedLogs) ? "No logs available" : formattedLogs;
                throw new InvalidOperationException(RunCommandUtils.WithDeprecationGuidance(
                    string.Format(k_CommandExecutionFailedMessage, logs), agentCommand));
            }

            var hasErrors = executionResult.Logs != null && executionResult.Logs.Any(log =>
                log.LogType == LogType.Error ||
                log.LogType == LogType.Exception);

            if (hasErrors)
            {
                var logs = string.IsNullOrEmpty(formattedLogs) ? "No logs available" : formattedLogs;
                throw new InvalidOperationException(RunCommandUtils.WithDeprecationGuidance(
                    string.Format(k_CommandExecutionFailedMessage, logs), agentCommand));
            }

            return new RunCommandTool.ExecutionOutput
            {
                IsExecutionSuccessful = true,
                ExecutionId = executionResult.Id,
                ExecutionLogs = RunCommandUtils.WithDeprecationGuidance(formattedLogs, agentCommand)
            };
        }

        static string FormatLogs(ReadonlyExecutionResult executionResult)
        {
            if (executionResult?.Logs == null || executionResult.Logs.Count == 0)
                return string.Empty;

            return string.Join("\n", executionResult.GetFormattedLogs());
        }
    }
}
