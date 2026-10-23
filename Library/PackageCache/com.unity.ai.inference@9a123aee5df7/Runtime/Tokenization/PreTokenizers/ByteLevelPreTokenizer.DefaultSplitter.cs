namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    partial class ByteLevelPreTokenizer
    {
        internal class DefaultSplitter : IOneToManyConverter<SubString, SubString>
        {
            public void Convert(SubString input, Output<SubString> output)
            {
                if (input.IsNull)
                    return;

                output.Add(input);
            }
        }
    }
}
