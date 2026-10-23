using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    internal static class IPlayableLookupExtensions
    {
        public static bool GetPlayableFromAnimClip(this IPlayableLookup lookup, AnimationClip clip, out Playable p)
        {
            if (clip == null)
            {
                p = Playable.Null;
                return false;
            }

            var dict = lookup.animationClipToPlayable;
            if (dict.TryGetValue(clip, out p))
            {
                if (p.IsValid())
                    return true;

                // we want to remove invalid playables so they don't accumulate over time
                dict.Remove(clip);
            }

            p = Playable.Null;
            return false;
        }

        public static TimelineClip GetTimelineClipFromCurves(this IPlayableLookup lookup, AnimationClip clip)
        {
            if (clip == null)
                return null;

            lookup.animationClipToTimelineClip.TryGetValue(clip, out var timelineClip);
            return timelineClip;
        }

        public static void ClearPlayableLookup(this IPlayableLookup lookup)
        {
            lookup.animationClipToPlayable.Clear();
        }
    }
}
