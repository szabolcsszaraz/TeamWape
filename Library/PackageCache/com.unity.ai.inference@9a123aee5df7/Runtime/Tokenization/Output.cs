using System;
using System.Collections.Generic;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Target interface for tokenization components.
    /// </summary>
    /// <typeparam name="T">Type of the data to store.</typeparam>
    public readonly ref struct Output<T>
    {
        readonly Action<T> m_Add;
        readonly Func<T, T> m_Transform;

        /// <summary>
        /// Initializes a new instance of the <see cref="Output{T}"/> type.
        /// </summary>
        /// <param name="add">
        /// The method to call when adding a new <typeparamref name="T" /> value.
        /// </param>
        /// <param name="transform">
        /// An optional transform method.
        /// Common implementation is cloning the value.
        /// </param>
        public Output(Action<T> add, Func<T, T> transform = null)
        {
            m_Add = add;
            m_Transform = transform ?? (t => t);
        }

        /// <summary>
        /// Adds a new value.
        /// </summary>
        /// <param name="item">
        /// The value to add.
        /// </param>
        public void Add(T item) => m_Add(m_Transform(item));

        /// <summary>
        /// Adds a collection of values.
        /// </summary>
        /// <param name="items">
        /// The values to add.
        /// </param>
        public void AddRange(IEnumerable<T> items)
        {
            if (items is IList<T> list)
            {
                for (var i = 0; i < list.Count; i++)
                    Add(list[i]);
            }
            else if (items is IReadOnlyList<T> roList)
            {
                for (var i = 0; i < roList.Count; i++)
                    Add(roList[i]);
            }
            else
            {
                foreach (var item in items)
                    Add(item);
            }
        }
    }

    /// <summary>
    /// Utility methods for <see cref="Output{T}"/>
    /// </summary>
    public static class OutputUtility
    {
        /// <summary>
        /// Creates an instance of <see cref="Output{T}"/> from the specified
        /// <paramref name="this"/>.
        /// </summary>
        /// <param name="this">
        /// The target storage.
        /// </param>
        /// <param name="transform">
        /// The transformation method.
        /// Common usage is cloning the values.
        /// </param>
        /// <typeparam name="T">
        /// The type of the values to add.
        /// </typeparam>
        /// <returns>
        /// An <see cref="Output{T}"/> wrapper.
        /// </returns>
        public static Output<T> AsOutput<T>(this ICollection<T> @this,
            Func<T, T> transform = null) => new(@this.Add, transform);
    }
}
