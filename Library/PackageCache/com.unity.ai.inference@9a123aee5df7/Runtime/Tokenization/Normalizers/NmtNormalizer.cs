namespace Unity.InferenceEngine.Tokenization.Normalizers
{
    /// <summary>
    /// Provides NMT (Neural Machine Translation) normalization for text preprocessing.
    /// Filters out control characters and normalizes various whitespace and special Unicode
    /// characters to standard spaces.
    /// </summary>
    public class NmtNormalizer : INormalizer
    {
        static bool TryConvert(char c, out char result)
        {
            if (c is
                >= '\x0001' and <= '\x0008' or
                '\x000b' or '\x000c' or
                >= '\x000e' and <= '\x001f' or
                '\x007f' or '\x008f' or '\x009f')
            {
                result = '\0';
                return false;
            }

            var convC = c switch
            {
                '\x0009' => ' ',
                '\x000a' => ' ',
                '\x000c' => ' ',
                '\x000d' => ' ',
                '\x1680' => ' ',
                >= '\x200b' and <= '\x200f' => ' ',
                '\x2028' => ' ',
                '\x2029' => ' ',
                '\x2581' => ' ',
                '\xfeff' => ' ',
                '\xfffd' => ' ',
                _ => c,
            };
            result = convC;
            return true;
        }

        /// <inheritdoc />
        public SubString Normalize(SubString input)
        {
            var vanilla = true;
            var length = input.Length;

            // 1. Check if normalization alters the input.
            for (var i = 0; i < input.Length; i++)
            {
                var c = input[i];
                var keep = TryConvert(c, out var convC);
                vanilla = keep && c == convC && vanilla;
                if (!keep)
                    length--;
            }

            // 2a. Not altered.
            if (vanilla)
                return input;

            // 2b. Altered. Generating the normalized version.
            return string.Create(length, input, (span, src) =>
            {
                var index = 0;
                for (var i = 0; i < src.Length; i++)
                {
                    var c = src[i];
                    if(TryConvert(c, out var converted))
                        span[index++] = converted;
                }
            });
        }
    }
}
