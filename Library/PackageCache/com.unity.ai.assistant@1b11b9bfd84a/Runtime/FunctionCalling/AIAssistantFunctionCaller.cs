using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Backend;
using Unity.AI.Assistant.Socket.Workflows.Chat;
using Unity.AI.Assistant.Utils;

namespace Unity.AI.Assistant.FunctionCalling
{
    class AIAssistantFunctionCaller : IFunctionCaller
    {
        public static JsonSerializer ParameterSerializer { get; } = new() { Converters = { new StringEnumConverter() } };

        IToolPermissions ToolPermissions { get; }
        IToolInteractions ToolInteractions { get; }

        public AIAssistantFunctionCaller(IToolPermissions toolPermissions, IToolInteractions toolInteractions)
        {
            ToolPermissions = toolPermissions;
            ToolInteractions = toolInteractions;
        }

        /// <inheritdoc />
        public void CallByLLM(IChatWorkflow workFlow, string functionId, JObject functionParameters, Guid callId, CancellationToken cancellationToken)
        {
            MainThread.DispatchAndForgetAsync(async () =>
            {
                var conversationContext = new ConversationContext(workFlow);
                var result = await CallFunction(conversationContext, functionId, callId, functionParameters, cancellationToken);
                workFlow.SendFunctionCallResponse(result, callId);
            });
        }

        async Task<FunctionCallResult> CallFunction(ConversationContext conversationContext, string functionId, Guid callId, JObject functionParameters, CancellationToken cancellationToken)
        {
            InternalLog.Log($"Calling tool {functionId} ({callId}) with parameters\n: {functionParameters}");
            try
            {
                // Build execution context
                var callInfo = new ToolExecutionContext.CallInfo(functionId, callId, functionParameters);
                var permission = new ToolCallPermissions(callInfo, ToolPermissions, cancellationToken);
                var interactions = new ToolCallInteractions(callInfo, ToolInteractions, cancellationToken);
                var context = new ToolExecutionContext(conversationContext, callInfo, permission, interactions);

                // Check that tool can be executed
                await context.Permissions.CheckCanExecute();

                var result = await ToolRegistry.FunctionToolbox.RunToolByIDAsync(context);
                var jsonResult = result != null ? AssistantJsonHelper.FromObject(result, ParameterSerializer) : JValue.CreateNull();
                InternalLog.Log($"Calling tool {functionId} successful:\n" + jsonResult);
                return FunctionCallResult.SuccessfulResult(jsonResult);
            }
            catch (Exception e)
            {
                InternalLog.LogWarning($"Calling tool {functionId} failed: {e.Message}\n{e.StackTrace}");
                return FunctionCallResult.FailedResult(GetExceptionErrorMessage(e));
            }
        }

        // Internal for tests: tool wrappers rely on this preferring InnerException.Message when
        // deciding whether to attach an inner exception (see SkillsTools.LoadResourceContent).
        internal static string GetExceptionErrorMessage(Exception e)
        {
            return e.InnerException?.Message ?? e.Message;
        }
    }
}
