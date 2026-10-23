using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    class OneToManyCachedConverter<TFrom, TTo> : IOneToManyConverter<TFrom, TTo>
    {
        readonly Dictionary<TFrom, TTo[]> m_Cache;
        readonly IOneToManyConverter<TFrom, TTo> m_Converter;
        readonly Func<TTo, TTo> m_Transform;
        readonly Pool<List<TTo>> m_OutputPool = new(() => new(), list => list.Clear());

        public OneToManyCachedConverter(
            [NotNull] IOneToManyConverter<TFrom, TTo> converter,
            [CanBeNull] Func<TTo, TTo> transform = null) : this(
            converter, EqualityComparer<TFrom>.Default, transform)
        { }

        public OneToManyCachedConverter(
            [NotNull] IOneToManyConverter<TFrom, TTo> converter,
            [NotNull] IEqualityComparer<TFrom> inputComparer,
            [CanBeNull] Func<TTo, TTo> transform = null)
        {
            if (inputComparer == null)
                throw new ArgumentNullException(nameof(inputComparer));

            m_Transform = transform ?? (t => t);

            m_Cache = new(inputComparer);
            m_Converter = converter ?? throw new ArgumentNullException(nameof(converter));
        }

        public void Convert(TFrom input, Output<TTo> output)
        {
            if (!m_Cache.TryGetValue(input, out var cached))
            {
                using var _ = m_OutputPool.Get(out var list);

                m_Converter.Convert(input, list.AsOutput(m_Transform));
                cached = list.ToArray();
                m_Cache.Add(input, cached);
            }

            output.AddRange(cached);
        }
    }
}
