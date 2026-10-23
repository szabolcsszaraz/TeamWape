using System;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Represents the data of a token in a sequence.
    /// </summary>
    public readonly struct Token : IEquatable<Token>
    {
        /// <summary>
        /// ID of the token.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// Value of the token.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Attention of the token.
        /// </summary>
        public readonly bool Attention;

        /// <summary>
        /// Identifies the subsequence this token belongs to, in a sequence of token computed from a
        /// pair of input.
        /// </summary>
        public readonly int TypeId;

        /// <summary>
        /// Tells whether the token is special.
        /// </summary>
        public readonly bool Special;

        /// <summary>
        /// The portion of the original input represented by this <see cref="Token"/>.
        /// </summary>
        public readonly Range Offsets;

        readonly int m_HashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> type.
        /// </summary>
        /// <param name="id">
        /// ID of the token.
        /// </param>
        /// <param name="value">
        /// Value of the token.
        /// </param>
        /// <param name="attention">
        /// Attention of the token.
        /// </param>
        /// <param name="typeId">
        /// Identifies the subsequence this token belongs to, in a sequence of token computed from a
        /// pair of inputs.
        /// </param>
        /// <param name="special">
        /// Whether the token is special.
        /// </param>
        /// <param name="offsets">
        /// The portion of the original input represented by this <see cref="Token"/>.
        /// </param>
        public Token(int id, string value = null, bool attention = false, int typeId = 0,
            bool special = false,
            Range offsets = default)
        {
            Id = id;
            Value = value;
            Attention = attention;
            TypeId = typeId;
            Special = special;
            Offsets = offsets;

            m_HashCode = HashCode.Combine(Id, Value, Attention, TypeId, Special, Offsets);
        }

        /// <summary>
        /// Creates a new token with a new <see cref="Id"/> value.
        /// </summary>
        /// <param name="id">
        /// The Id of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a id set to <paramref name="id"/>.
        /// </returns>
        public Token SetId(int id) => new(id, Value, Attention, TypeId, Special, Offsets);

        /// <summary>
        /// Creates a new token with a new <see cref="Value"/> value.
        /// </summary>
        /// <param name="value">
        /// The value of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a value set to <paramref name="value"/>.
        /// </returns>
        public Token SetValue(string value) => new(Id, value, Attention, TypeId, Special, Offsets);

        /// <summary>
        /// Creates a new token with a new <see cref="Attention"/> value.
        /// </summary>
        /// <param name="attention">
        /// The attention of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a attention set to <paramref name="attention"/>.
        /// </returns>
        public Token SetAttention(bool attention) =>
            new(Id, Value, attention, TypeId, Special, Offsets);

        /// <summary>
        /// Creates a new token with a new <see cref="TypeId"/> value.
        /// </summary>
        /// <param name="typeId">
        /// The typeId of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a typeid set to <paramref name="typeId"/>.
        /// </returns>
        public Token SetTypeId(int typeId) => new(Id, Value, Attention, typeId, Special, Offsets);

        /// <summary>
        /// Creates a new token with a new <see cref="Special"/> value.
        /// </summary>
        /// <param name="special">
        /// The special state of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a special set to <paramref name="special"/>.
        /// </returns>
        public Token SetSpecial(bool special) =>
            new(Id, Value, Attention, TypeId, special, Offsets);

        /// <summary>
        /// Creates a new token with a new <see cref="Offsets"/> value.
        /// </summary>
        /// <param name="offsets">
        /// The range of the new token.
        /// </param>
        /// <returns>
        /// The new token, with a offsets set to <paramref name="offsets"/>.
        /// </returns>
        public Token SetOffsets(Range offsets) =>
            new(Id, Value, Attention, TypeId, Special, offsets);

        /// <summary>
        /// Deconstructs a token.
        /// </summary>
        /// <param name="id">
        /// ID of the token.
        /// </param>
        /// <param name="value">
        /// Value of the token.
        /// </param>
        /// <param name="attention">
        /// Attention of the token.
        /// </param>
        /// <param name="typeId">
        /// Identifies the subsequence this token belongs to, in a sequence of token computed from a
        /// pair of inputs.
        /// </param>
        /// <param name="special">
        /// Tells whether the token is special.
        /// </param>
        /// <param name="offsets">
        /// The portion of the original input represented by this <see cref="Token"/>.
        /// </param>
        public void Deconstruct(out int id, out string value, out int attention, out int typeId,
            out int special,
            out Range offsets)
        {
            id = Id;
            value = Value;
            attention = Attention ? 1 : 0;
            typeId = TypeId;
            special = Special ? 1 : 0;
            offsets = Offsets;
        }

        /// <inheritdoc />
        public bool Equals(Token other) =>
            Id == other.Id &&
            Attention == other.Attention &&
            (Value is null && other.Value is null || Value is not null
                && Value.Equals(other.Value, StringComparison.InvariantCulture)) &&
            TypeId == other.TypeId &&
            Special == other.Special &&
            Offsets.Equals(other.Offsets);

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is Token other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => m_HashCode;

        /// <inheritdoc />
        public override string ToString() =>
            $"[{nameof(Id)}: {Id}, {nameof(Value)}: {Value}, {nameof(Attention)}: {Attention}, {nameof(TypeId)}: {TypeId}, {nameof(Special)}: {Special}, {nameof(Offsets)}: {Offsets}]";
    }
}
