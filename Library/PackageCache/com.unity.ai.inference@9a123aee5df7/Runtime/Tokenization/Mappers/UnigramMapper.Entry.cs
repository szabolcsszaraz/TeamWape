using SEncoding = System.Text.Encoding;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    public partial class UnigramMapper
    {
        class VocabEntry
        {
            public readonly int Id;
            public readonly string Value;
            public readonly double Score;
            public readonly byte[] Bytes;

            public VocabEntry(int id, string value, double score)
            {
                Id = id;
                Value = value;
                Score = score;
                Bytes = SEncoding.UTF8.GetBytes(Value);
            }

            public void Deconstruct(out int id, out string value, out double score,
                out byte[] bytes)
            {
                id = Id;
                value = Value;
                score = Score;
                bytes = Bytes;
            }

            public void Deconstruct(out int id, out string value, out double score)
            {
                id = Id;
                value = Value;
                score = Score;
            }

            public void Deconstruct(out string value, out double score)
            {
                value = Value;
                score = Score;
            }
        }
    }
}
