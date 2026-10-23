namespace Unity.InferenceEngine.Tokenization.Mappers
{
    public partial class UnigramMapper
    {
        readonly struct ByteString
        {
            public readonly SubString Value;
            public readonly byte[] Bytes;

            public ByteString(SubString value, byte[] bytes)
            {
                Value = value;
                Bytes = bytes;
            }
        }
    }
}
