using System;

namespace Unity.InferenceEngine.Tokenization.PostProcessors.Templating
{
    /// <summary>
    /// Represents a sequence of tokens in a <see cref="Template" />.
    /// </summary>
    public class Sequence : Piece, IEquatable<Sequence>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="Sequence" /> type.
        /// </summary>
        /// <param name="identifier">
        /// Identifies the sequence:
        /// <list type="bullet">
        ///     <item>
        ///         <term>
        ///             <see cref="SequenceIdentifier.A" /> for the primary sequence.
        ///         </term>
        ///     </item>
        ///     <item>
        ///         <term>
        ///             <see cref="SequenceIdentifier.B" /> for the secondary sequence.
        ///         </term>
        ///     </item>
        /// </list>
        /// </param>
        /// <param name="sequenceId">
        /// The type id of the sequence.
        /// </param>
        public Sequence(SequenceIdentifier identifier, int sequenceId) : base(sequenceId)
        {
            Identifier = identifier;
        }

        /// <summary>
        /// Identifies the sequence:
        /// <list type="bullet">
        ///     <item>
        ///         <term>
        ///             <see cref="SequenceIdentifier.A" /> for the primary sequence.
        ///         </term>
        ///     </item>
        ///     <item>
        ///         <term>
        ///             <see cref="SequenceIdentifier.B" /> for the secondary sequence.
        ///         </term>
        ///     </item>
        /// </list>
        /// </summary>
        public SequenceIdentifier Identifier { get; }

        /// <inheritdoc />
        public bool Equals(Sequence other) =>
            other != null && Identifier == other.Identifier && SequenceId == other.SequenceId;

        /// <inheritdoc />
        protected override bool PieceEquals(Piece other) =>
            other is Sequence sequence && Equals(sequence);

        /// <inheritdoc />
        protected override int GetPieceHashCode() =>
            HashCode.Combine((int) Identifier, SequenceId);

        /// <inheritdoc />
        public override string ToString() => $"${Identifier}:{SequenceId}";
    }
}
