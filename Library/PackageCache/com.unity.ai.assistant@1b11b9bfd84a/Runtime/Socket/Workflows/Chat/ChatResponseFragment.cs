namespace Unity.AI.Assistant.Socket.Workflows.Chat
{
    struct ChatResponseFragment
    {
        public string Fragment { get; set; }
        public string Id { get; set; }
        public string CorrelationMessageId { get; set; }
        public bool IsLastFragment { get; set; }
        public int? UsedTokens { get; set; }
        public int? MaxTokens { get; set; }
    }
}
