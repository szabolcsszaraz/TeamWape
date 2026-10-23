using System;
using Newtonsoft.Json.Linq;
using Unity.AI.Assistant.UI.Editor.Scripts.Data;
using Unity.AI.Assistant.UI.Editor.Scripts.Data.MessageBlocks;

namespace Unity.AI.Assistant.UI.Editor.Scripts.Components.Virtualization
{
    /// <summary>
    /// A 64-bit key over a display item. Two flavours are built with it: an identity key, which names
    /// a position in the conversation and survives content changes, and a content key, which folds in
    /// everything that changes rendered height.
    /// </summary>
    readonly struct ContentKey : IEquatable<ContentKey>
    {
        public ContentKey(long value)
        {
            Value = value;
        }

        public long Value { get; }

        public bool Equals(ContentKey other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ContentKey other && Equals(other);

        public override int GetHashCode() => Value.GetHashCode();

        public override string ToString() => Value.ToString("x16");

        public static bool operator ==(ContentKey left, ContentKey right) => left.Equals(right);

        public static bool operator !=(ContentKey left, ContentKey right) => !left.Equals(right);
    }

    /// <summary>
    /// Accumulates a <see cref="ContentKey"/> with FNV-1a. String.GetHashCode is randomized per
    /// process, so a key built from it cannot be persisted across a domain reload.
    /// </summary>
    struct ContentKeyBuilder
    {
        const ulong k_OffsetBasis = 14695981039346656037UL;
        const ulong k_Prime = 1099511628211UL;

        // Long text is sampled rather than hashed whole: a streamed answer is re-keyed on every chunk,
        // and a sampling collision costs one stale cached height, which the real measurement corrects.
        const int k_MaxSampledChars = 64;
        const int k_NullMarker = -1;

        ulong m_Hash;

        ContentKeyBuilder(ulong hash)
        {
            m_Hash = hash;
        }

        public static ContentKeyBuilder Start() => new(k_OffsetBasis);

        public ContentKey Key => new(unchecked((long)m_Hash));

        public ContentKeyBuilder Add(bool value) => Add(value ? 1 : 0);

        public ContentKeyBuilder Add(int value) => Add(unchecked((uint)value));

        public ContentKeyBuilder Add(long value) => Add(unchecked((ulong)value));

        /// <summary>
        /// Hashes a short identifying string in full. Use <see cref="AddText"/> for displayed prose.
        /// </summary>
        public ContentKeyBuilder AddName(string value)
        {
            if (value == null)
                return Add(k_NullMarker);

            Add(value.Length);
            for (var i = 0; i < value.Length; i++)
                MixChar(value[i]);

            return this;
        }

        /// <summary>
        /// Hashes a length plus up to <see cref="k_MaxSampledChars"/> evenly spaced characters,
        /// including the first and the last.
        /// </summary>
        public ContentKeyBuilder AddText(string value)
        {
            if (value == null)
                return Add(k_NullMarker);

            Add(value.Length);

            if (value.Length <= k_MaxSampledChars)
            {
                for (var i = 0; i < value.Length; i++)
                    MixChar(value[i]);

                return this;
            }

            var last = value.Length - 1;
            for (var i = 0; i < k_MaxSampledChars; i++)
                MixChar(value[(int)((long)i * last / (k_MaxSampledChars - 1))]);

            return this;
        }

        /// <summary>
        /// Folds in a JSON payload the way <see cref="AddText"/> folds prose: names and structure in
        /// full, string values sampled, so a tool input holding a whole file stays cheap to re-key.
        /// </summary>
        public ContentKeyBuilder AddJson(JToken token)
        {
            if (token is null)
                return Add(k_NullMarker);

            Add((int)token.Type);

            switch (token)
            {
                case JObject json:
                    Add(json.Count);
                    foreach (var property in json.Properties())
                    {
                        AddName(property.Name);
                        AddJson(property.Value);
                    }

                    return this;
                case JArray array:
                    Add(array.Count);
                    foreach (var item in array)
                        AddJson(item);

                    return this;
                case JValue { Value: null }:
                    return this;
                case JValue { Value: string text }:
                    return AddText(text);
                case JValue { Value: bool flag }:
                    return Add(flag);
                case JValue { Value: long number }:
                    return Add(number);
                default:
                    return AddText(token.ToString());
            }
        }

        /// <summary>
        /// Folds in the message state that changes the height of anything rendered from the message as
        /// a whole, without walking its blocks.
        /// </summary>
        public ContentKeyBuilder AddMessageState(MessageModel message)
            => Add(message.IsComplete)
                .Add(message.Feedback.HasValue)
                .Add(message.HasCheckpoint)
                .Add(message.Timestamp)
                .Add(message.RevertedTimeStamp)
                .Add(message.Context?.Length ?? 0)
                .Add(message.Blocks?.Count ?? 0)
                // The feedback row is offered to a message that ends on an answer, and a block list can
                // replace its last entry without changing how many entries it holds.
                .Add(message.Blocks is { Count: > 0 } blocks && blocks[^1] is AnswerBlockModel);

        /// <summary>
        /// Folds in the height-relevant state of a block model: displayed text, and the flags that
        /// change what the block renders.
        /// </summary>
        public ContentKeyBuilder Add(IMessageBlockModel block)
        {
            switch (block)
            {
                case null:
                    return Add(k_NullMarker);
                case AnswerBlockModel answer:
                    return AddText(answer.Content).Add(answer.IsComplete);
                case ThoughtBlockModel thought:
                    return AddText(thought.Content);
                case ErrorBlockModel error:
                    return AddText(error.Error);
                case InfoBlockModel info:
                    return AddText(info.Message);
                case PromptBlockModel prompt:
                    return AddText(prompt.Content);
                case FunctionCallBlockModel call:
                    return AddName(call.Call.FunctionId)
                        .AddName(call.Call.Agent)
                        .Add(call.Call.Result.IsDone)
                        .Add(call.Call.Result.HasFunctionCallSucceeded);
                case AcpToolCallBlockModel toolCall:
                    // The protocol has one running status, so a call that streams its output to the end
                    // keeps the same IsDone throughout: without the update and the inputs the renderer
                    // draws, a realized slot would be skipped for rebinding until the call finished.
                    return AddName(toolCall.CallInfo?.ToolName)
                        .AddText(toolCall.CallInfo?.Title)
                        .Add(toolCall.IsDone)
                        .Add(toolCall.HasPendingPermission)
                        .Add(toolCall.IsReasoning)
                        .AddText(toolCall.LatestUpdate?.Content)
                        .AddJson(toolCall.CallInfo?.RawInput)
                        .AddJson(toolCall.RawInput);
                case AcpPlanBlockModel plan:
                    return AddPlanEntries(plan);
                default:
                    return AddName(block.GetType().Name);
            }
        }

        ContentKeyBuilder AddPlanEntries(AcpPlanBlockModel plan)
        {
            Add(plan.Entries.Count);
            foreach (var entry in plan.Entries)
            {
                AddText(entry.Content);
                AddName(entry.Status);
            }

            return this;
        }

        ContentKeyBuilder Add(uint value)
        {
            MixByte((byte)value);
            MixByte((byte)(value >> 8));
            MixByte((byte)(value >> 16));
            MixByte((byte)(value >> 24));
            return this;
        }

        ContentKeyBuilder Add(ulong value)
        {
            Add((uint)value);
            Add((uint)(value >> 32));
            return this;
        }

        void MixChar(char value)
        {
            MixByte((byte)value);
            MixByte((byte)(value >> 8));
        }

        void MixByte(byte value)
        {
            m_Hash ^= value;
            m_Hash *= k_Prime;
        }
    }
}
