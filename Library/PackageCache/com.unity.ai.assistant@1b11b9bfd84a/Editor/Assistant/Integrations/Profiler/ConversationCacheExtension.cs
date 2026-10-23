using Unity.AI.Assistant.FunctionCalling;

namespace Unity.AI.Assistant.Integrations.Profiler.Editor
{
    static class ConversationCacheExtension
    {
        static readonly ConversationCache s_ConversationCache = new();

        // Shared cache for MCP callers that have no conversation context.
        static FrameDataCache s_McpFrameDataCache;

        public static FrameDataCache GetFrameDataCache(this ConversationContext conversationContext)
        {
            if (conversationContext == null)
                return s_McpFrameDataCache ??= new FrameDataCache();

            return s_ConversationCache.GetOrCreateCache(conversationContext);
        }

        public static void ClearFrameDataCache(this ConversationContext conversationContext)
        {
            if (conversationContext == null)
            {
                s_McpFrameDataCache?.Dispose();
                s_McpFrameDataCache = null;
                return;
            }

            s_ConversationCache.ClearFrameDataCache(conversationContext);
        }

        public static void CleanUp()
        {
            s_ConversationCache?.CleanUp();
            s_McpFrameDataCache?.Dispose();
            s_McpFrameDataCache = null;
        }
    }
}
