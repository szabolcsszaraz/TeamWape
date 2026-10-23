#if SENTIS_ANALYTICS_ENABLED
using System;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.InferenceEngine.Editor.Visualizer.Editor.Analytics
{
    [AnalyticInfo(eventName: k_EventName, vendorKey: k_VendorKey, version: 1)]
    internal class ModelVisualizerAnalytics : IAnalytic
    {
        const string k_EventName = "sentisModelVisualizerOpen";
        const string k_VendorKey = "unity.sentis";

        [Serializable]
        internal class OpenData : IAnalytic.IData
        {
            /// <summary>
            /// The Unity asset GUID of the model being visualized.
            /// </summary>
            public string assetGuid;
        }

        internal OpenData openData;

        public bool TryGatherData(out IAnalytic.IData data, out Exception error)
        {
            error = null;
            data = openData;
            return data != null;
        }

        public static void SendOpenEvent(string assetGuid)
        {
            var analytic = new ModelVisualizerAnalytics
            {
                openData = new OpenData { assetGuid = assetGuid }
            };
            EditorAnalytics.SendAnalytic(analytic);
        }
    }
}
#endif
