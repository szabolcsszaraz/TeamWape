using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Unity.AI.Assistant.Agent.Dynamic.Extension.Editor;
using Unity.AI.Assistant.Editor.CodeAnalyze;
using Unity.AI.Assistant.Utils;
using UnityEditor.Macros;

namespace Unity.AI.Assistant.Editor.RunCommand
{
    [UnityEditor.InitializeOnLoad]
    static class RunCommandUtils
    {
        const string k_DynamicAssemblyName = "Unity.AI.Assistant.Bridge.Editor";
        const string k_DynamicCommandNamespace = "Unity.AI.Assistant.Agent.Dynamic.Extension.Editor";
        const string k_DynamicCommandClassName = "CommandScript";
        const string k_DynamicAssemblyDirectory = "Library/AssistantRunCommand";
        const string k_DynamicAssemblyFilePrefix = "Unity.AI.Assistant.RunCommand.Dynamic";
        const string k_MacroEvaluatorEntryPointClassName = "RunCommandMacroEvaluatorEntryPoint";

        const string k_DummyCommandScript =
            "\nusing UnityEngine;\nusing UnityEditor;\n\ninternal class CommandScript : IRunCommand\n{\n    public void Execute(ExecutionResult result) {}\n}";
        
        internal static DynamicAssemblyBuilder Builder => m_Builder;
        static readonly DynamicAssemblyBuilder m_Builder = new(k_DynamicAssemblyName, k_DynamicCommandNamespace);

        
        static RunCommandUtils()
        {
            var dynamicAssemblyDirectory = Path.GetFullPath(k_DynamicAssemblyDirectory);
            Directory.CreateDirectory(dynamicAssemblyDirectory);

            foreach (var dllPath in Directory.GetFiles(dynamicAssemblyDirectory, "*.dll"))
                SafeDelete(dllPath);

            // Warm up call
            Task.Run(() => m_Builder.Compile(k_DummyCommandScript, out _));
        }

        internal static AgentRunCommand BuildRunCommand(string commandScript)
        {
            var compilationSuccessful =
                m_Builder.TryCompileCode(commandScript, out var compilationLogs, out var compilation);
            
            var updatedScript = compilation.GetSourceCode();
            var runCommand =
                new AgentRunCommand()
                {
                    CompilationErrors = compilationLogs,
                    Script = updatedScript,
                    DeprecatedApiWarning = AnalyzeDeprecatedApis(commandScript, compilation, compilationSuccessful)
                };

            var unauthorizedNamespaceError = RunCommandCodeAnalyzer.GetUnauthorizedNamespaceError(updatedScript);
            if (unauthorizedNamespaceError != null)
            {
                runCommand.CompilationSuccess = false;
                runCommand.UnauthorizedNamespaceError = unauthorizedNamespaceError;
            }
            else if (compilationSuccessful)
            {
                runCommand.CompilationSuccess = true;
                runCommand.Initialize(compilation);
            }
            else
            {
                InternalLog.LogWarning($"Unable to compile the command:\n{compilationLogs}");
            }

            return runCommand;
        }

        /// <summary>
        /// Produces the deprecated-API guidance for a command, with line numbers that count the text the agent
        /// will be reading it against.
        /// </summary>
        /// <remarks>
        /// The compiled tree is not the submitted script: the builder moves it into
        /// <see cref="k_DynamicCommandNamespace"/>, normalizes its whitespace and may apply fixes, all of which
        /// shift lines. Which text the agent holds depends on the outcome. When compilation failed, the guidance
        /// travels next to <see cref="AgentRunCommand.CompilationErrors"/>, whose lines count that compiled
        /// tree, so the guidance counts it too and the two agree. When compilation succeeded, the agent gets
        /// execution logs and nothing else, and the only source it has is the one it submitted, so the analysis
        /// runs over a verbatim compilation of that text instead. The cost is one extra parse and semantic model
        /// per executed command, and a symbol that only bound thanks to an auto-applied fix, such as an added
        /// using, goes unreported on that path.
        /// </remarks>
        static string AnalyzeDeprecatedApis(string commandScript, CSharpCompilation compilation, bool compilationSuccessful)
        {
            return compilationSuccessful
                ? ObsoleteApiAnalyzer.Analyze(m_Builder.CompileVerbatim(commandScript))
                : ObsoleteApiAnalyzer.Analyze(compilation);
        }

        /// <summary>
        /// Appends the command's deprecated-API guidance to a message bound for the agent, if there is any.
        /// Used on both the failure and the success path so that a deprecation is reported once, in the same
        /// turn it occurred, whatever its severity.
        /// </summary>
        internal static string WithDeprecationGuidance(string message, AgentRunCommand command)
        {
            return WithDeprecationGuidance(message, command?.DeprecatedApiWarning);
        }

        /// <inheritdoc cref="WithDeprecationGuidance(string, AgentRunCommand)"/>
        internal static string WithDeprecationGuidance(string message, string deprecationWarning)
        {
            if (string.IsNullOrEmpty(deprecationWarning))
                return message;

            // The message may or may not end in a newline -- CompilationErrors.ToString() does, the joined
            // execution logs do not -- so the separator is applied to the trimmed message either way, to keep
            // exactly one blank line ahead of the guidance.
            var trimmedMessage = message?.TrimEnd();

            return string.IsNullOrEmpty(trimmedMessage)
                ? deprecationWarning
                : $"{trimmedMessage}\n\n{deprecationWarning}";
        }

        internal static ExecutionResult Execute(AgentRunCommand command, string title = "")
        {
            var commandInstance = CreateCommandScriptInstance(command) as IRunCommand;
            command.SetInstance(commandInstance);

            command.Execute(out var executionResult, title);

            return executionResult;
        }

        internal static ReadonlyExecutionResult ExecuteReadonly(AgentRunCommand command, string title = "")
        {
            var commandInstance = CreateCommandScriptInstance(command) as IReadonlyRunCommand;
            command.SetReadonlyInstance(commandInstance);

            command.ExecuteReadonly(out var executionResult, title);

            return executionResult;
        }


        static object CreateCommandScriptInstance(AgentRunCommand command)
        {
            // Compile a temporary assembly and write it to disk. This assembly contains 2 things. 1) The generated code
            // in the original generated assembly. 2) The macro evaluator entry point code.
            var commandKey = Guid.NewGuid().ToString("N");
            var dynamicAssemblyName = $"{k_DynamicAssemblyFilePrefix}.{commandKey}";
            var compilation = command.Compilation
                .WithAssemblyName(dynamicAssemblyName)
                .AddSyntaxTrees(CSharpSyntaxTree.ParseText(GetMacroEvaluatorEntryPointSource()));

            var dynamicAssemblyDirectory = Path.GetFullPath(k_DynamicAssemblyDirectory);
            Directory.CreateDirectory(dynamicAssemblyDirectory);

            var dynamicAssemblyPath = Path.Combine(dynamicAssemblyDirectory, dynamicAssemblyName + ".dll");
            using (var stream = File.Create(dynamicAssemblyPath))
            {
                var result = compilation.Emit(stream);
                if (!result.Success)
                    return null;
            }

            // Invoke the MacroEvaluator create function, which loads the assembly and posts a CommandScript object.
            // This is sync, so InvokeMacroEvaluator will complete before moving on
            InvokeCreateViaMacroEvaluator(dynamicAssemblyPath, dynamicAssemblyName, commandKey);
            SafeDelete(dynamicAssemblyPath);

            // There should be a commandScript posted. This can now be returned to user.
            return CommandScriptPostBox.Pull(commandKey);
        }

        static void SafeDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // Ignore, dlls get locked up in certain platforms. We try to delete every initialization until the lock
                // is removed. On Windows a loaded managed assembly throws UnauthorizedAccessException (not IOException),
                // so both must be swallowed here.
            }
        }

        static string GetMacroEvaluatorEntryPointSource()
        {
            return $@"
namespace {k_DynamicCommandNamespace}
{{
    public static class {k_MacroEvaluatorEntryPointClassName}
    {{
        public static void Create(string key)
        {{
            CommandScriptPostBox.Post(key, new {k_DynamicCommandClassName}());
        }}
    }}
}}";
        }

        static void InvokeCreateViaMacroEvaluator(
            string assemblyPath,
            string assemblyName,
            string key)
        {
            var typeName = $"{k_DynamicCommandNamespace}.{k_MacroEvaluatorEntryPointClassName}, {assemblyName}";
            var parcel = MacroEvaluatorParcelUtility.CreateParcel(
                assemblyPath,
                typeName,
                "Create",
                new[] { typeof(string) },
                new object[] { key });

            try
            {
                MacroEvaluator.Eval(parcel);
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException ?? e;
            }
        }

    }
}
