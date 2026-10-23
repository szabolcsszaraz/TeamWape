namespace Unity.InferenceEngine.Tokenization.Mappers
{
    public partial class UnigramMapper
    {
        struct BestPathNode
        {
            public int Id;
            public double BestPathScore;
            public int? StartsAt;
        }
    }
}
