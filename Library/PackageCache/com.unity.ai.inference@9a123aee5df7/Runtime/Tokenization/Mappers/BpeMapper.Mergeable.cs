namespace Unity.InferenceEngine.Tokenization.Mappers
{
    partial class BpeMapper
    {
        /// <summary>
        /// Represents a possible merge in the sequence of <see cref="Symbol" />s of the
        /// <see cref="Merger" /> conversion.
        /// </summary>
        struct Mergeable
        {
            /// <summary>
            /// The definition of the merged token.
            /// </summary>
            public int Id;

            /// <summary>
            /// The position of the merge in the word.
            /// It is a position of the first token of the pair.
            /// </summary>
            public int Position;

            /// <summary>
            /// The priority of the merge.
            /// It is how the merging priority queue choose the next most relevant merge rule to
            /// apply.
            /// </summary>
            public int Rank;
        }
    }
}
