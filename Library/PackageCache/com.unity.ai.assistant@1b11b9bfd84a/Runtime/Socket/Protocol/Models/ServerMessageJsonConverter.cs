using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.Socket.Protocol.Models.FromClient;
using Unity.AI.Assistant.Socket.Protocol.Models.FromServer;

namespace Unity.AI.Assistant.Socket.Protocol.Models
{
    class ServerMessageJsonConverter : JsonConverter<IModel>
    {
        public override void WriteJson(JsonWriter writer, IModel value, JsonSerializer serializer)
        {
        }

        public override IModel ReadJson(
            JsonReader reader,
            Type objectType,
            IModel existingValue,
            bool hasExistingValue,
            JsonSerializer serializer)
        {
            var jsonObject = JObject.Load(reader);

            if (!jsonObject.TryGetValue("$type", out var typeToken))
                throw new JsonSerializationException("Unknown type");

            IModel message = typeToken.Value<string>() switch
            {
                CapabilitiesRequestV1.Type => new CapabilitiesRequestV1(),
                CapabilitiesResponseV1.Type => new CapabilitiesResponseV1(),
                SkillsRequestV1.Type => new SkillsRequestV1(),
                SkillsResponseV1.Type => new SkillsResponseV1(),
                AvailableSkillsV1.Type => new AvailableSkillsV1(),
                ChatRequestV1.Type => new ChatRequestV1(),
                ChatResponseV1.Type => new ChatResponseV1(),
                ChatAcknowledgmentV1.Type => new ChatAcknowledgmentV1(),
                DiscussionInitializationV1.Type => new DiscussionInitializationV1(),
                FunctionCallRequestV1.Type => new FunctionCallRequestV1(),
                FunctionCallResponseV1.Type => new FunctionCallResponseV1(),
                ClientDisconnectV1.Type => new ClientDisconnectV1(),
                ServerDisconnectV1.Type => new ServerDisconnectV1(),
                _ => null,
            };

            if (message == null)
            {
                UnityEngine.Debug.LogWarning($"[Muse] Unknown server message $type: '{typeToken.Value<string>()}'. " +
                    "This usually means the backend was updated with a new message type that this client version does not understand. Skipping.");
                return null;
            }

            serializer.Populate(jsonObject.CreateReader(), message);

            return message;
        }
    }
}
