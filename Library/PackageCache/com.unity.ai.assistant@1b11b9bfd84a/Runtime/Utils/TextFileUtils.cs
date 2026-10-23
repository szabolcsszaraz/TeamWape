using System.IO;
using System.Text;
using UnityEngine;

namespace Unity.AI.Assistant.Utils
{
    internal enum TextFileResult { Valid, Binary, HasBom }

    internal static class TextFileUtils
    {
        static readonly UTF8Encoding k_StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

        // Heuristic to check if a file is binary or not
        public static bool IsTextFile(string path, int sampleSize = 4096)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

                var len = (int)Mathf.Min(fs.Length, sampleSize);

                if (len == 0)
                    return true;

                var buffer = new byte[len];
                var readCount = fs.Read(buffer, 0, len);

                var nonPrintable = 0;
                for (var i = 0; i < readCount; i++)
                {
                    var b = buffer[i];

                    // Count the number of non printable characters
                    // Allow: tab (9), LF (10), CR (13), printable ASCII (32-126)
                    if (b != 9 && b != 10 && b != 13 && (b < 32 || b > 126))
                        nonPrintable++;
                }

                // If more than 5% of characters are non-printable, consider it binary
                var ratio = (float)nonPrintable / len;
                return ratio < 0.05f;
            }
            catch
            {
                return false;
            }
        }

        // Detects a UTF-8 BOM, rejects null bytes and ASCII control characters, and validates
        // UTF-8 sequences for files that fit entirely within the sample window.
        // Full validation of larger files is left to the caller's strict-decoder read.
        public static TextFileResult IsUtf8TextFile(string path, int sampleSize = 4096)
        {
            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);

                var len = (int)Mathf.Min(fs.Length, sampleSize);

                if (len == 0)
                    return TextFileResult.Valid;

                var buffer = new byte[len];
                var readCount = fs.Read(buffer, 0, len);

                // A BOM only changes the classification, not the validation: the bytes after it
                // still go through the NUL/control scan and UTF-8 check below, so a BOM-prefixed
                // binary file is rejected rather than accepted on the strength of its first 3 bytes.
                var hasBom = readCount >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF;
                var offset = hasBom ? 3 : 0;

                for (var i = offset; i < readCount; i++)
                {
                    var b = buffer[i];
                    if (b == 0 || (b < 0x20 && b != 0x09 && b != 0x0A && b != 0x0D))
                        return TextFileResult.Binary;
                }

                // For files that fit entirely in the sample, validate the full byte sequence so
                // truncated multi-byte characters at EOF are caught. Larger files rely on the
                // caller's strict-decoder read for anything beyond this window.
                if (fs.Length <= sampleSize)
                    k_StrictUtf8.GetCharCount(buffer, offset, readCount - offset);

                return hasBom ? TextFileResult.HasBom : TextFileResult.Valid;
            }
            catch
            {
                return TextFileResult.Binary;
            }
        }

        /// <summary>
        /// Validates that the file is UTF-8 text (via <see cref="IsUtf8TextFile"/>) and reads it with
        /// a strict decoder, so corrupt UTF-8 beyond the sampled window surfaces as an error instead
        /// of being silently replaced with U+FFFD. A leading BOM is stripped from the returned string;
        /// whether it is accepted at all is controlled by <paramref name="allowBom"/>. When the caller
        /// truncates the content anyway, pass <paramref name="maxChars"/> to bound the read: at most
        /// maxChars + 1 characters are returned, so the overflow is still detectable.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when the file is not valid UTF-8 text.</exception>
        public static string ReadUtf8TextFile(string path, bool allowBom, int maxChars = int.MaxValue)
        {
            var result = IsUtf8TextFile(path);
            if (result == TextFileResult.HasBom && !allowBom)
                throw new System.InvalidOperationException($"File must be saved as UTF-8 without BOM: {path}");
            // Explicit allowlist so a future TextFileResult member fails closed instead of being
            // silently accepted as text.
            if (result != TextFileResult.Valid && result != TextFileResult.HasBom)
                throw new System.InvalidOperationException($"File does not appear to be a text file: {path}");

            try
            {
                // detectEncodingFromByteOrderMarks is deliberately false: on seeing a UTF-8 BOM,
                // StreamReader swaps in the lenient default UTF-8 encoding, which would silently
                // replace invalid bytes with U+FFFD. With the strict decoder the BOM comes through
                // as U+FEFF and is stripped below instead.
                using var reader = new StreamReader(path, k_StrictUtf8, detectEncodingFromByteOrderMarks: false);

                if (maxChars == int.MaxValue || new FileInfo(path).Length <= maxChars)
                {
                    // UTF-8 never decodes to more chars than bytes, so a file whose byte length
                    // fits the budget can simply be read whole.
                    var content = reader.ReadToEnd();
                    return content.Length > 0 && content[0] == '\uFEFF' ? content.Substring(1) : content;
                }

                // +1 so the caller can detect the overflow, +1 for a possible BOM char.
                var buffer = new char[maxChars + 2];
                var total = 0;
                int read;
                while (total < buffer.Length && (read = reader.ReadBlock(buffer, total, buffer.Length - total)) > 0)
                    total += read;

                var start = total > 0 && buffer[0] == '\uFEFF' ? 1 : 0;
                return new string(buffer, start, System.Math.Min(total - start, maxChars + 1));
            }
            catch (DecoderFallbackException ex)
            {
                // The decoder message names the offending bytes and their index — the only
                // actionable detail for an author locating the corruption — so keep it and the
                // inner exception.
                throw new System.InvalidOperationException($"File contains invalid UTF-8 encoding: {path} ({ex.Message})", ex);
            }
        }
    }
}
