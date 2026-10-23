using System;
using Newtonsoft.Json;
using Unity.AI.Assistant.Editor.RunCommand;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.FunctionCalling;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    static class RunCommandValidatorTool
    {
        internal const string k_FunctionId = "Unity.RunCommandValidator";
        internal const string k_CodeRequiredMessage = "Code parameter cannot be empty.";

        [Serializable]
        public struct CompileOutput
        {
            [JsonProperty("isCompilationSuccessful")] // must match Python code
            public bool IsCompilationSuccessful;

            [JsonProperty("compilationLogs")] // must match Python code
            public string CompilationLogs;

            [JsonProperty("localFixedCode")] // must match Python code
            public string LocalFixedCode;
        }

        [AgentTool("Validate that a run command is valid. Can be compiled and executed.",
            k_FunctionId)]
        [AgentToolSettings(toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_CodeCorrectionTag)]
        public static CompileOutput RunCommandValidator([ToolParameter("The code to attempt to compile")] string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException(k_CodeRequiredMessage);

            var compilationSuccessful = RunCommandUtils.Builder.TryCompileCode(code, out var compilationErrors, out var compilation);
            // A deprecation severe enough to fail compilation is explained here, next to the error it caused.
            // Warning-severity deprecations are deliberately left out: the code compiles and runs, so they are
            // reported from the execution path instead, and appending them here would put text into
            // CompilationLogs for code that compiled cleanly. Asking the analyzer for error severity is what
            // enforces that -- gating on overall compilation success instead would let an unrelated compile
            // error elsewhere in the script drag warning-severity deprecations in here.
            var compilationLogs = RunCommandUtils.WithDeprecationGuidance(
                compilationErrors.ToString(),
                ObsoleteApiAnalyzer.Analyze(compilation, errorsOnly: true));

            return new CompileOutput
            {
                IsCompilationSuccessful = compilationSuccessful,
                CompilationLogs = compilationLogs,
                LocalFixedCode = compilation.GetSourceCode()
            };
        }
    }
}
