using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Unity.AI.Image.Services.Stores.Actions;
using Unity.AI.Generators.Redux;
using UnityEngine.UIElements;

namespace Unity.AI.Image.Services.Stores.Slices
{
    static class DebuggerSlice
    {
        // Some action payloads carry a VisualElement reference for downstream UI work.
        // Walking VisualElement properties via reflection during JSON serialization
        // triggers Matrix4x4 ValidTRS asserts and floods the console — skip those types.
        static readonly JsonSerializerSettings k_JsonSettings = new()
        {
            Formatting = Formatting.Indented,
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
            ContractResolver = new SkipUnityRuntimeContractResolver()
        };

        public static void Create(Store store) => store.CreateSlice(
            DebuggerActions.slice,
            new DebuggerState(),
            reducers => reducers
                .Add(DebuggerActions.setRecording, (state, payload) => state.record = payload)
                .Add(DebuggerActions.init).With((state, payload) => payload with {}),
            extraReducers => extraReducers
                .AddMatcher(action => action.type != AppActions.init.type, (state, action) =>
                {
                    state.info.action = action;
                    state.info.json = JsonConvert.SerializeObject(action, k_JsonSettings);
                    state.info.tick++;
                }),
            state => state with
            {
                info = state.info with {}
            });

        class SkipUnityRuntimeContractResolver : DefaultContractResolver
        {
            protected override JsonProperty CreateProperty(System.Reflection.MemberInfo member, MemberSerialization memberSerialization)
            {
                var property = base.CreateProperty(member, memberSerialization);
                var type = property.PropertyType;
                if (type != null && (typeof(VisualElement).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type)))
                    property.ShouldSerialize = _ => false;
                return property;
            }
        }
    }
}
