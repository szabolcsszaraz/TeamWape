using System;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Unity.AI.MCP.Editor.Helpers;
using Unity.AI.MCP.Editor.ToolRegistry;
using Unity.AI.MCP.Editor.Tools.Parameters;
using Unity.AI.Assistant.Tools.Editor;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.MCP.Editor.Tools
{
    /// <summary>
    /// Handles compilation and execution of C# scripts in the Unity environment.
    /// Combines validation and execution into a single operation by delegating to
    /// RunCommandValidatorTool and RunCommandTool.
    /// </summary>
    public static class RunCommand
    {
        public const string Title = "Compile and execute a C# script";

        /// <summary>
        /// Human-readable description of the Unity.RunCommand tool functionality and usage.
        /// </summary>
        public const string Description =
            @"Compile and execute a C# script in the Unity Editor.

This tool first validates that the code can be compiled, then executes it if compilation succeeds.
Args: code (required), title (optional).
Returns: compilation status, execution status, logs, and results.

This is a powerful tool that allows you to programmatically control virtually every aspect of the game, including physics, input, graphics, gameplay logic, project setting and package management.

### The Golden Template
```csharp
using UnityEngine;
using UnityEditor;

internal class CommandScript : IRunCommand
{
    public void Execute(ExecutionResult result)
    {
        // 1. Your logic here
        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);

        // 2. Register changes for Undo/Redo and tracking
        result.RegisterObjectCreation(cube);

        // 3. Log the result
        result.Log(""Created {0}"", cube);
    }
}
```
### Rules for Success
1. **Class Name is Mandatory**: The class MUST be named `CommandScript`. Using any other name will cause a NullReferenceException or execution failure.
2. **Use `internal` Accessibility**: Always use `internal class CommandScript`. Using `public` will cause an ""Inconsistent Accessibility"" compilation error.
3. **Use the `result` Object**:
   - **Creation**: Use `result.RegisterObjectCreation(obj)` after creating objects.
   - **Modification**: Use `result.RegisterObjectModification(obj)` BEFORE changing properties.
   - **Deletion**: Use `result.DestroyObject(obj)` instead of `Object.DestroyImmediate`.
   - **Logging**:
     - `result.Log(""Created {0}"", obj)` - Log with object references using `{0}`, `{1}`, etc.
     - `result.LogWarning(""Warning message"")` - Log warnings
     - `result.LogError(""Error message"")` - Log errors
4. **Avoid Top-Level Statements**: Always wrap your code in the class structure above.

";

        /// <summary>
        /// Returns the output schema for this tool.
        /// </summary>
        /// <returns>The output schema object defining the structure of successful responses.</returns>
        [McpOutputSchema("Unity.RunCommand")]
        public static object GetOutputSchema()
        {
            return new
            {
                type = "object",
                properties = new
                {
                    success = new {type = "boolean", description = "Whether the operation succeeded"},
                    message = new {type = "string", description = "Human-readable message about the operation"},
                    data = new
                    {
                        type = "object",
                        description = "Execution result data",
                        properties = new
                        {
                            isCompilationSuccessful = new {type = "boolean", description = "Whether the code compiled successfully"},
                            isExecutionSuccessful = new {type = "boolean", description = "Whether the code executed successfully"},
                            executionId = new {type = "integer", description = "ID of the execution"},
                            compilationLogs = new {type = "string", description = "Logs from the compilation process"},
                            executionLogs = new {type = "string", description = "Logs from the execution process"},
                            localFixedCode = new {type = "string", description = "Code with local fixes applied (if any)"},
                            result = new {type = "string", description = "Human-readable result message"}
                        }
                    }
                },
                required = new[] {"success", "message"}
            };
        }

        /// <summary>
        /// The MCP tool name used to identify this tool in the registry.
        /// </summary>
        public const string ToolName = "Unity.RunCommand";

        /// <summary>
        /// Main handler for script compilation and execution.
        /// </summary>
        /// <param name="parameters">Parameters containing the script code and optional title.</param>
        /// <returns>A response object indicating success or failure with compilation and execution details.</returns>
        [McpTool(ToolName, Description, Title, Groups = new string[] {"core", "scripting"}, EnabledByDefault = true)]
        public static async Task<object> HandleCommand(RunCommandParams parameters)
        {
            string code = parameters?.Code;
            if (string.IsNullOrWhiteSpace(code))
            {
                return Response.Error("CODE_REQUIRED: code parameter cannot be empty.");
            }

            try
            {
                // Ensure any externally-written scripts are imported and compiled,
                // then wait for the editor to become idle before Roslyn compilation.
                // If domain reload happens, the MCP server retries automatically.
                await EditorReadyHelper.RefreshAndWaitForReady();

                // Step 1: Validate the code using RunCommandValidatorTool
                var validationResult = RunCommandValidatorTool.RunCommandValidator(code);

                if (!validationResult.IsCompilationSuccessful)
                {
                    return Response.Error(
                        "COMPILATION_FAILED: Code failed to compile.",
                        new
                        {
                            isCompilationSuccessful = false,
                            isExecutionSuccessful = false,
                            compilationLogs = validationResult.CompilationLogs,
                            localFixedCode = validationResult.LocalFixedCode
                        });
                }

                // Step 2: Execute the command using RunCommandTool
                // Create a ToolExecutionContext for MCP calls using the factory
                var toolParams = new JObject
                {
                    ["code"] = code,
                    ["title"] = parameters?.Title ?? string.Empty
                };
                var context = ToolExecutionContextFactory.CreateForExternalCall(
                    RunCommandTool.k_FunctionId,
                    toolParams);

                var executionResult = await RunCommandTool.ExecuteCommand(
                    context,
                    code,
                    parameters?.Title);

                // Return combined result
                var resultMessage = executionResult.IsExecutionSuccessful
                    ? "Command executed successfully."
                    : "Command execution failed.";

                return Response.Success(
                    resultMessage,
                    new
                    {
                        isCompilationSuccessful = true,
                        isExecutionSuccessful = executionResult.IsExecutionSuccessful,
                        executionId = executionResult.ExecutionId,
                        compilationLogs = validationResult.CompilationLogs,
                        executionLogs = executionResult.ExecutionLogs,
                        localFixedCode = validationResult.LocalFixedCode,
                        result = resultMessage
                    });
            }
            catch (TimeoutException)
            {
                return Response.Error(
                    "COMPILATION_IN_PROGRESS: Unity is still compiling scripts after waiting. Retry after a few seconds.",
                    new {isCompiling = true});
            }
            catch (Exception e)
            {
                return Response.Error($"UNEXPECTED_ERROR: {e.Message}");
            }
        }
    }
}
