using System;
using System.Threading.Tasks;
using Unity.AI.Toolkit.Utility;

namespace Unity.AI.Generators.Redux.Toolkit
{
    [Serializable]
    record ApiState
    {
        public SerializableDictionary<ApiCacheKey, EndpointCacheItem> cachedResponses = new();
#if UNITY_6000_6_OR_NEWER
        // Task is never Unity-serializable; mark explicitly to silence UAC1001 on 6000.6+.
        [NonSerialized]
#endif
        public Task refetchOperations = Task.CompletedTask;
    }
}
