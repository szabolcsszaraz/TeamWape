using System;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Tells whether performing a process to the <see cref="Left"/>, to the
    /// <see cref="Right"/>, or both.
    /// </summary>
    [Flags]
    public enum Direction
    {
        /// <summary>
        /// Default value. No direction.
        /// </summary>
        None = 0,

        /// <summary>
        /// Performs a process to the left direction.
        /// </summary>
        Left = 1,

        /// <summary>
        /// Performs a process to the right direction.
        /// </summary>
        Right = 2,

        /// <summary>
        /// Performs a process to both directions.
        /// </summary>
        Both = Left | Right
    }

    /// <summary>
    /// Utility methods for <see cref="Direction"/>.
    /// </summary>
    static class DirectionUtility
    {
        /// <summary>
        /// Tells whether <paramref name="this"/> <see cref="Direction"/> contains at least the
        /// specified <paramref name="direction"/>.
        /// This version prevents allocation due to <see cref="Enum.HasFlag"/> boxing.
        /// </summary>
        /// <param name="this">
        /// The direction to test.
        /// </param>
        /// <param name="direction">
        /// The direction to search.
        /// </param>
        /// <returns>
        /// Whether <paramref name="this"/> <see cref="Direction"/> contains at least the specified
        /// <paramref name="direction"/>
        /// </returns>
        public static bool Match(this Direction @this, Direction direction) =>
            (@this & direction) == direction;
    }
}
