using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    partial struct SubString
    {
        class ComparerImpl : IEqualityComparer<SubString>
        {
            public bool Equals(SubString x, SubString y) => x.Equals(y);

            public int GetHashCode(SubString subString) => subString.GetHashCode();
        }
    }
}
