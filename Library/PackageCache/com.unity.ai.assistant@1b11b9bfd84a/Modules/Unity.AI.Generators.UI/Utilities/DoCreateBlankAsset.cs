using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Unity.AI.Generators.UI.Utilities
{
#if UNITY_6000_5_OR_NEWER
    class DoCreateBlankAsset : AssetCreationEndAction
    {
        public delegate void ActionHandler(long instanceId, string pathName, string resourceFile);

        public ActionHandler action { get; set; }

        public override void Action(EntityId entityId, string pathName, string resourceFile)
        {
            action?.Invoke((long)EntityId.ToULong(entityId), pathName, resourceFile);
        }
    }
#else
    class DoCreateBlankAsset : EndNameEditAction
    {
        public delegate void ActionHandler(long instanceId, string pathName, string resourceFile);

        public ActionHandler action { get; set; }

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            action?.Invoke(instanceId, pathName, resourceFile);
        }
    }
#endif
}
