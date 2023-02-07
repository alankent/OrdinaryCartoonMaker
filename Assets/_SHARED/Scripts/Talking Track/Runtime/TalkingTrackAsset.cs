using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;
using UnityEngine.Timeline;

[Serializable]
[DisplayName("Talking Mouth Animation Track")]
[TrackColor(2 / 255f, 126 / 255f, 234 / 255f)] // To ask UX
[TrackClipType(typeof(TalkingClip))]
[TrackBindingType(typeof(AlansBlendShapeClipVowels))]
public class TalkingTrackAsset : TrackAsset
{
}
