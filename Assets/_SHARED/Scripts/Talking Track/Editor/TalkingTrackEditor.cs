using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Timeline;

namespace UnityEditor.Sequences.Timeline
{
    [CustomTimelineEditor(typeof(TalkingTrackAsset))]
    public class TalkingTrackEditor : TrackEditor
    {
        public override TrackDrawOptions GetTrackOptions(TrackAsset track, Object binding)
        {
            var options = base.GetTrackOptions(track, binding);
            options.icon = (Texture2D)AssetDatabase.LoadAssetAtPath("Assets/_SHARED/Scripts/Talking Track/Editor/TalkingTrackIcon.png", typeof(Texture2D));
            return options;
        }
    }
}


