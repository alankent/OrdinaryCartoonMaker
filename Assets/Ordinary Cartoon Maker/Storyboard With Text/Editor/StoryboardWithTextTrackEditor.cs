using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Sequences.Timeline;
using UnityEngine.Timeline;

namespace UnityEditor.Sequences.Timeline
{
    [CustomTimelineEditor(typeof(StoryboardWithTextTrack))]
    public class StoryboardWithTextTrackEditor : TrackEditor
    {
        /// <inheritdoc cref="TrackEditor.GetTrackOptions"/>
        /// <remarks>minimumHeight is larger to better see the thumbnail image</remarks>
        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.minimumHeight = 40;

            return options;
        }
    }
}
