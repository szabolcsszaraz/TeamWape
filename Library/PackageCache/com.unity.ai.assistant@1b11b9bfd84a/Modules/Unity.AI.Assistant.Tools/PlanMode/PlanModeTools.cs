using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.Editor;
using Unity.AI.Assistant.FunctionCalling;
using UnityEditor;
using UnityEngine;

namespace Unity.AI.Assistant.Tools.Editor
{
    /// <summary>
    /// Tools for Plan Mode: WriteTodos tracks progress during plan execution.
    /// </summary>
    class PlanModeTools
    {
        const string k_WritePlanFunctionId = "Unity.WritePlan";
        const string k_EditPlanFunctionId = "Unity.EditPlan";
        const string k_WriteTodosFunctionId = "Unity.WriteTodos";

        // Single source of truth for the plans directory location, relative to project root.
        // Referenced by tool parameter descriptions and ValidatePlansDirSecurity; also
        // consumed by ExitPlanModeTools (same assembly).
        internal const string PlansRelativePath = "Assets/Plans";

        // Stable prefix for success-result strings. Tests reference this constant instead
        // of hardcoding text. Failure cases throw InvalidOperationException (matching
        // CodeEditTool's pattern), so no error prefix is needed.
        internal const string SuccessPrefix = "Success";
        const string k_WriteTodosDescription = @"This tool can help you list out the current subtasks that are required to be completed for a given user request. The list of subtasks helps you keep track of the current task, organize complex queries and help ensure that you don't miss any steps. With this list, the user can also see the current progress you are making in executing a given task.

Depending on the task complexity, you should first divide a given task into subtasks and then use this tool to list out the subtasks that are required to be completed for a given user request.
Each of the subtasks should be clear and distinct.

Use this tool for complex queries that require multiple steps. If you find that the request is actually complex after you have started executing the user task, create a todo list and use it. If execution of the user task requires multiple steps, planning and generally is higher complexity than a simple Q&A, use this tool.

DO NOT use this tool for simple tasks that can be completed in less than 3 steps. If the user query is simple and straightforward, do not use the tool. If you can respond with an answer in a single turn then this tool is not required.

## Task state definitions

- pending: Work has not begun on a given subtask.
- in_progress: Marked just prior to beginning work on a given subtask. You should only have one subtask as in_progress at a time.
- completed: Subtask was successfully completed with no errors or issues. If the subtask required more steps to complete, update the todo list with the subtasks. All steps should be identified as completed only when they are completed.
- cancelled: As you update the todo list, some tasks are not required anymore due to the dynamic nature of the task. In this case, mark the subtasks as cancelled.


## Methodology for using this tool
1. Use this todo list only when executing an approved plan — map each plan step to a todo item.
2. Keep track of every subtask that you update the list with.
3. Mark a subtask as in_progress before you begin working on it. You should only have one subtask as in_progress at a time.
4. Update the subtask list as you proceed in executing the task. The subtask list is not static and should reflect your progress and current plans, which may evolve as you acquire new information.
5. Mark a subtask as completed when you have completed it.
6. Mark a subtask as cancelled if the subtask is no longer needed.
7. You must update the todo list as soon as you start, stop or cancel a subtask. Don't batch or wait to update the todo list.


## Examples of When to Use the Todo List

<example>
User request: Create a website with a React for creating fancy logos using gemini-2.5-flash-image

ToDo list created by the agent:
1. Initialize a new React project environment (e.g., using Vite).
2. Design and build the core UI components: a text input (prompt field) for the logo description, selection controls for style parameters (if the API supports them), and an image preview area.
3. Implement state management (e.g., React Context or Zustand) to manage the user's input prompt, the API loading status (pending, success, error), and the resulting image data.
4. Create an API service module within the React app (using ""fetch"" or ""axios"") to securely format and send the prompt data via an HTTP POST request to the specified ""gemini-2.5-flash-image"" (Gemini model) endpoint.
5. Implement asynchronous logic to handle the API call: show a loading indicator while the request is pending, retrieve the generated image (e.g., as a URL or base64 string) upon success, and display any errors.
6. Display the returned ""fancy logo"" from the API response in the preview area component.
7. Add functionality (e.g., a ""Download"" button) to allow the user to save the generated image file.
8. Deploy the application to a web server or hosting platform.

<reasoning>
The agent used the todo list to break the task into distinct, manageable steps:
1. Building an entire interactive web application from scratch is a highly complex, multi-stage process involving setup, UI development, logic integration, and deployment.
2. The agent inferred the core functionality required for a ""logo creator,"" such as UI controls for customization (Task 3) and an export feature (Task 7), which must be tracked as distinct goals.
3. The agent rightly inferred the requirement of an API service model for interacting with the image model endpoint.
</reasoning>
</example>


## Examples of When NOT to Use the Todo List

<example>
User request: Ensure that the test <test file> passes.

Agent:
<Goes into a loop of running the test, identifying errors, and updating the code until the test passes.>

<reasoning>
The agent did not use the todo list because this task could be completed by a tight loop of execute test->edit->execute test.
</reasoning>
</example>";
        
        /// <summary>
        /// Writes or updates the todo/progress list for tracking plan execution.
        /// </summary>
        [AgentTool(k_WriteTodosDescription,
            k_WriteTodosFunctionId)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        public static string WriteTodos(
            ToolExecutionContext context,
            [ToolParameter("Path to the approved implementation plan file that these todos are tracking (e.g. " + PlansRelativePath + "/feature-x.md). The plan file must already exist — this tool cannot be used without a pre-existing plan.")]
            string planPath,
            [ToolParameter("JSON array of todo objects. Each object must have 'description' (string: the task description) and 'status' (string: one of 'pending', 'in_progress', 'completed', or 'cancelled'). Send the complete list every time — this replaces the existing list.")]
            string todos)
        {
            var resolvedPath = ResolvePath(planPath);

            ValidatePlansDirSecurity(resolvedPath);

            if (!File.Exists(resolvedPath))
                throw new InvalidOperationException($"No plan file found at '{resolvedPath}'. WriteTodos can only be used when executing an approved implementation plan.");

            var todoItems = JsonConvert.DeserializeObject<List<TodoItem>>(todos);

            if (todoItems == null || todoItems.Count == 0)
                throw new InvalidOperationException("No todo items provided.");

            // Validate: only one in_progress
            var inProgressCount = todoItems.Count(t =>
                string.Equals(t.Status, "in_progress", StringComparison.OrdinalIgnoreCase));
            if (inProgressCount > 1)
                throw new InvalidOperationException("Only one task can be in_progress at a time.");

            // Fire event to update the todo list UI
            TodoUpdateEvent.Raise(todoItems, planPath, context.Conversation.ConversationId);

            // Return summary to LLM
            return FormatTodoSummary(todoItems);
        }

        static string FormatTodoSummary(List<TodoItem> todos)
        {
            if (todos.Count == 0)
                return $"{SuccessPrefix}: cleared the todo list.";

            var sb = new StringBuilder();
            sb.AppendLine($"{SuccessPrefix}: updated the todo list. The current list is now:");
            for (var i = 0; i < todos.Count; i++)
                sb.AppendLine($"{i + 1}. [{todos[i].Status}] {todos[i].Description}");

            return sb.ToString().TrimEnd();
        }

        [AgentTool(
            "Writes a complete plan file to the local filesystem. Use this ONLY for the INITIAL CREATION of a plan. " +
            "For ANY revision to an existing plan — small or large — use Unity.EditPlan instead, so reviewers " +
            "see a precise diff of what changed. Do not use Unity.WritePlan to overwrite an existing plan. " +
            "Creates parent directories if they don't exist. Do not use this tool to write code.",
            k_WritePlanFunctionId)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        public static async Task<string> WritePlan(
            ToolExecutionContext context,
            [ToolParameter("The path to the file to write to. Must be inside " + PlansRelativePath + "/ (e.g., \"" + PlansRelativePath + "/feature-x.md\").")]
            string filePath,
            [ToolParameter("The content to write to the file.")]
            string content)
        {
            var resolvedPath = ResolvePath(filePath);

            ValidatePlansDirSecurity(resolvedPath);

            // Creation-only: revisions must go through Unity.EditPlan so reviewers see a diff.
            // Without this guard, the agent could overwrite an approved plan with a fresh
            // WritePlan call and the user would see only an all-green permission card.
            if (File.Exists(resolvedPath))
                throw new InvalidOperationException($"Plan file already exists at '{resolvedPath}'. Use Unity.EditPlan to revise an existing plan.");

            // Single permission prompt for the whole "create new plan" operation. Parent
            // directory creation is silent (matches CodeEditTool's pattern); the user already
            // approved a Create on this path, which logically implies its containing dir.
            await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Create, resolvedPath);

            var directory = Path.GetDirectoryName(resolvedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(resolvedPath, content);

            AssetDatabase.Refresh();

            return $"{SuccessPrefix}: wrote to file {resolvedPath}";
        }

        [AgentTool(
            "Edits an existing plan file using precise string replacement. " +
            "Use this to revise a plan previously created with WritePlan. " +
            "oldString must match the file contents exactly (whitespace, indentation, newlines). " +
            "Include enough surrounding context (3+ lines) to uniquely identify the location. " +
            "For creating a new plan, use WritePlan instead. Do not use this tool to write code.",
            k_EditPlanFunctionId)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Plan,
            toolCallEnvironment: ToolCallEnvironment.EditMode,
            tags: FunctionCallingUtilities.k_PlanningTag)]
        public static async Task<string> EditPlan(
            ToolExecutionContext context,
            [ToolParameter("Path to the plan file to edit. Must be inside " + PlansRelativePath + "/ (e.g., \"" + PlansRelativePath + "/feature-x.md\").")]
            string filePath,
            [ToolParameter("Exact literal text to replace. Must include sufficient context (3+ lines) to uniquely identify the location and match whitespace precisely.")]
            string oldString,
            [ToolParameter("Replacement text. Becomes the new content at the matched location.")]
            string newString,
            [ToolParameter("Number of occurrences expected to be replaced. Defaults to 1.")]
            int expectedOccurrences = 1)
        {
            var resolvedPath = ResolvePath(filePath);

            ValidatePlansDirSecurity(resolvedPath);

            // Fail fast on the empty-oldString case before any FS access or permission prompt:
            // the user shouldn't see a permission dialog for an op that immediately errors.
            if (string.IsNullOrEmpty(oldString))
                throw new InvalidOperationException("oldString cannot be empty. Use Unity.WritePlan to create new files.");

            if (!File.Exists(resolvedPath))
                throw new InvalidOperationException($"Plan file not found at '{resolvedPath}'. Use Unity.WritePlan to create new plan files.");

            // Defensive size cap (mirrors CodeEditTool's check). Avoids reading a huge plan
            // into memory only to discover we can't process it; agent gets a clear error.
            if (FileUtils.ExceedsMaxReadSize(resolvedPath, out var sizeMB))
                throw new InvalidOperationException($"Plan file '{resolvedPath}' is too large to edit ({sizeMB:F1} MB). Only files under {AssistantConstants.MaxGetFileContentSizeMB} MB are supported.");

            await context.Permissions.CheckFileSystemAccess(PermissionItemOperation.Modify, resolvedPath);

            var originalContent = await File.ReadAllTextAsync(resolvedPath);

            // LLMs typically emit \n in JSON tool calls but plan files on Windows often have
            // \r\n line endings (git autocrlf, native editors). Without normalization, oldString
            // would never match and the agent would loop on an invisible difference. Reuse
            // CodeEditTool.NormalizeLineEndings (handles \r\n, lone \r, and \n) on all three
            // strings, match in normalized space, and restore the file's original line ending
            // on write. newString is null-coalesced so an explicit-empty replacement (delete)
            // is safe and a malformed null payload doesn't throw out of string.Replace.
            //
            // Note: line-ending restoration is whole-file (any LF gets converted to CRLF if the
            // file is CRLF-flavored), not per-block like CodeEditTool. Acceptable for markdown
            // plans where mixed line endings are rare and not semantically significant; differs
            // from CodeEditTool which preserves original casing per replacement.
            var fileLineEnding = originalContent.Contains("\r\n") ? "\r\n" : "\n";
            var normalizedContent = CodeEditTool.NormalizeLineEndings(originalContent);
            var normalizedOld = CodeEditTool.NormalizeLineEndings(oldString);
            var normalizedNew = CodeEditTool.NormalizeLineEndings(newString ?? string.Empty);

            var matchCount = 0;
            var idx = 0;
            while ((idx = normalizedContent.IndexOf(normalizedOld, idx, StringComparison.Ordinal)) != -1)
            {
                matchCount++;
                idx += normalizedOld.Length;
            }

            if (matchCount == 0)
                throw new InvalidOperationException($"oldString not found in '{resolvedPath}'.");

            if (matchCount != expectedOccurrences)
                throw new InvalidOperationException($"Expected {expectedOccurrences} occurrence(s) of oldString but found {matchCount} in '{resolvedPath}'. Provide more context to uniquely identify the target, or set expectedOccurrences to {matchCount}.");

            var modifiedContent = normalizedContent.Replace(normalizedOld, normalizedNew);

            if (fileLineEnding == "\r\n")
                modifiedContent = Regex.Replace(modifiedContent, @"(?<!\r)\n", "\r\n");

            await File.WriteAllTextAsync(resolvedPath, modifiedContent);

            AssetDatabase.Refresh();

            return $"{SuccessPrefix}: edited file {resolvedPath}";
        }

        internal static string ResolveExistingPlanPath(string filePath, string missingFileMessage)
        {
            var resolvedPath = ResolvePath(filePath);

            ValidatePlansDirSecurity(resolvedPath);

            if (!File.Exists(resolvedPath))
                throw new InvalidOperationException($"Plan file not found at '{filePath}'. {missingFileMessage}");

            return resolvedPath;
        }

        // Mirrors CodeEditTool.ValidatePathSecurity but tightens the scope from
        // project root to PlansRelativePath. Plan file operations may only target
        // paths inside that directory.
        internal static void ValidatePlansDirSecurity(string resolvedPath)
        {
            var projectRoot = Path.GetFullPath(Directory.GetCurrentDirectory());
            var plansDir = Path.GetFullPath(Path.Combine(projectRoot, PlansRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            var fullResolved = Path.GetFullPath(resolvedPath);

            if (!fullResolved.StartsWith(plansDir + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && !string.Equals(fullResolved, plansDir, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException(
                    $"Path '{resolvedPath}' resolves outside the {PlansRelativePath}/ directory. Plan file operations are restricted to {PlansRelativePath}/.");
            }
        }

        // Wraps both branches in Path.GetFullPath (matches CodeEditTool.ResolvePath) so every
        // downstream caller sees a canonicalized absolute path — File.Exists, Directory.GetDirectoryName,
        // and the security validator all operate on the same form, no risk of "/foo/./bar" vs "/foo/bar"
        // discrepancies.
        internal static string ResolvePath(string filePath)
        {
            if (Path.IsPathRooted(filePath))
                return Path.GetFullPath(filePath);

            var projectPath = Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectPath, filePath.Replace('/', Path.DirectorySeparatorChar)));
        }
    }
}
