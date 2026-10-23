using System;
using System.Threading.Tasks;
using Unity.AI.Assistant.Editor.Mcp.Manager;
using Unity.AI.Assistant.Editor.Service;
using Unity.AI.Assistant.Utils;
using UnityEditor;

namespace Unity.AI.Assistant.Editor.Mcp
{
    /// <summary>
    /// Handles MCP initialization when Unity starts
    /// </summary>
    static class McpInitializer
    {
        const int k_MaxRetrySeconds = 300; // 5 minutes
        static int m_RetrySeconds = 2;
        
        [InitializeOnLoadMethod]
        static async Task InitializeMcpServices()
        {
            await AssistantGlobal.Services.RegisterService(new McpServerManagerService());
            var handle = AssistantGlobal.Services.GetService<McpServerManagerService>();

            while (handle.State != ServiceState.RegisteredAndInitialized)
            {
                InternalLog.LogError("The MCP server service failed to initialize. This means that the relay server " +
                                     "failed to initialize or the initialization signalling logic is not working " +
                                     "correctly. A reinitialization is being attempted now. Retrying in " +
                                     $"{m_RetrySeconds} seconds.");

                // Wait before performing a retry
                await Task.Delay(m_RetrySeconds * 1000);
                
                // Double the retry length if there is another failure, capped at 5 minutes
                m_RetrySeconds = Math.Min(m_RetrySeconds * 2, k_MaxRetrySeconds);
                
                await handle.InitializeService();
            }
        }
    }
}
