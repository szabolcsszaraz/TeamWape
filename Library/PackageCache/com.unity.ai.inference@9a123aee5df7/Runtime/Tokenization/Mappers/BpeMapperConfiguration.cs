namespace Unity.InferenceEngine.Tokenization.Mappers
{
    /// <summary>
    /// Configuration settings for the Byte Pair Encoding (BPE) mapper used in tokenization.
    /// </summary>
    public struct BpeMapperOptions
    {
        /// <summary>
        /// Gets or sets the dropout rate applied during BPE merge operations.
        /// When specified, randomly skips merges during training to improve robustness.
        /// </summary>
        /// <value>
        /// A float value between 0.0 and 1.0 representing the dropout probability,
        /// or <c>null</c> to disable dropout.
        /// </value>
        public float? DropOut;

        /// <summary>
        /// Whether or not to direct output words if they are part of the vocab.
        /// Not yet implemented.
        /// </summary>
        public bool? IgnoreMerges;

        /// <summary>
        /// Gets or sets the token string used to represent unknown or out-of-vocabulary words.
        /// </summary>
        /// <value>
        /// A string representing the unknown token (commonly "&lt;unk&gt;" or "[UNK]"),
        /// or <c>null</c> if no unknown token is specified.
        /// </value>
        public string UnknownToken;

        /// <summary>
        /// Gets or sets a value indicating whether to fuse consecutive unknown tokens
        /// into a single unknown token.
        /// </summary>
        /// <value>
        /// <c>true</c> to fuse unknown tokens; <c>false</c> to keep them separate;
        /// <c>null</c> to use default behavior.
        /// </value>
        public bool? FuseUnknown;

        /// <summary>
        /// Gets or sets a value indicating whether to fall back to byte-level encoding
        /// when encountering characters that cannot be tokenized normally.
        /// </summary>
        /// <value>
        /// <c>true</c> to enable byte-level fallback; <c>false</c> to disable;
        /// <c>null</c> to use default behavior.
        /// </value>
        public bool? ByteFallback;

        /// <summary>
        /// Gets or sets the prefix string added to subword tokens to distinguish them
        /// from complete words.
        /// </summary>
        /// <value>
        /// A string prefix (commonly "##" or "@@") added to subword tokens,
        /// or <c>null</c> if no prefix is used.
        /// </value>
        public string SubWordPrefix;

        /// <summary>
        /// Gets or sets the suffix string added to word tokens to mark word boundaries.
        /// </summary>
        /// <value>
        /// A string suffix (commonly "@@" or specific boundary markers) added to word tokens,
        /// or <c>null</c> if no suffix is used.
        /// </value>
        public string WordSuffix;
    }
}
