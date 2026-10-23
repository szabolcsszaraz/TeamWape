namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Normalizes text by removing leading and/or trailing whitespace characters.
    /// </summary>
    /// <remarks>
    /// This normalizer strips whitespace from the beginning and/or end of a substring
    /// based on the configured options. Whitespace is determined using <see cref="char.IsWhiteSpace"/>,
    /// which includes spaces, tabs, newlines, carriage returns, and other Unicode whitespace characters.
    /// Whitespace within the content (between non-whitespace characters) is preserved.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Strip both sides (default)
    /// var normalizer = new StripNormalizer();
    /// var result = normalizer.Normalize(new SubString("  hello  "));
    /// // result: "hello"
    ///
    /// // Strip left only
    /// var leftNormalizer = new StripNormalizer(left: true, right: false);
    /// var result = leftNormalizer.Normalize(new SubString("  hello  "));
    /// // result: "hello  "
    ///
    /// // Strip right only
    /// var rightNormalizer = new StripNormalizer(left: false, right: true);
    /// var result = rightNormalizer.Normalize(new SubString("  hello  "));
    /// // result: "  hello"
    /// </code>
    /// </example>
    public class StripNormalizer : INormalizer
    {
        readonly bool m_Left;
        readonly bool m_Right;

        /// <summary>
        /// Initializes a new instance of the <see cref="StripNormalizer"/> class.
        /// </summary>
        /// <param name="left">
        /// If <c>true</c>, removes leading (left-side) whitespace characters. Default is <c>true</c>.
        /// </param>
        /// <param name="right">
        /// If <c>true</c>, removes trailing (right-side) whitespace characters. Default is <c>true</c>.
        /// </param>
        /// <remarks>
        /// Setting both parameters to <c>false</c> will result in no normalization being performed.
        /// </remarks>
        public StripNormalizer(bool left = true, bool right = true)
        {
            m_Left = left;
            m_Right = right;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input)
        {
            if (input.Length == 0)
                return input;

            var (from, to) = (0, input.Length);

            if (m_Left)
            {
                while (from < to && char.IsWhiteSpace(input[from]))
                    from++;
            }

            if (m_Right)
            {
                while (to > from && char.IsWhiteSpace(input[to - 1]))
                    to--;
            }

            if (from == to)
                return string.Empty;

            return input[from..to];
        }
    }
}
