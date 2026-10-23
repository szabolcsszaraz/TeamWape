using System;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization.PostProcessors.Templating
{
    /// <summary>
    /// Represents a special token in a <see cref="Template" />.
    /// </summary>
    public class SpecialToken : Piece, IEquatable<SpecialToken>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpecialToken" /> type.
        /// </summary>
        /// <param name="value">
        /// The value of the token.
        /// </param>
        /// <param name="sequenceId">
        /// The type id of the sequence.
        /// </param>
        public SpecialToken([NotNull] string value, int sequenceId) : base(sequenceId) =>
            Value = value ?? throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// The value of the token.
        /// </summary>
        public string Value { get; }

        /// <inheritdoc />
        public bool Equals(SpecialToken other) =>
            other != null && Value == other.Value && SequenceId == other.SequenceId;

        /// <inheritdoc />
        protected override bool PieceEquals(Piece other) =>
            other is SpecialToken token && Equals(token);

        /// <inheritdoc />
        protected override int GetPieceHashCode() =>
            HashCode.Combine(Value, SequenceId);

        /// <inheritdoc />
        public override string ToString() => $"{Value}:{SequenceId}";
    }
}
