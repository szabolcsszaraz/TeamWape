using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    static class CurvesOwnerInspectorHelper
    {
        readonly struct AnimationPreviewWrite
        {
            public readonly ObjectId target;
            public readonly int dirtyCount;
            public readonly List<string> writtenPaths;

            public AnimationPreviewWrite(ObjectId target, int dirtyCount, List<string> writtenPaths)
            {
                this.target = target;
                this.dirtyCount = dirtyCount;
                this.writtenPaths = writtenPaths;
            }
        }


        //Cache for preview mode writes, used to figure out if we should rebuild the graph or not
        //Pops oldest write once it reaches it's max entries capacity (64), internal nodes are reused so allocation only happen up till max capacity
        class AnimationPreviewWriteCache
        {
            const int k_MaxTrackedTargets = 64;

            readonly Dictionary<ObjectId, LinkedListNode<AnimationPreviewWrite>> m_TargetWrites = new();
            readonly LinkedList<AnimationPreviewWrite> m_LastAccessList = new();

            public AnimationPreviewWriteCache()
            {
                m_TargetWrites.EnsureCapacity(k_MaxTrackedTargets);
            }

            public bool TryGetValue(UnityEngine.Object obj, out AnimationPreviewWrite write)
            {
                if (m_TargetWrites.TryGetValue(obj.GetObjectId(), out var node))
                {
                    write = node.Value;
                    return true;
                }

                write = default;
                return false;
            }

            public void Add(UnityEngine.Object target, List<string> writtenPaths)
            {
                var id = target.GetObjectId();
                var newWrite = new AnimationPreviewWrite(id, EditorUtility.GetDirtyCount(target), writtenPaths);

                if (m_TargetWrites.TryGetValue(id, out var node))
                {
                    //pop the node and change it's payload then re-add it to the list
                    m_LastAccessList.Remove(node);
                    node.Value = newWrite;
                    m_LastAccessList.AddLast(node);

                }
                else //new entry
                {
                    LinkedListNode<AnimationPreviewWrite> newNode = null;
                    if (m_TargetWrites.Count == k_MaxTrackedTargets)
                    {
                        var oldestNode = m_LastAccessList.First;
                        m_TargetWrites.Remove(oldestNode.Value.target);
                        m_LastAccessList.Remove(oldestNode);

                        //update the payload so we can recycle the node
                        oldestNode.Value = newWrite;
                        newNode = oldestNode;
                    }
                    else
                    {
                        newNode = new LinkedListNode<AnimationPreviewWrite>(newWrite);
                    }

                    m_TargetWrites[id] = newNode;
                    m_LastAccessList.AddLast(newNode);
                }
            }

            public void Remove(ObjectId target)
            {
                if (m_TargetWrites.Remove(target, out var node))
                    m_LastAccessList.Remove(node);
            }
        }

        static readonly AnimationPreviewWriteCache s_PreviewsCache = new();

        // Because what is animated is not the asset, but the instanced playable,
        // we apply the animation clip here to preview what is being shown
        // This could be improved doing something more inline with animation mode,
        // and reverting values that aren't be recorded later to avoid dirtying the asset
        public static void PreparePlayableAsset(ICurvesOwnerInspectorWrapper wrapper)
        {
            if (wrapper.serializedPlayableAsset == null)
                return;

            var curvesOwner = wrapper.curvesOwner;
            if (curvesOwner == null || curvesOwner.curves == null)
                return;

            var timelineWindow = TimelineWindow.instance;
            if (timelineWindow == null || timelineWindow.state == null)
                return;

            // requires preview mode. reset the eval time so previous value is correct value is displayed while toggling
            if (!timelineWindow.state.previewMode)
            {
                wrapper.lastEvalTime = -1;
                ClearAnimationPreviewWrite(wrapper.serializedPlayableAsset.targetObject);
                return;
            }

            var time = wrapper.ToLocalTime(timelineWindow.state.editSequence.time);

            // detect if the time has changed, or if the curves have changed
            if (Math.Abs(wrapper.lastEvalTime - time) < TimeUtility.kTimeEpsilon)
            {
                int curveVersion = AnimationClipCurveCache.Instance.GetCurveInfo(curvesOwner.curves).version;
                if (curveVersion == wrapper.lastCurveVersion)
                    return;

                wrapper.lastCurveVersion = curveVersion;
            }

            wrapper.lastEvalTime = time;

            var clipInfo = AnimationClipCurveCache.Instance.GetCurveInfo(curvesOwner.curves);
            int count = clipInfo.bindings.Length;
            if (count == 0)
                return;

            wrapper.serializedPlayableAsset.Update();

            var writtenPaths = new List<string>();
            var prop = wrapper.serializedPlayableAsset.GetIterator();
            while (prop.NextVisible(true))
            {
                if (curvesOwner.IsParameterAnimated(prop.propertyPath))
                {
                    var curve = curvesOwner.GetAnimatedParameter(prop.propertyPath);
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Boolean:
                            prop.boolValue = curve.Evaluate((float)time) > 0;
                            writtenPaths.Add(prop.propertyPath);
                            break;
                        case SerializedPropertyType.Float:
                            prop.floatValue = curve.Evaluate((float)time);
                            writtenPaths.Add(prop.propertyPath);
                            break;
                        case SerializedPropertyType.Integer:
                            prop.intValue = Mathf.FloorToInt(curve.Evaluate((float)time));
                            writtenPaths.Add(prop.propertyPath);
                            break;
                        case SerializedPropertyType.Color:
                            SetAnimatedValue(curvesOwner, prop, "r", time, writtenPaths);
                            SetAnimatedValue(curvesOwner, prop, "g", time, writtenPaths);
                            SetAnimatedValue(curvesOwner, prop, "b", time, writtenPaths);
                            SetAnimatedValue(curvesOwner, prop, "a", time, writtenPaths);
                            break;
                        case SerializedPropertyType.Quaternion:
                        case SerializedPropertyType.Vector4:
                            SetAnimatedValue(curvesOwner, prop, "w", time, writtenPaths);
                            goto case SerializedPropertyType.Vector3;
                        case SerializedPropertyType.Vector3:
                            SetAnimatedValue(curvesOwner, prop, "z", time, writtenPaths);
                            goto case SerializedPropertyType.Vector2;
                        case SerializedPropertyType.Vector2:
                            SetAnimatedValue(curvesOwner, prop, "x", time, writtenPaths);
                            SetAnimatedValue(curvesOwner, prop, "y", time, writtenPaths);
                            break;
                    }
                }
            }

            if (wrapper.serializedPlayableAsset.ApplyModifiedPropertiesWithoutUndo())
                RecordAnimationPreviewWrite(wrapper.serializedPlayableAsset.targetObject, writtenPaths);
        }

        internal static bool IsChangeFromAnimationPreview(SerializedProperty property)
        {
            bool isChangeFromPreview = false;

            if (property == null || property.serializedObject == null)
                return isChangeFromPreview;

            var targets = property.serializedObject.targetObjects;
            if (targets == null || targets.Length == 0)
                return isChangeFromPreview;

            foreach (var target in targets)
            {
                if (target == null || !s_PreviewsCache.TryGetValue(target, out var lastWrite))
                    continue;

                //Theoretically a user edit after saving could bypass this refresh
                //due to PersistentManager clearing dirty flag when saving to disk
                if (lastWrite.dirtyCount != EditorUtility.GetDirtyCount(target))
                    continue;

                if (!ContainsRelatedPath(lastWrite.writtenPaths, property.propertyPath))
                    continue;

                isChangeFromPreview = true;
            }

            return isChangeFromPreview;
        }

        static void RecordAnimationPreviewWrite(UnityEngine.Object target, List<string> writtenPaths)
        {
            if (target == null || writtenPaths.Count == 0)
                return;

            s_PreviewsCache.Add(target, writtenPaths);
        }

        static void ClearAnimationPreviewWrite(UnityEngine.Object target)
        {
            if (ReferenceEquals(target, null))
                return;

            s_PreviewsCache.Remove(target.GetObjectId());
        }

        static bool ContainsRelatedPath(List<string> writtenPaths, string propertyPath)
        {
            foreach (var writtenPath in writtenPaths)
            {
                if (writtenPath == propertyPath ||
                    AnimatedParameterUtility.IsPropertyPathNestedUnder(propertyPath, writtenPath) ||
                    AnimatedParameterUtility.IsPropertyPathNestedUnder(writtenPath, propertyPath))
                    return true;
            }

            return false;
        }

        static void SetAnimatedValue(ICurvesOwner clip, SerializedProperty property, string path, double localTime, List<string> writtenPaths)
        {
            var prop = property.FindPropertyRelative(path);
            if (prop != null)
            {
                var curve = clip.GetAnimatedParameter(prop.propertyPath);
                if (curve != null)
                {
                    prop.floatValue = curve.Evaluate((float)localTime);
                    writtenPaths.Add(prop.propertyPath);
                }
            }
        }
    }
}
