namespace Unity.AI.Assistant.Utils.Perf
{
    // Temporary diagnostic instrumentation for UUM-146077.
    enum PerfGauge
    {
        ConversationMessages,
        StreamedCharsTotal,
        RetainedChatElements,
        MarkdownContentChars,
        MarkdownCharsProcessed,
        MarkdownTextElements,
        SlotContentElements,
        EmbeddingIndexAssets,
        KnowledgeQueueDepth,
        TranscriptJsonChars,
        FeedbackSubscribers,
        FeedbackQueueDepth,
        TodoStateEntries
    }
}
