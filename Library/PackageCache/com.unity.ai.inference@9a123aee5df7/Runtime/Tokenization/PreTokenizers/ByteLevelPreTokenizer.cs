using System;
using System.Collections.Generic;
using System.Text;

namespace Unity.InferenceEngine.Tokenization.PreTokenizers
{
    /// <summary>
    /// Pre tokenize an input using ByteLevel rules.
    /// </summary>
    public partial class ByteLevelPreTokenizer : IPreTokenizer
    {
        readonly Pool<StringBuilder> m_StringBuilderPool = new(() => new(), sb => sb.Clear());
        readonly Pool<List<SubString>> m_ListOfSubStringPool = new(() => new(), list => list.Clear());
        readonly Pool<List<byte>> m_ListOfBytePool = new(() => new(), list => list.Clear());

        IOneToManyConverter<SubString, SubString> m_InputSplitter;
        IOneToManyConverter<SubString, byte> m_SubStringToBytes;
        IOneToManyConverter<SubString, SubString> m_Utf8CharSplitter;

        /// <summary>
        /// Adds a whitespace at the beginning of the input if it doesn't start with one.
        /// </summary>
        bool m_AddPrefixSpace;

        /// <summary>
        /// Initializes a new instance of the <see cref="ByteLevelPreTokenizer" /> type.
        /// </summary>
        /// <param name="addPrefixSpace">
        /// Adds a whitespace at the beginning of the input if it doesn't start with one.
        /// </param>
        /// <param name="gpt2Regex">
        /// Uses the GPT2 regex to split the input into smaller <see cref="SubString" />s.
        /// </param>
        public ByteLevelPreTokenizer(bool addPrefixSpace = true, bool gpt2Regex = true)
        {
            Init(
                gpt2Regex ? new Gpt2Splitter() : new DefaultSplitter(), Utf8CharSplitter.Instance,
                new OneToManyCachedConverter<SubString, byte>(
                    SubStringToByteConverter.Instance, SubString.k_Comparer), addPrefixSpace);
        }

        internal ByteLevelPreTokenizer(
            IOneToManyConverter<SubString, SubString> splitter,
            IOneToManyConverter<SubString, SubString> stringToUtf8Chars,
            IOneToManyConverter<SubString, byte> stringToBytes,
            bool addPrefixSpace)
        {
            Init(splitter, stringToUtf8Chars, stringToBytes, addPrefixSpace);
        }

        void Init(
            IOneToManyConverter<SubString, SubString> inputSplitter,
            IOneToManyConverter<SubString, SubString> utf8CharsSplitter,
            IOneToManyConverter<SubString, byte> subStringToBytes,
            bool addPrefixSpace)
        {
            m_InputSplitter = inputSplitter;
            m_Utf8CharSplitter = utf8CharsSplitter;
            m_SubStringToBytes = subStringToBytes;

            m_AddPrefixSpace = addPrefixSpace;
        }

        /// <inheritdoc />
        public void PreTokenize(SubString input, Output<SubString> output)
        {
            if (input.IsNull)
                throw new ArgumentNullException(nameof(input));

            if (m_AddPrefixSpace && !input.StartsWith(" "))
                input = $" {input}";

            using var splitOutputHandle = m_ListOfSubStringPool.Get(out var splits);
            m_InputSplitter.Convert(input, splits.AsOutput());

            for (int sI = 0, sLimit = splits.Count; sI < sLimit; sI++)
            {
                var split = splits[sI];
                using var utfCharHandle = m_ListOfSubStringPool.Get(out var utfChars);
                m_Utf8CharSplitter.Convert(split, utfChars.AsOutput());

                using var byteHandle = m_ListOfBytePool.Get(out var byteOutput);
                for (int cI = 0, cLimit = utfChars.Count; cI < cLimit; cI++)
                {
                    var utfChar = utfChars[cI];
                    m_SubStringToBytes.Convert(utfChar, byteOutput.AsOutput());
                }

                using var _ = m_StringBuilderPool.Get(out var builder);
                for (int bI = 0, bLimit = byteOutput.Count; bI < bLimit; bI++)
                {
                    var b = byteOutput[bI];
                    builder.Append(ByteLevelHelper.BytesChars[b]);
                }

                output.Add(builder.ToString());
            }
        }
    }
}
