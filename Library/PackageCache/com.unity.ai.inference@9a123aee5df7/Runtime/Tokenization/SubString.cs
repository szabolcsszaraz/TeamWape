using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Unity.InferenceEngine.Tokenization
{
    /// <summary>
    /// Represents a portion of a <see cref="string" /> value.
    /// </summary>
    /// <remarks>
    /// This type is required as <see cref="ReadOnlySpan{T}" /> has some blocking constraints.
    /// </remarks>
    public readonly partial struct SubString
        : IEquatable<string>,
            IComparable<string>,
            IEquatable<SubString>,
            IComparable<SubString>,
            IEnumerable<char>
    {
        const string k_NullSourceExceptionMessage =
            "The underlying source of this substring is null";

        /// <summary>
        /// Efficiently compares <see cref="SubString" /> instances.
        /// </summary>
        public static readonly IEqualityComparer<SubString> k_Comparer = new ComparerImpl();

        /// <summary>
        /// Creates a <see cref="SubString" /> instance from a full <see cref="string" /> value.
        /// </summary>
        /// <param name="input">
        /// The original <see cref="string" /> value.
        /// The resulting <see cref="SubString" /> will cover the whole value of
        /// <paramref name="input" />.
        /// </param>
        /// <returns>
        /// A <see cref="SubString" /> instance covering the whole <paramref name="input" />.
        /// </returns>
        public static implicit operator SubString(string input) => new(input);

        /// <summary>
        /// Gets a <see cref="string" /> value from the portion of the source
        /// <see cref="string" /> of this <see cref="SubString" />.
        /// </summary>
        /// <param name="input">
        /// The <see cref="SubString" /> value to convert to a <see cref="string" /> value.
        /// </param>
        /// <returns>
        /// The <see cref="string" /> representing the value of this <see cref="SubString" />.
        /// </returns>
        public static implicit operator string(SubString input) => input.ToString();

        /// <summary>
        /// Computes a hashcode of this <see cref="SubString" /> instance with its
        /// <paramref name="prefix" /> and <paramref name="suffix" /> like if they were combined.
        /// </summary>
        /// <param name="value">
        /// A value to hash.
        /// </param>
        /// <param name="prefix">
        /// A prefix value to combine with this.
        /// </param>
        /// <param name="suffix">
        /// A suffix value to combine with this.
        /// </param>
        /// <returns>
        /// The hashcode of "{prefix}{this}{suffix}"
        /// </returns>
        public static int GetHashCode(ReadOnlySpan<char> value, SubString? prefix = null,
            SubString? suffix = null)
        {
            var hashA = 5381;
            var hashB = hashA;

            if (prefix.HasValue)
                Hash(prefix.Value.AsSpan(), ref hashA, ref hashB);

            Hash(value, ref hashA, ref hashB);

            if (suffix.HasValue)
                Hash(suffix.Value.AsSpan(), ref hashA, ref hashB);

            var hashCode = hashA + hashB * 1566083941;
            return hashCode;
        }

        static void Hash(ReadOnlySpan<char> source, ref int a, ref int b)
        {
            int i = 0, limit = source.Length;
            while (i < limit)
            {
                var c = source[i];
                a = ((a << 5) + a) ^ c;

                if (++i == limit)
                    break;

                c = source[i];
                b = ((b << 5) + b) ^ c;
                i++;
            }
        }

        /// <summary>
        /// The computed hash code of the portion.
        /// <see cref="SubString" /> uses its owns implementation of <see cref="string" /> hash
        /// code computation because the standard one is not exposed as a helper method by the
        /// standard library at the moment (recent versions of .NET exposes it).
        /// </summary>
        readonly int m_HashCode;

        /// <summary>
        /// Initializes a new instance of the <see cref="SubString" /> type.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="string" /> from which this <see cref="SubString" /> is built.
        /// </param>
        /// <param name="offsets">
        /// The bounds of the portion of <see cref="Source" /> to keep.
        /// </param>
        public SubString([CanBeNull] string source, Range offsets)
        {
            Source = source;
            if (source is null)
            {
                Offset = 0;
                Length = 0;
                m_HashCode = 0;
                return;
            }

            var (offset, length) = offsets.GetOffsetAndLength(source.Length);

            if (offset < 0 || offset > source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

            if (length < 0 || offset + length > source.Length)
                throw new ArgumentOutOfRangeException(nameof(length), length, null);

            Offset = offset;
            Length = length;
            m_HashCode = GetHashCode(source.AsSpan(offset, length));
        }

        SubString(string source, int offset, int length, int hashCode)
        {
            Source = source;
            Offset = offset;
            Length = length;
            m_HashCode = hashCode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SubString" /> type.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="string" /> from which this <see cref="SubString" /> is built.
        /// This constructor keeps the whole <paramref name="source" />.
        /// </param>
        public SubString(string source) : this(source, Range.All)
        {
        }

        /// <summary>
        /// Gets the character ar <paramref name="index" />.
        /// </summary>
        /// <param name="index">
        /// Index of the character to get.
        /// </param>
        public char this[int index] =>
            IsNull ? throw new IndexOutOfRangeException(k_NullSourceExceptionMessage) : Source[Offset + index];

        /// <inheritdoc cref="Sub" />
        public SubString this[Range offsets] => Sub(offsets);

        /// <summary>
        /// Tells whether the portion covers the source string.
        /// </summary>
        public bool IsApplied =>
           IsNull
                ? throw new NullReferenceException(k_NullSourceExceptionMessage)
                : Offset == 0 && Length == Source.Length;

        /// <summary>
        /// The source <see cref="string" /> from which this <see cref="SubString" /> is built.
        /// </summary>
        public string Source { get; }

        /// <summary>
        /// The lower bound of the portion of <see cref="Source" /> to keep.
        /// </summary>
        public int Offset { get; }

        /// <summary>
        /// The number of <see cref="char" /> of this portion.
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Gets the bounds of the subpart of the original string0
        /// </summary>
        public Range Offsets => new(Offset, Offset + Length);

        /// <summary>
        /// The number of Utf-8 valid characters of this portion.
        /// </summary>
        public int UtfLength => IsNull
            ? throw new NullReferenceException(k_NullSourceExceptionMessage)
            : TextElementUtility.GetUtfLength(Source.AsSpan()[Offset..(Offset + Length)]);

        /// <summary>
        /// Gets the byte length of the string.
        /// </summary>
        public int Utf8Length => System.Text.Encoding.UTF8.GetByteCount(Source, Offset, Length);

        /// <summary>
        /// Tells whether the substring does not reference any valid source.
        /// </summary>
        public bool IsNull => Source is null;

        /// <summary>
        /// Tells whether this instance is empty.
        /// </summary>
        public bool IsEmpty =>
            IsNull ? throw new NullReferenceException(k_NullSourceExceptionMessage) : Length == 0;

        /// <summary>
        /// Tells whether this instance is null, empty or if it just contains white spaces.
        /// </summary>
        public bool IsNullOrWhiteSpace
        {
            get
            {
                if(IsNull) return true;

                for (int i = Offset, limit = Offset + Length; i < limit; i++)
                    if (!char.IsWhiteSpace(Source[i]))
                        return false;

                return true;
            }
        }

        /// <summary>
        /// Returns a new <see cref="SubString" /> value which source <see cref="string" /> is the
        /// portion of this one.
        /// </summary>
        /// <returns>
        /// A new <see cref="SubString" /> value which source is the portion of this one.
        /// </returns>
        /// <remarks>
        /// If the hash code has already been computed for this <see cref="SubString" />, it is
        /// copied to the new one.
        /// </remarks>
        public SubString Apply() => IsApplied ? this : new SubString(Source, Offset, Length, m_HashCode);

        /// <summary>
        /// Creates a new read-only span over a string.
        /// </summary>
        /// <returns>
        /// The read-only span representation of the string.
        /// </returns>
        public ReadOnlySpan<char> AsSpan() => Source.AsSpan(Offset, Length);

        /// <summary>
        /// Creates a new read-only span over a portion of the target string from a specified
        /// position for a specified number of characters.
        /// </summary>
        /// <param name="offset">
        /// The index at which to begin this slice.
        /// </param>
        /// <param name="length">
        /// The desired length for the slice.
        /// </param>
        /// <returns>
        /// The read-only span representation of the string.
        /// </returns>
        public ReadOnlySpan<char> AsSpan(int offset, int length)
        {
            if(offset < 0)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

            if(length < 0)
                throw new ArgumentOutOfRangeException(nameof(length), length, null);

            if (offset + length > Length)
                throw new ArgumentOutOfRangeException($"{nameof(offset)} + {nameof(length)}", offset + length, null);

            return Source.AsSpan(Offset + offset, length);
        }

        /// <summary>
        /// Gets a portion of this instance.
        /// </summary>
        /// <param name="offsets">
        /// The bounds of the subpart to extract.
        /// </param>
        /// <returns>
        /// A new <see cref="SubString" /> instance.
        /// </returns>
        public SubString Sub(Range offsets)
        {
            if(IsNull)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var (offset, length) = offsets.GetOffsetAndLength(Length);
            offset += Offset;

            if (offset + length > Source.Length)
                throw new ArgumentOutOfRangeException(nameof(offset), offset, null);

            offsets = new(offset, offset + length);

            return new(Source, offsets);
        }

        /// <summary>
        /// Gets a portion of this instance, considering the unicode characters instead of chars.
        /// </summary>
        /// <param name="offsets">
        /// The bounds of the subpart to extract.
        /// </param>
        /// <returns>
        /// A new <see cref="SubString" /> instance.
        /// </returns>
        public SubString UtfSub(Range offsets)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var charOffset = 0;

            var (utfSubOffset, utfSubLength) = offsets.GetOffsetAndLength(UtfLength);

            for (var i = 0; i < utfSubOffset; i++)
            {
                if (char.IsHighSurrogate(Source[Offset + charOffset]) && charOffset + 1 < Length
                    && char.IsLowSurrogate(Source[Offset + charOffset + 1]))
                    charOffset++;

                charOffset++;
            }

            var charLength = 0;

            for (var i = 0; i < utfSubLength; i++)
            {
                if (charOffset + charLength >= Length)
                    throw new ArgumentOutOfRangeException(nameof(utfSubLength));

                if (char.IsHighSurrogate(Source[Offset + charOffset + charLength])
                       && charOffset + charLength + 1 < Length
                       && char.IsLowSurrogate(Source[Offset + charOffset + charLength + 1]))
                    charLength++;

                charLength++;
            }

            offsets = new(Offset + charOffset, Offset + charOffset + charLength);
            return new(Source, offsets);
        }

        /// <summary>
        /// Creates a substring based on UTF-8 byte offsets rather than character offsets.
        /// This method converts UTF-8 byte-based range indices to character-based indices and
        /// returns the corresponding substring.
        /// The byte offsets must align with UTF-8 character boundaries.
        /// </summary>
        /// <param name="offsets">
        /// A range specifying the start and end positions in UTF-8 bytes.
        /// The range is relative to the UTF-8 byte representation of the current substring, and
        /// both start and end positions must align with character boundaries in the UTF-8 encoding.
        /// </param>
        /// <returns>
        /// A new <see cref="SubString"/> representing the portion of the current substring
        /// specified by the UTF-8 byte range.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown when the specified byte offsets do not align with character boundaries in the
        /// UTF-8 encoding, or when the offsets extend beyond the bounds of the current substring.
        /// </exception>
        /// <remarks>
        /// This method properly handles Unicode surrogate pairs and multi-byte UTF-8 characters.
        /// It ensures that the resulting substring boundaries align with complete Unicode
        /// characters rather than splitting characters in the middle.
        /// The conversion process walks through the string character by character, accumulating
        /// UTF-8 byte counts until the specified byte positions are reached.
        /// </remarks>
        /// <example>
        /// <code>
        /// var text = new SubString("Hello 世界");
        /// // "世" is 3 bytes in UTF-8, "界" is 3 bytes in UTF-8
        /// var substring = text.Utf8ByteSub(6..9); // Gets "世" (bytes 6-8 in UTF-8)
        /// </code>
        /// </example>
        public SubString Utf8ByteSub(Range offsets)
        {
            var byteLength = System.Text.Encoding.UTF8.GetByteCount(Source, Offset, Length);

            var (subByteOffset, subByteLength) = offsets.GetOffsetAndLength(byteLength);

            var charStart = 0;
            var byteI = 0;
            while (byteI < subByteOffset)
            {
                if (charStart >= Length)
                    throw new ArgumentNullException(nameof(offsets),
                        "Byte offset must match a character");

                var utfCharCount = char.IsHighSurrogate(Source[Offset + charStart])
                    && charStart + 1 < Length
                    && char.IsLowSurrogate(Source[Offset + charStart + 1])
                        ? 2
                        : 1;

                byteI +=
                    System.Text.Encoding.UTF8.GetByteCount(Source, Offset + charStart,
                        utfCharCount);
                charStart += utfCharCount;
            }

            if(byteI != subByteOffset)
                throw new ArgumentOutOfRangeException(nameof(offsets), "Byte offset must match a character");

            if (subByteOffset + subByteLength == byteLength)
                return this[charStart..];

            var charEnd = charStart;
            while (byteI < subByteOffset + subByteLength)
            {
                if (charEnd >= Length)
                    throw new ArgumentNullException(nameof(offsets),
                        "Byte offset must match a character");

                var utfCharCount = char.IsHighSurrogate(Source[Offset + charEnd])
                    && charEnd + 1 < Length
                    && char.IsLowSurrogate(Source[Offset + charEnd + 1])
                        ? 2
                        : 1;

                byteI +=
                    System.Text.Encoding.UTF8.GetByteCount(Source, Offset + charEnd, utfCharCount);
                charEnd += utfCharCount;
            }

            if(byteI != subByteOffset + subByteLength)
                throw new ArgumentOutOfRangeException(nameof(offsets), "Byte offset must match a character");

            return this[charStart .. charEnd];
        }

        /// <summary>
        /// Tells whether this <see cref="SubString" /> starts with the specified
        /// <paramref name="prefix" />.
        /// </summary>
        /// <param name="prefix">
        /// The pattern to compare to the beginning of this <see cref="SubString" />.
        /// </param>
        /// <returns>
        /// Whether this <see cref="SubString" /> starts with the specified
        /// <paramref name="prefix" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// <paramref name="prefix" /> cannot ne <c>null</c>.
        /// </exception>
        public bool StartsWith(SubString prefix)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            if (prefix.Source is null)
                throw new ArgumentNullException(nameof(prefix));

            var prefixLength = prefix.Length;
            if (Offset + prefixLength > Source.Length)
                return false;

            unsafe
            {
                fixed (char* pSource = Source)
                fixed (char* pPrefixSource = prefix.Source)
                {
                    var pMe = pSource + Offset;
                    var pPrefix = pPrefixSource + prefix.Offset;
                    for (var i = 0; i < prefixLength; i++)
                        if (pPrefix[i] != pMe[i])
                            return false;
                }
            }

            return true;
        }

        /// <inheritdoc />
        public int CompareTo(string other) => CompareTo((SubString)other);

        /// <inheritdoc />
        public unsafe int CompareTo(SubString other)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var (from, length) = (Offset, Length);
            var (otherFrom, otherLength) = (other.Offset, other.Length);

            var testLength = length < otherLength ? length : otherLength;

            fixed (char* pSource = Source)
            fixed (char* pOtherSource = other.Source)
            {
                var pSub = pSource + from;
                var pOtherSub = pOtherSource + otherFrom;

                for (var i = 0; i < testLength; i++)
                {
                    var comp = pSub[i].CompareTo(pOtherSub[i]);
                    if (comp != 0)
                        return comp;
                }
            }

            return length - otherLength;
        }

        /// <summary>
        /// Deconstructs this <see cref="SubString" />.
        /// </summary>
        /// <param name="source">
        /// The source <see cref="string" /> from which this <see cref="SubString" /> is built.
        /// </param>
        /// <param name="offsets">
        /// The bounds of the portion of <see cref="Source" /> to keep.
        /// </param>
        public void Deconstruct(out string source, out Range offsets)
        {
            if (IsNull)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            source = Source;
            offsets = Offsets;
        }

        /// <summary>
        /// Returns the index of the first match of <paramref name="sub"/>, starting from
        /// <paramref name="startIndex"/>, or <c>-1</c>.
        /// </summary>
        /// <param name="sub">
        /// The pattern to find.
        /// </param>
        /// <param name="startIndex">
        /// The index from which to start looking for <paramref name="sub"/>.
        /// </param>
        /// <param name="comparison">
        /// <see cref="string"/> comnparison method.
        /// </param>
        /// <returns>
        /// The index of the first match of <paramref name="sub"/>
        /// </returns>
        /// <exception cref="NullReferenceException">
        /// <see cref="Source"/> cannot be <c>null</c>.
        /// </exception>
        public int IndexOf(SubString sub, int startIndex = 0, StringComparison comparison = StringComparison.Ordinal)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var pattern = sub.AsSpan();

            for (int i = startIndex, limit = Length - sub.Length; i <= limit; i++)
            {
                var candidate = AsSpan(i, pattern.Length);
                if (pattern.CompareTo(candidate, comparison) == 0)
                    return i;
            }

            return -1;
        }

        /// <summary>
        /// Computes a hashcode of this <see cref="SubString" /> instance with its
        /// <paramref name="prefix" /> and <paramref name="suffix" /> like if they were combined.
        /// </summary>
        /// <param name="prefix">
        /// A prefix value to combine with this.
        /// </param>
        /// <param name="suffix">
        /// A suffix value to combine with this.
        /// </param>
        /// <returns>
        /// The hashcode of "{prefix}{this}{suffix}"
        /// </returns>
        public int GetHashCode(SubString? prefix, SubString? suffix) => GetHashCode(AsSpan(), prefix, suffix);

        /// <inheritdoc />
        public bool Equals(string other) => other != null && Equals((SubString)other);

        /// <inheritdoc />
        public bool Equals(SubString other)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            if (other.Source is null)
                return false;

            var length = Length;
            if (length != other.Length)
                return false;

            unsafe
            {
                fixed (char* pSource = Source)
                fixed (char* pOther = other.Source)
                {
                    var pChar = pSource + Offset;
                    var pOtherChar = pOther + other.Offset;

                    for (var i = 0; i < length; i++)
                        if (pChar[i] != pOtherChar[i])
                            return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Gets all the runes of the substring as substrings.
        /// </summary>
        /// <param name="output">
        /// Target list for characters.
        /// </param>
        /// <returns>
        /// The number of copied characters.
        /// </returns>
        public int GetRunes(Output<SubString> output)
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var count = 0;
            for (int i = 0, limit = Length; i < limit; i++)
            {
                if (char.IsHighSurrogate(this[i]) && i + 1 < limit
                    && char.IsLowSurrogate(this[i + 1]))
                {
                    output.Add(this[i ..(i + 2)]);
                    i++;
                }
                else
                {
                    output.Add(this[i..(i + 1)]);
                }

                count++;
            }
            return count;
        }

        /// <inheritdoc />
        public override int GetHashCode() => m_HashCode;

        /// <inheritdoc />
        public override string ToString() => IsApplied ? Source : Source.Substring(Offset, Length);

        /// <summary>
        /// Gets the sequence of <see cref="char" /> from the portion of the source
        /// <see cref="string" /> covered by this <see cref="SubString" />.
        /// </summary>
        /// <returns>
        /// The sequence of <see cref="char" /> from the portion of the source
        /// <see cref="string" /> covered by this <see cref="SubString" />.
        /// </returns>
        IEnumerable<char> GetChars()
        {
            if (Source is null)
                throw new NullReferenceException(k_NullSourceExceptionMessage);

            var (source, from, to) = (Source, Offset, Offset + Length);
            for (var i = from; i < to; i++)
                yield return source[i];
        }

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() => GetChars().GetEnumerator();

        /// <inheritdoc />
        IEnumerator<char> IEnumerable<char>.GetEnumerator() => GetChars().GetEnumerator();
    }
}
