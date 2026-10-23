using System.Threading;
using System.Threading.Tasks;
using Unity.AI.Assistant.Agents;
using Unity.AI.Assistant.Data;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.AI.Assistant.Editor.Api
{
    public static partial class AssistantApi
    {
        /// <summary>
        /// Run the Assistant with its UI.
        /// </summary>
        /// <param name="userPrompt">The user's input prompt or question.</param>
        /// <param name="attachedContext">Additional context information to include with the query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task</returns>
        public static async Task Run(string userPrompt, AttachedContext attachedContext = null, CancellationToken cancellationToken = default)
        {
            await RunInternal(userPrompt, attachedContext, AssistantMode.Agent, null, cancellationToken);
        }

        /// <summary>
        /// Show a prompt popup, then run the assistant with the provided prompt.
        /// </summary>
        /// <param name="parentRect">The rect from which the position of the popup will be determined (will appear below).</param>
        /// <param name="placeholderPrompt">The default prompt when opening the popup.</param>
        /// <param name="attachedContext">Attached context for the prompt.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task</returns>
        public static async Task PromptThenRun(Rect parentRect, string placeholderPrompt = "", AttachedContext attachedContext = null, CancellationToken cancellationToken = default)
        {
            await PromptThenRunInternal(parentRect, placeholderPrompt, attachedContext, AssistantMode.Agent, null, cancellationToken);
        }

        /// <summary>
        /// Show a prompt popup, then run the assistant with the provided prompt.
        /// </summary>
        /// <param name="parent">The visual element on which to show the popup (will appear below).</param>
        /// <param name="placeholderPrompt">The default prompt when opening the popup.</param>
        /// <param name="attachedContext">Attached context for the prompt.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task</returns>
        public static async Task PromptThenRun(VisualElement parent, string placeholderPrompt = "", AttachedContext attachedContext = null, CancellationToken cancellationToken = default)
        {
            await PromptThenRunInternal(parent, placeholderPrompt, attachedContext, AssistantMode.Agent, null, cancellationToken);
        }
        
        /// <summary>
        /// Run an agent headless, without the Assistant UI.
        /// </summary>
        /// <param name="agent">The agent to execute for processing the query. If no agent is passed, will use the default behavior.</param>
        /// <param name="userPrompt">The user's input prompt or question.</param>
        /// <param name="attachedContext">Additional context information to include with the query.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the asynchronous operation.</param>
        /// <returns>A task with the final answer</returns>
        public static async Task<string> RunHeadless(this IAgent agent, string userPrompt, AttachedContext attachedContext = null, CancellationToken cancellationToken = default)
        {
            var output = await RunHeadlessInternal(userPrompt, attachedContext, agent, null, null, cancellationToken);
            var lastBlock = output.Message.Blocks[^1] as AnswerBlock;
            return lastBlock?.Content;
        }
    }
}
