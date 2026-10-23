using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEditor.Timeline
{
    partial class TimelineWindow
    {
        PlayableLookup m_PlayableLookup = new PlayableLookup();

        class PlayableLookup : IPlayableLookup
        {
            const int k_InitialDictionarySize = 10;

            readonly Dictionary<AnimationClip, Playable> m_AnimationClipToPlayable =
                new Dictionary<AnimationClip, Playable>(k_InitialDictionarySize);
            readonly Dictionary<AnimationClip, TimelineClip> m_AnimationClipToTimelineClip =
                new Dictionary<AnimationClip, TimelineClip>(k_InitialDictionarySize);

            public IDictionary<AnimationClip, Playable> animationClipToPlayable => m_AnimationClipToPlayable;
            public IDictionary<AnimationClip, TimelineClip> animationClipToTimelineClip => m_AnimationClipToTimelineClip;

            public void UpdatePlayableLookup(TimelineClip clip, GameObject go, Playable p)
            {
                if (clip == null || go == null || !p.IsValid())
                    return;

                if (clip.curves != null)
                    m_AnimationClipToTimelineClip[clip.curves] = clip;

                UpdatePlayableLookup(clip.GetParentTrack().timelineAsset, clip, go, p);
            }

            public void UpdatePlayableLookup(TrackAsset track, GameObject go, Playable p)
            {
                if (track == null || go == null || !p.IsValid())
                    return;

                UpdatePlayableLookup(track.timelineAsset, track, go, p);
            }

            void UpdatePlayableLookup(TimelineAsset timelineAsset, ICurvesOwner curvesOwner, GameObject go, Playable p)
            {
                var director = go.GetComponent<PlayableDirector>();
                var editingDirector = instance.state.editSequence.director;
                // No Asset mode update
                if (curvesOwner.curves != null && director != null && director == editingDirector &&
                    timelineAsset == instance.state.editSequence.asset)
                {
                    m_AnimationClipToPlayable[curvesOwner.curves] = p;
                }
            }
        }
    }
}
