using System;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Represents a token that can be added to a <see cref="Tokenizer"/> instance, with optional
    ///  properties that control its behavior.
    /// </summary>
    public readonly struct TokenConfiguration : IEquatable<TokenConfiguration>
    {
        readonly int m_HashCode;

        /// <summary>
        /// The ID of the token.
        /// </summary>
        public readonly int Id;

        /// <summary>
        /// The value of the token.
        /// </summary>
        public readonly string Value;

        /// <summary>
        /// Specifies whether the token should only match whole words.
        /// If set to <c>true</c>, the token will not match within a word.
        /// For example, the token `ing` would match tokenizing when this option is <c>false</c>,
        /// but not when it is <c>true</c>.
        /// Word boundaries are determined using regular expression rules, meaning the token
        /// must begin and end at word boundaries.
        /// </summary>
        public readonly bool WholeWord;

        /// <summary>
        /// Defines whether this token should strip all potential whitespaces on its left side,
        /// right side, or both.
        /// For example if we try to match the token <c>[MASK]</c> with <see cref="Strip"/> =
        /// <see cref="Direction.Left"/>, in the text "I saw a [MASK]", we would match on " [MASK]".
        /// (Note the space on the left).
        /// </summary>
        public readonly Direction Strip;

        /// <summary>
        /// Defines whether this token should match against the normalized version of the input
        /// text.
        /// For example, with the added token "yesterday", and a normalizer in charge of
        /// lowercasing the text, the token could be extract from the input "I saw a lion
        /// Yesterday".
        /// </summary>
        public readonly bool Normalized;

        /// <summary>
        /// Defines whether this token should be skipped when decoding.
        /// </summary>
        public readonly bool Special;

        /// <summary>
        /// Initializes a new instance of the <see cref="TokenConfiguration"/> type.
        /// </summary>
        /// <param name="id">
        /// The ID of the token.
        /// See <see cref="Id"/>
        /// </param>
        /// <param name="value">
        /// The value of the token.
        /// See <see cref="Value"/>.
        /// </param>
        /// <param name="wholeWord">
        /// Specifies whether the token should only match whole words.
        /// See <see cref="WholeWord"/>.
        /// </param>
        /// <param name="strip">
        /// Defines whether this token should strip all potential whitespaces on its left side,
        /// right side, or both.
        /// See <see cref="Strip"/>.
        /// </param>
        /// <param name="normalized">
        /// Defines whether this token should match against the normalized version of the input
        /// text.
        /// See <see cref="Normalized"/>.
        /// </param>
        /// <param name="special">
        /// Defines whether this token should be skipped when decoding.
        /// See <see cref="Special"/>.
        /// </param>
        public TokenConfiguration(int id, string value, bool wholeWord, Direction strip,
            bool normalized,
            bool special)
        {
            Id = id;
            Value = value;
            WholeWord = wholeWord;
            Strip = strip;
            Normalized = normalized;
            Special = special;
            m_HashCode = HashCode.Combine(Id, Value, WholeWord, (int) Strip, Normalized, Special);
        }

        /// <inheritdoc />
        public bool Equals(TokenConfiguration other) => Id == other.Id && Value == other.Value
            && WholeWord == other.WholeWord && Strip == other.Strip
            && Normalized == other.Normalized && Special == other.Special;

        /// <inheritdoc />
        public override bool Equals(object obj) => obj is TokenConfiguration other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => m_HashCode;
    }
}
