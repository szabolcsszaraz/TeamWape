namespace Unity.AI.Assistant.Utils.Perf
{
    // Temporary diagnostic instrumentation for UUM-146077. Remove with the rest of this namespace once the root cause is known.
    enum PerfProbe
    {
        StreamChunkNotify,
        AcpStreamChunk,
        AcpToolCallNotify,
        ConversationChangedRequested,
        ConversationChangedDispatched,
        ConvertConversationToModel,
        ConversationReload,
        ConversationPopulate,
        PopulateAddRows,
        ChatListRefresh,
        ConversationPanelDiff,
        BuildMarkdownChunks,
        RefreshText,
        NotifyVisibleElements,
        ResponseApiStateChanged,
        TranscriptSaveRequested,
        TranscriptValidate,
        TranscriptSerialize,
        TranscriptWrite,
        PeriodicSaveTick,
        EmbeddingIndexAdd,
        EmbeddingIndexSaveNow,
        AssetQueueProcess,
        AssetDependencyMapBuild,
        FeedbackLoadDispatch,
        TodoStateSave,
        TodoStateLoad
    }
}
