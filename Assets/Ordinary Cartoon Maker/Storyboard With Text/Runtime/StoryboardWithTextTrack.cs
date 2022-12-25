using System;
using System.ComponentModel;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace UnityEngine.Sequences.Timeline
{
    [Serializable]
    [DisplayName("Storyboard With Text Track")]
    [TrackClipType(typeof(StoryboardWithTextPlayableAsset))]
    [TrackColor(237 / 255f, 126 / 255f, 2 / 255f)] // To ask UX
    public class StoryboardWithTextTrack : TrackAsset
    {
        // To find a better name
        [Tooltip("Clips created in this track will be created with this default duration." +
            "Changing this value with not change the length of pre-existing Clips")]
        [SerializeField] public double defaultFrameDuration = 3;

        [Tooltip("sorting order of the Storyboard Canvas")]
        [SerializeField] public int sortOrder;

        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
        {
            var mixer =  ScriptPlayable<StoryboardWithTextMixerBehaviour>.Create(graph, inputCount);

            mixer.GetBehaviour().canvas.sortingOrder = sortOrder;
            return mixer;
        }

        protected override void OnCreateClip(TimelineClip clip)
        {
            clip.duration = defaultFrameDuration;
            base.OnCreateClip(clip);
        }
    }
}
