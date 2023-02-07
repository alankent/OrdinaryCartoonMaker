using UnityEngine;
using UnityEditor.Timeline;
using UnityEngine.Sequences.Timeline;
using UnityEngine.Timeline;

namespace UnityEditor.Sequences.Timeline
{
    [CustomTimelineEditor(typeof(TalkingClip))]
    public class TalkingClipEditor : ClipEditor
    {
        /// <inheritdoc cref="ClipEditor.GetClipOptions"/>
        public override ClipDrawOptions GetClipOptions(TimelineClip clip)
        {
            var options = base.GetClipOptions(clip);

            // set clip colour to green when playhead is on clip
            var director = TimelineEditor.inspectedDirector;
            if (director != null && director.time >= clip.start && director.time <= clip.end)
            {
                options.highlightColor = options.highlightColor * 1.5f;
            }

            return options;
        }

#if false
    public override void OnCreate(TimelineClip clip, TrackAsset track, TimelineClip clonedFrom)
    {
        var talkingTrack = track as TalkingTrackAsset;
        if (talkingTrack != null)
        {
            clip.duration = talkingTrack.defaultFrameDuration;
            // TODO: Maybe we could set the clip length based on amount of text.
            // For now I let user scale length to control duration.
        }
    }
#endif
    }
}