using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    internal interface IPlayableLookup
    {
        IDictionary<AnimationClip, Playable> animationClipToPlayable { get; }
        IDictionary<AnimationClip, TimelineClip> animationClipToTimelineClip { get; }
    }
}
