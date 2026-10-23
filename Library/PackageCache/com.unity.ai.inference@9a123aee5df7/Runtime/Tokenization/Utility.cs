using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    static class Utility
    {
        public static (Pool<List<Token>> tokenPool, Pool<List<List<Token>>> sequencePool) InitSequencePool()
        {
            var tokenPool = new Pool<List<Token>>(() => new(), tokens => tokens.Clear());
            var sequencePool = new Pool<List<List<Token>>>(
                () => new(), sequence =>
                {
                    foreach (var tokens in sequence)
                        tokenPool.Release(tokens);
                    sequence.Clear();
                });
            return (tokenPool, sequencePool);
        }
    }
}
