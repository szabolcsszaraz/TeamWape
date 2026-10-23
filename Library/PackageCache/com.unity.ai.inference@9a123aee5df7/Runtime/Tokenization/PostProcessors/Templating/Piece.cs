using System;

namespace Unity.InferenceEngine.Tokenization.PostProcessors.Templating
{
    /// <summary>
    /// An element of a template.
    /// It can be either a <see cref="Sequence" /> or a <see cref="SpecialToken" />.
    /// </summary>
    public abstract class Piece : IEquatable<Piece>
    {
        /// <summary>
        /// The type id of the sequence.
        /// </summary>
        public int SequenceId { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Piece"/> type.
        /// </summary>
        /// <param name="sequenceId">
        /// The ID of the sequence this piece belongs to.
        /// </param>
        protected Piece(int sequenceId) => SequenceId = sequenceId;

        /// <inheritdoc />
        public bool Equals(Piece other) => PieceEquals(other);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Piece piece && Equals(piece);

        /// <inheritdoc cref="object.GetHashCode"/>
        public override int GetHashCode() => GetPieceHashCode();

        /// <inheritdoc cref="object.ToString"/>
        public abstract override string ToString();

        /// <summary>
        /// Tell whether this <see cref="Piece" /> equals the <paramref name="other" /> one.
        /// </summary>
        /// <param name="other">
        /// The other <see cref="Piece" /> to compare.
        /// </param>
        /// <returns>
        /// Whether this <see cref="Piece" /> equals the <paramref name="other" /> one.
        /// </returns>
        protected abstract bool PieceEquals(Piece other);

        /// <summary>
        /// Gets the hash code of this <see cref="Piece" />.
        /// </summary>
        /// <returns>
        /// The hash code of this <see cref="Piece" />.
        /// </returns>
        protected abstract int GetPieceHashCode();
    }
}
