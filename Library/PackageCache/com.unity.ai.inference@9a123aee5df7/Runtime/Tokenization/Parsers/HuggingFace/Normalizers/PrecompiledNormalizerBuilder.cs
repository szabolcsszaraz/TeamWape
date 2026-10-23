using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.Normalizers;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.Normalizers
{
    [HfNormalizer("Precompiled")]
    class PrecompiledNormalizerBuilder : IComponentBuilder<INormalizer>
    {
        const string k_KeyPrecompiledCharsmap = "precompiled_charsmap";

        static bool TryParse(ref ReadOnlySpan<byte> precompiledCharsmap, out List<ulong> trieBlob)
        {
            trieBlob = null;

            if (precompiledCharsmap.Length < 4)
                return false;

            // First u32 is trie_size (little-endian)
            var trieSize = BinaryPrimitives.ReadUInt32LittleEndian(precompiledCharsmap);
            precompiledCharsmap = precompiledCharsmap[4..];

            // The Rust code: trie_char_size = trie_size / 4; then for _ in 0..trie_char_size read le_u32.
            var trieCharSize = trieSize / 4;
            if (precompiledCharsmap.Length < trieCharSize * 4)
                return false;

            trieBlob = new();
            for (var i = 0; i < trieCharSize; i++)
            {
                var value = BinaryPrimitives.ReadUInt32LittleEndian(precompiledCharsmap);
                precompiledCharsmap = precompiledCharsmap[4..];
                trieBlob.Add(value);
            }

            return true;
        }

        public INormalizer Build(JToken parameters, HuggingFaceParser parser)
        {
            var b64 = parameters.GetString(k_KeyPrecompiledCharsmap);
            var bytes = Convert.FromBase64String(b64);

            ReadOnlySpan<byte> precompiledCharsmapSpan = bytes.AsSpan();

            if (!TryParse(ref precompiledCharsmapSpan, out var trieBlob))
                throw new("Cannot parse");

            return new PrecompiledNormalizer(trieBlob, precompiledCharsmapSpan);
        }
    }
}
