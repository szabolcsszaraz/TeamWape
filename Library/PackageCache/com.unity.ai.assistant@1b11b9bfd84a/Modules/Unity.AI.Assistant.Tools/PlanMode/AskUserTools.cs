using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unity.AI.Assistant.Data;
using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Tools.Editor
{
    class AskUserTools
    {
        const string k_ToolName = "Unity.AskUser";

        [AgentTool(
            "Ask the user one or more questions to gather preferences, clarify requirements, or make decisions.\n\n" +
            "The 'questions' parameter is a JSON array of question objects (1-8 questions per call). " +
            "Each question object has:\n" +
            "- 'question' (string, required): The complete question text, ending with '?'.\n" +
            "- 'type' (string, optional): 'choice' (default) for multiple-choice with options, " +
            "'text' for free-form input, 'yesno' for Yes/No confirmation." +
            "- 'options' (array, required for 'choice'): 2-4 option objects, each with 'label' (1-5 words) " +
            "and 'description' (brief explanation). An 'Other' input is automatically added.\n" +
            "- 'multiSelect' (bool, optional): Only for 'choice' type. Set to true to allow selecting multiple options.\n" +
            "- 'placeholder' (string, optional): Hint text for input fields. " +
            "For type='text', shown in the main input. For type='choice', shown in the 'Other' custom input.\n\n" +
            "Return value is a JSON object with:\n" +
            "- 'answers' (object): Maps question index (0-based) to the user's answer string. " +
            "Only contains entries for questions the user actually answered.\n" +
            "- 'skipped' (array): List of question indices (0-based) the user explicitly skipped.\n" +
            "- 'notes' (string or null): Optional free-form text the user typed in the notes field " +
            "for additional context or details. Null if the user left it empty.",
            k_ToolName)]
        [AgentToolSettings(
            assistantMode: AssistantMode.Agent | AssistantMode.Plan,
            tags: FunctionCallingUtilities.k_SmartContextTag)]
        internal static async Task<string> AskUser(
            ToolExecutionContext context,
            [ToolParameter("JSON array of question objects. Each must have 'question'. " +
                       "For 'choice' type, include 'options' array with 'label' and 'description' per option.")]
            string questions)
        {
            List<AskUserQuestion> parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<List<AskUserQuestion>>(questions);
            }
            catch (Exception e)
            {
                throw new ArgumentException($"Invalid questions JSON: {e.Message}", e);
            }

            if (parsed == null || parsed.Count == 0)
                throw new ArgumentException("At least one question is required.");

            if (parsed.Count > 8)
                throw new ArgumentException("Maximum 8 questions per call.");

            for (var i = 0; i < parsed.Count; i++)
            {
                var q = parsed[i];

                if (string.IsNullOrWhiteSpace(q.Question))
                    throw new ArgumentException($"Question {i + 1}: 'question' is required.");

                var type = string.IsNullOrEmpty(q.Type) ? "choice" : q.Type.ToLowerInvariant();

                if (type == "choice" && (q.Options == null || q.Options.Count < 2))
                    throw new ArgumentException($"Question {i + 1}: type 'choice' requires 'options' array with 2-4 items.");

                if (type == "choice" && q.Options.Count > 4)
                    throw new ArgumentException($"Question {i + 1}: 'options' array must have at most 4 items.");
            }

            var interaction = new AskUserInteraction(parsed);

            return await context.Interactions.WaitForUser(interaction);
        }
    }
}
