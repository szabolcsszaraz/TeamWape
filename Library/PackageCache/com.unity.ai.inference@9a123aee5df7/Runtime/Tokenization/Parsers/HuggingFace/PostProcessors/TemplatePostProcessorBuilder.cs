using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;
using Unity.InferenceEngine.Tokenization.PostProcessors;
using Unity.InferenceEngine.Tokenization.PostProcessors.Templating;
using UnityEngine;

namespace Unity.InferenceEngine.Tokenization.Parsers.HuggingFace.PostProcessors
{
    [HfPostProcessor("TemplateProcessing")]
    class TemplatePostProcessorBuilder : IComponentBuilder<IPostProcessor>
    {
        static IEnumerable<Piece> BuildPieces(JArray data)
        {
            foreach (var entry in data)
            {
                var templatePieceData = entry as JObject;
                if (templatePieceData.ContainsKey("SpecialToken"))
                    yield return BuildSpecialToken(templatePieceData["SpecialToken"] as JObject);
                else if (templatePieceData.ContainsKey("Sequence"))
                    yield return BuildSequence(templatePieceData["Sequence"] as JObject);
            }
        }

        static SpecialToken BuildSpecialToken(JObject data)
        {
            var id = data["id"].Value<string>();
            var typeId = data["type_id"].Value<int>();
            return new(id, typeId);
        }

        static Sequence BuildSequence(JObject data)
        {
            var idString = data["id"].Value<string>().ToLowerInvariant();

            var id = idString.ToLowerInvariant() switch
            {
                "a" => SequenceIdentifier.A,
                "b" => SequenceIdentifier.B,
                _ => throw new ArgumentException($"Unknown sequence id: {idString}")
            };

            var typeId = data["type_id"].Value<int>();

            return new(id, typeId);
        }

        static Template GetTemplate(JArray data)
        {
            var pieces = BuildPieces(data);
            return new(pieces);
        }

        IEnumerable<(string value, int id)> GetSpecialTokens(JObject data)
        {
            return from property in data.Properties()
                select property.Value as JObject
                into tokenJson
                let tokenId = (tokenJson["ids"] as JArray)?.First?.Value<int>() ?? throw new DataException("Cannot get token ID")
                let tokenValue = (tokenJson["tokens"] as JArray)?.First?.Value<string>() ?? throw new DataException("Cannot get token value")
                select (value: tokenValue, id: tokenId);
        }

        public IPostProcessor Build(JToken parameters, HuggingFaceParser parser)
        {
            var sb = new StringBuilder();

            var singleTemplate = GetTemplate(parameters["single"] as JArray);
            var pairTemplate = GetTemplate(parameters["pair"] as JArray);
            var specialTokens = GetSpecialTokens(parameters["special_tokens"] as JObject).ToArray();

            sb.AppendLine($"Single: {singleTemplate}");
            sb.AppendLine($"Pair: {pairTemplate}");
            sb.AppendLine("Special Tokens");
            foreach (var (value, id) in specialTokens)
            {
                sb.AppendLine($"  - {id}: {value}");
            }

            return new TemplatePostProcessor(singleTemplate, pairTemplate, specialTokens);
        }
    }
}
