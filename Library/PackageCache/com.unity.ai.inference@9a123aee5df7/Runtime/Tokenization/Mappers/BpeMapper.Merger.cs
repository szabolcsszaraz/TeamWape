using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace Unity.InferenceEngine.Tokenization.Mappers
{
    partial class BpeMapper
    {
        internal class Merger : IManyToManyConverter<Token, Token>
        {
            readonly Pool<PriorityQueue<Mergeable>> m_MergeableQueuePool = new(
                () => new(Compare), l => l.Clear());

            readonly ReadOnlyDictionary<long, (int rank, Token token)> m_Merges;

            public Merger(IEnumerable<(Token a, Token b, Token merged, int rank)> merges)
            {
                var mergesLut = new Dictionary<long, (int rank, Token token)>();
                foreach (var (a, b, merged, rank) in merges)
                    mergesLut.Add(GetPairId(a.Id, b.Id), (rank, merged));

                m_Merges = new ReadOnlyDictionary<long, (int, Token)>(mergesLut);
            }

            public void Convert(
                IReadOnlyList<Token> input,
                Output<Token> output)
            {
                if (input.Count == 0)
                    return;

                var symbols = new Symbol[input.Count];
                for (var position = 0; position < input.Count; position++)
                {
                    symbols[position] = new()
                    {
                        Token = input[position],
                        Position = position,
                        Previous = position - 1,
                        Next = position + 1,
                        Discarded = false
                    };
                }
                symbols[^1].Next = -1;

                Merge(symbols);

                for (int sI = 0, sLimit = symbols.Length; sI < sLimit; sI++)
                {
                    var symbol = symbols[sI];
                    if (symbol.Discarded)
                        continue;
                    output.Add(symbol.Token);
                }
            }

            static int Compare(Mergeable a, Mergeable b)
            {
                var rank = a.Rank - b.Rank;
                return rank != 0 ? rank : a.Position - b.Position;
            }

            static long GetPairId(int a, int b) => ((long) a << 32) | (uint) b;

            void Merge(Symbol[] symbols)
            {
                using var _ = m_MergeableQueuePool.Get(out var mergeableQueue);

                {
                    ref var a = ref symbols[0];
                    var sI = 1;
                    while (sI < symbols.Length)
                    {
                        ref var b = ref symbols[sI++];
                        TryAddMergeable(ref a, ref b, mergeableQueue);
                        a = ref b;
                    }
                }

                while (mergeableQueue.TryPop(out var mergeable))
                {
                    ref var symbolA = ref symbols[mergeable.Position];
                    if (symbolA.Discarded || symbolA.Next == -1)
                        continue;

                    ref var symbolB = ref symbols[symbolA.Next];

                    if (symbolB.Discarded)
                        continue;

                    var pairId = GetPairId(symbolA.Token.Id, symbolB.Token.Id);
                    if (!m_Merges.TryGetValue(pairId, out var merged))
                        continue;

                    if (merged.token.Id != mergeable.Id)
                        continue;

                    symbolA.Token = merged.token;
                    symbolB.Discarded = true;

                    symbolA.Next = symbolB.Next;
                    if (symbolB.Next != -1)
                        symbols[symbolB.Next].Previous = symbolA.Position;

                    // Try pair with previous
                    if (symbolA.Previous != -1)
                        TryAddMergeable(ref symbols[symbolA.Previous], ref symbolA, mergeableQueue);

                    // Try pair with next
                    if (symbolA.Next != -1)
                        TryAddMergeable(ref symbolA, ref symbols[symbolA.Next], mergeableQueue);
                }
            }

            void TryAddMergeable(ref Symbol a, ref Symbol b, PriorityQueue<Mergeable> target)
            {
                var pairId = GetPairId(a.Token.Id, b.Token.Id);

                if (!m_Merges.TryGetValue(pairId, out var merged))
                    return;

                var mergeable = new Mergeable
                {
                    Id = merged.token.Id, Position = a.Position, Rank = merged.rank
                };

                target.Push(mergeable);
            }
        }
    }
}
