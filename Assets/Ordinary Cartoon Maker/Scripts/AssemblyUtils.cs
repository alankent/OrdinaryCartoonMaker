using Cinemachine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Encoder;
using UnityEditor.Recorder.Timeline;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Sequences.Timeline;
using UnityEngine.Timeline;

namespace OrdinaryCartoonMaker
{
    public class AssemblyUtils
    {
        private const float SIT_OFFSET = 0.4f;

        public enum RecordingTypeEnum { Movie, Still };

#if SPEECH_BUBBLES
        public enum SpeechBubblePositionEnum { TopLeft = 0, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight };

        private static Vector2[] BubblePositions =
        {
            new Vector2(-500, 300),  new Vector2(0, 300),  new Vector2(500, 300),
            new Vector2(-500, 0),    new Vector2(0, 0),    new Vector2(500, 0),
            new Vector2(-500, -300), new Vector2(0, -300), new Vector2(500, -300),
        };
        private static string[] BubbleImages =
        {
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-5.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-6.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-7.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-3.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-6.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-9.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-1.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-12.png",
            "Assets/Ordinary Cartoon Maker/Images/Speech Bubbles/speech-11.png",
        };

        public static void AddSpeechBubbleTrack(Transform shot, string speechBubbleText, bool speechBubbleAutoWrap, SpeechBubblePositionEnum speechBubblePosition)
        {
            var shotTimeline = shot.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;

            // Speech bubble
            var speechTrack = shotTimeline.CreateTrack<StoryboardWithTextTrack>(speechBubbleText.Length > 15 ? speechBubbleText.Substring(0, 12) + "..." : speechBubbleText);
            Debug.Log(speechTrack);
            var speechTimlineClip = speechTrack.CreateClip<StoryboardWithTextPlayableAsset>();
            var speechClip = speechTimlineClip.asset as StoryboardWithTextPlayableAsset;
            speechTimlineClip.start = 1;
            speechTimlineClip.duration = 5;
            speechClip.alpha = 0.9f;
            speechClip.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Ordinary Cartoon Maker/Fonts/CCComicrazy-Roman.ttf");
            speechClip.text = speechBubbleAutoWrap ? LineWrap(speechBubbleText.Trim()) : speechBubbleText;
            speechClip.board = AssetDatabase.LoadAssetAtPath<Texture>(BubbleImages[(int)speechBubblePosition]);
            speechClip.position = BubblePositions[(int)speechBubblePosition];

            EditorUtility.SetDirty(shotTimeline);
            AssetDatabase.SaveAssetIfDirty(shotTimeline);

            // Need to tell Timeline window to refresh! (It does not show the new track otherwise)
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        }

        private static string LineWrap(string text)
        {
            if (text.Length <= 15)
            {
                return text;
            }
            if (text.Length <= 30)
            {
                return ReplaceSpaceNearIndex(text, text.Length / 2);
            }
            if (text.Length <= 60)
            {
                return ReplaceSpaceNearIndex(ReplaceSpaceNearIndex(text, text.Length * 18 / 60), text.Length * 42 / 60);
            }
            return ReplaceSpaceNearIndex(ReplaceSpaceNearIndex(ReplaceSpaceNearIndex(text, text.Length * 18 / 80), text.Length * 40 / 80), text.Length * 58 / 80);
        }

        // Look for a space near the request point and replace it with a newline.
        private static string ReplaceSpaceNearIndex(string text, int index)
        {
            var offset = 0;
            while (offset < index * 2 / 3 && index - offset > 1 && index + offset < text.Length - 2)
            {
                if (text[index - offset] == '\n' || text[index + offset] == '\n')
                {
                    break;
                }
                if (text[index - offset] == ' ')
                {
                    return text.Substring(0, index - offset) + "\n" + text.Substring(index - offset + 1, text.Length - (index - offset + 1));
                }
                if (text[index + offset] == ' ')
                {
                    return text.Substring(0, index + offset) + "\n" + text.Substring(index + offset + 1, text.Length - (index + offset + 1));
                }
                offset++;
            }
            return text;
        }
#endif

        public static TimelineClip AddTalkingTrack(Transform shot, Transform character, bool thinking, string dialog, double start)
        {
            var shotTimeline = shot.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;

            var track = shotTimeline.CreateTrack<TalkingTrackAsset>(dialog.Length > 15 ? dialog.Substring(0, 12) + "..." : dialog);
            shot.GetComponent<PlayableDirector>().SetGenericBinding(track, character.GetComponent<AlansBlendShapeClipVowels>());
            //Debug.Log(speechTrack);

            string characterName = Regex.Replace(character.name, "-.*", "");

            var speechTimlineClip = track.CreateClip<TalkingClip>();
            speechTimlineClip.start = start;
            speechTimlineClip.duration = dialog.Split(' ').Length / 3.0; // We talk at roughly 3 words per minute.
            var speechClip = speechTimlineClip.asset as TalkingClip;
            speechClip.Text = thinking ? $"<i>{dialog}</i>" : dialog;
            speechClip.Speaker = characterName;

            EditorUtility.SetDirty(shotTimeline);
            AssetDatabase.SaveAssetIfDirty(shotTimeline);

            // Need to tell Timeline window to refresh! (It does not show the new track otherwise)
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

            return speechTimlineClip;
        }

        // Find the animation track for the given character (or return null if not found).
        public static AnimationTrack FindAnimationTrackForCharacter(Transform shotSeq, GameObject character)
        {
            var timeline = shotSeq.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            foreach (var track in timeline.GetRootTracks())
            {
                var binding = shotSeq.GetComponent<PlayableDirector>().GetGenericBinding(track) as GameObject;
                if (track is AnimationTrack && binding == character)
                {
                    return track as AnimationTrack;
                }
            }
            return null;
        }

        public static void AddRecorderTrack(string episodeName, string shotCode, Transform shot, RecordingTypeEnum recordingType)
        {
            var shotTimeline = shot.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            foreach (var track in shotTimeline.GetRootTracks())
            {
                if (track.name == "Recorder")
                {
                    // Don't create another one.
                    return;
                }
            }    

            var recorderTrack = shotTimeline.CreateTrack<RecorderTrack>("Recorder");
            var timelineClip = recorderTrack.CreateClip<RecorderClip>();
            timelineClip.blendInDuration = 0;
            timelineClip.blendOutDuration = 0;
            EditorUtility.SetDirty(shotTimeline);
            AssetDatabase.SaveAssetIfDirty(shotTimeline);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

            var recorderClip = timelineClip.asset as RecorderClip;

            if (recordingType == RecordingTypeEnum.Movie)
            {
                timelineClip.start = 2;
                timelineClip.duration = 5;
                recorderClip.name = "WebM Movie Recorder clip";
                var mrs = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                mrs.name = "WebM Movie Recorder Settings";
                var settings = new CoreEncoderSettings
                {
                    Codec = CoreEncoderSettings.OutputCodec.WEBM,
                    EncodingQuality = CoreEncoderSettings.VideoEncodingQuality.High
                };
                mrs.EncoderSettings = settings;
                mrs.FileNameGenerator.FileName = shotCode;
                recorderClip.settings = mrs;
            }
            else
            {
                timelineClip.start = 2;
                timelineClip.duration = 1.0 / (SequencesApi.RootSequence(episodeName).GetComponent<PlayableDirector>().playableAsset as TimelineAsset).editorSettings.frameRate;
                recorderClip.name = "PNG Image Recorder clip";
                var mrs = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                mrs.name = "PNG Image Recorder Settings";
                mrs.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG;
                mrs.FileNameGenerator.FileName = shotCode;
                recorderClip.settings = mrs;
            }

            // Add the settings asset as a child asset of the recorder clip
            AssetDatabase.AddObjectToAsset(recorderClip.settings, recorderClip);
            EditorUtility.SetDirty(recorderClip);
            AssetDatabase.SaveAssetIfDirty(recorderClip);
            TimelineEditor.Refresh(RefreshReason.ContentsModified);
        }

        public static void PositionNonFollowCinemachineCamera(CinemachineVirtualCamera cm, Vector3 fromOffset, bool sitting)
        {
            if (cm.LookAt != null)
            {
                cm.transform.position = cm.LookAt.transform.position + (fromOffset.z * cm.LookAt.transform.forward) + (fromOffset.x * cm.LookAt.transform.right);
                if (sitting)
                {
                    cm.transform.position -= new Vector3(0, SIT_OFFSET, 0);
                }
                cm.transform.rotation = Quaternion.Euler(0f, cm.LookAt.transform.rotation.eulerAngles.y + 180f, 0f);
            }
        }

        // Turn on the "Look At" component on the character, then make it look up/down/left/right.
        public static void CharacterLookInDirection(GameObject character, Vector2 direction)
        {
            var eyesLookAt = character.GetComponent<AlansEyesLookAt>();
            if (eyesLookAt == null)
            {
                eyesLookAt = character.AddComponent<AlansEyesLookAt>();
            }
            eyesLookAt.enabled = true;
            eyesLookAt.horizontal = direction.x;
            eyesLookAt.vertical = direction.y;
            eyesLookAt.headTurnStrength = 1;
            eyesLookAt.headTurnSpeed = 5;
        }

        public static CinemachineComposer CinemachineCameraLookAt(CinemachineVirtualCamera cm, Transform lookAt, bool sitting)
        {
            cm.LookAt = AssemblyUtils.FindEyesStableTarget(lookAt);
            var composer = cm.AddCinemachineComponent<CinemachineComposer>();
            if (sitting)
            {
                composer.m_TrackedObjectOffset.y = -SIT_OFFSET;
            }
            return composer;
        }

        public static CinemachineTransposer CinemachineCameraFollow(CinemachineVirtualCamera cm, Transform follow, Vector3 fromOffset, bool sitting)
        {
            cm.Follow = AssemblyUtils.FindEyesStableTarget(follow);
            var transposer = cm.AddCinemachineComponent<CinemachineTransposer>();
            transposer.m_FollowOffset = fromOffset;
            if (sitting)
            {
                transposer.m_FollowOffset.y -= SIT_OFFSET;
            }
            return transposer;
        }

        public static List<string> CharactersInShot(Transform shot)
        {
            List<string> names = new();
            foreach (Transform child in shot)
            {
                if (child.GetComponent<AlansBlendShapeClipVowels>() != null) // Just using Animator found too many other things
                {
                    names.Add(child.name); // Regex.Replace(child.name, "-.*", "")
                }
            }
            return names;
        }

        public static Transform FindCharacterByName(Transform shot, string characterName)
        {
            // Game objects don't have spaces in names like "Mrs Short" or "Mrs. Short"
            characterName = characterName.Replace(" ", "").Replace(".", "");

            foreach (Transform child in shot)
            {
                if (child.name.StartsWith(characterName) && child.GetComponent<Animator>() != null)
                {
                    return child;
                }
            }
            return null;
        }

        public static GameObject InstantiatePrefab(Transform parent, string path)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var instance = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;

            // Make sure we don't accidentially update the prefab (messing up all other shots)
            //PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

            return instance;
        }

        public static void AlignWithPrefab(GameObject instance, string path)
        {
            // Move cinemachine camera to requested position
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Transform newPos = (prefab == null) ? UnityEditor.SceneView.lastActiveSceneView.camera.transform : (PrefabUtility.InstantiatePrefab(prefab) as GameObject).transform;
            instance.transform.SetPositionAndRotation(newPos.position, newPos.rotation);
        }

        // Start and stop playing, to cause the character to go into the initial pose, which can be
        // used to work out the correct head height for "look at" scripts that need to cope with looking
        // at the face of a sitting character.
        public static void StartAndStopTimeline(Transform shotSeq)
        {
            var director = shotSeq.GetComponent<PlayableDirector>();
            director.time = 0;
            director.Evaluate();
            director.Play();
            director.Stop();
        }

        public static bool AddClipToCharacter(AnimationTrack rootTrack, string clipType, string clipName)
        {
            if (clipName == null || clipName == "")
            {
                return false;
            }

            //Debug.Log($"AddClip: {clipType} - {clipName}");

            // Find the track.
            AnimationTrack track = null;
            if (clipType == "Body")
            {
                track = rootTrack;
            }
            else
            {
                // Generic clips get their own track (e.g. "Backpack On")
                var trackName = (clipType == "Generic") ? clipName : clipType;

                foreach (var childTrack in rootTrack.GetChildTracks())
                {
                    if (childTrack is AnimationTrack && childTrack.name == trackName)
                    {
                        track = childTrack as AnimationTrack;
                        break;
                    }
                }

                // If track not found, create it.
                if (track == null)
                {
                    TimelineAsset timeline = rootTrack.timelineAsset;
                    track = timeline.CreateTrack<AnimationTrack>(rootTrack, trackName);
                    track.applyAvatarMask = true;
                    track.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Ordinary Cartoon Maker/Avatar Masks/" + clipType + " Avatar Mask.mask");
                    //Debug.Log("Created track " + trackName);
                }
            }

            var path = ExtraClips.GetClipPath(clipType, clipName);
            if (path == null)
            {
                Debug.LogError("Animation clip " + clipType + " / " + clipName + " not found.");
                return false;
            }
            var clipInstructions = AssetDatabase.LoadAssetAtPath<AnimationClipInstructions>(path);
            
            if (clipInstructions.avatarMask != null)
            {
                track.avatarMask = clipInstructions.avatarMask;
                track.applyAvatarMask = true;
            }

            double start = 1;
            double duration = 5;
            foreach (var existingClip in track.GetClips())
            {
                if (existingClip.start + existingClip.duration > start)
                {
                    start = existingClip.start + existingClip.duration;
                    duration = 1;
                }
            }

            var timelineClip = track.CreateClip(clipInstructions.clip);
            timelineClip.timeScale = clipInstructions.speedMultiplier;
            timelineClip.start = start;
            timelineClip.duration = duration;

            return true;
        }

        public static void AddRecordedAnimationTrack(AnimationTrack rootTrack, string animationClipPath)
        {
            var name = animationClipPath.Substring(animationClipPath.LastIndexOf('/') + 1);
            name = name.Substring(name.IndexOf("_") + 1);
            name = name.Remove(name.LastIndexOf("_"));

            TimelineAsset timeline = rootTrack.timelineAsset;
            var track = timeline.CreateTrack<AnimationTrack>(rootTrack, name);
            track.applyAvatarMask = true;
            track.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Ordinary Cartoon Maker/Avatar Masks/Upper Body Avatar Mask.mask");

            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(animationClipPath);
            var timelineClip = track.CreateClip(clip);
            timelineClip.start = 2;
            timelineClip.duration = timelineClip.duration;

            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssetIfDirty(timeline);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        }

        public static void CharacterLookAt(GameObject character, Transform targetCharacter)
        {
            var eyesLookAt = character.GetComponent<AlansEyesLookAt>();
            if (eyesLookAt == null)
            {
                eyesLookAt = character.AddComponent<AlansEyesLookAt>();
            }
            eyesLookAt.enabled = true;
            eyesLookAt.headTurnSpeed = 5;
            eyesLookAt.headTurnStrength = 0.7f;
            eyesLookAt.target1 = FindHeadTarget(targetCharacter);
            eyesLookAt.target1Lerp = 1;
        }

        public static Transform FindEyesStableTarget(Transform lookAt)
        {
            // This is hard coded to look for "Root / xxx Eyes Stable Target" - this is not very generic.
            if (lookAt != null)
            {
                return FindNodeWithSuffix(lookAt, "Eyes Stable Target");
            }
            //Debug.Log("Did not find stable eyes target in: " + lookAt.name);
            return lookAt;
        }

        public static Transform FindHeadTarget(Transform lookAt)
        {
            if (lookAt != null)
            {
                return FindNodeWithSuffix(lookAt, "Eyes Target");
            }
            return lookAt;
        }

        public static Transform FindNodeWithSuffix(Transform node, string suffix)
        {
            if (node.name.EndsWith(suffix))
            {
                return node;
            }
            foreach (Transform child in node)
            {
                var n = FindNodeWithSuffix(child, suffix);
                if (n != null)
                {
                    return n;
                }
            }
            return null;
        }

        public static GameObject AddCharacterToShot(Transform shot, string characterSelection)
        {
            var path = TemplateManager.CharacterTemplates.GetTemplatePath(characterSelection);
            if (path == null)
            {
                Debug.LogError("Character selection not found: " + characterSelection);
                return null;
            }
            //Debug.Log(path);
            var instructions = AssetDatabase.LoadAssetAtPath<CharacterInstructions>(path);
            //Debug.Log("Position: " + instructions.transform.position.ToString());

            var prefab = instructions.CharacterPrefab;
            if (prefab == null)
            {
                Debug.LogError("Character Instructions did not specify character.");
                return null;
            }

            var inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            inst.transform.parent = shot;

            // Move object to correct position.
            var instructionsTransform = instructions.GetComponent<Transform>();
            if (instructionsTransform.position == Vector3.zero && instructionsTransform.rotation.eulerAngles == Vector3.zero)
            {
                // 0,0,0 means move it into the scene view looking at camera.
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null)
                {
                    inst.transform.position = sceneView.camera.transform.position + 4f * sceneView.camera.transform.forward;

                    // Default it to look at the scene camera
                    var offset = 180f;
                    inst.transform.rotation = Quaternion.Euler(0f, sceneView.rotation.eulerAngles.y + offset, 0f);

                    // Try to make it land on ground so don't have to get the Y value correct manually
                    // https://answers.unity.com/questions/39203/instantiate-object-and-align-with-object-surface.html
                    RaycastHit hit;
                    var down = new Vector3(0, -1, 0);
                    if (Physics.Raycast(inst.transform.position, down, out hit))
                    {
                        // if too far away, its probably best to leave at middle of scene view
                        var distanceToGround = hit.distance;
                        if (distanceToGround < 3f)
                        {
                            var currentPos = inst.transform.position;
                            var newY = currentPos.y - distanceToGround;
                            inst.transform.position = new Vector3(currentPos.x, newY, currentPos.z);
                        }
                    }
                }
            }
            else
            {
                // If not 0,0,0, then move object to that position.
                inst.transform.position = instructionsTransform.position;
                inst.transform.rotation = instructionsTransform.rotation;
            }

            TimelineAsset timeline = shot.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            if (inst.GetComponent<Animator>() == null)
            {
                Debug.LogError("Character prefab does not have Animator: " + characterSelection);
                return inst;
            }
            AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Body");
            shot.GetComponent<PlayableDirector>().SetGenericBinding(track, inst);
            track.trackOffset = TrackOffset.ApplySceneOffsets;
            track.applyAvatarMask = false;

            if (instructions.BodyClip != null)
            {
                var timelineClip = track.CreateClip(instructions.BodyClip);
                timelineClip.start = 2;
                timelineClip.duration = 5;
            }
            if (instructions.FacialExpressionClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.FacialExpressionClip, "Facial Expression");
            }
            if (instructions.LeftHandClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.LeftHandClip, "Left Hand");
            }
            if (instructions.RightHandClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.RightHandClip, "Right Hand");
            }

            // Select the root track so we can add things to it if needed.
            Selection.SetActiveObjectWithContext(track, null);
            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);

            return inst;
        }

        private static void AddOverrideTrack(TimelineAsset timeline, AnimationTrack parent, AnimationClip clip, string name)
        {
            var overrideTrack = timeline.CreateTrack<AnimationTrack>(parent, name);
            overrideTrack.applyAvatarMask = true;
            overrideTrack.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Ordinary Cartoon Maker/Avatar Masks/" + name + " Avatar Mask.mask");

            var timelineClip = overrideTrack.CreateClip(clip);
            timelineClip.start = 1;
            timelineClip.duration = 5;
        }

        public static void AddCinemachineBrainTrack(Transform shot, GameObject mainCameraGO, List<GameObject> cinemachineCameras)
        {
            TimelineAsset timeline = shot.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            var track = timeline.CreateTrack<CinemachineTrack>();
            shot.GetComponent<PlayableDirector>().SetGenericBinding(track, mainCameraGO.GetComponent<CinemachineBrain>());
            double start = 0;
            foreach (var cm in cinemachineCameras)
            {
                // The clip has start, duration etc, and references a "CinemachineShot" that has the reference to the virtual camera.
                var clip = track.CreateClip<CinemachineShot>();
                clip.start = start;
                clip.duration = 2;
                clip.displayName = cm.name;
                start += clip.duration;

                // The virtual camera reference uses the ExposedReference<> type magic.
                var cmShot = clip.asset as CinemachineShot;
                cmShot.DisplayName = cm.name;
                cmShot.VirtualCamera.exposedName = cm.name; // Unity normally uses a GUID, I think just has to be unique.
                shot.GetComponent<PlayableDirector>().SetReferenceValue(cmShot.VirtualCamera.exposedName, cm.GetComponent<CinemachineVirtualCamera>());
            }
            EditorUtility.SetDirty(timeline);
            AssetDatabase.SaveAssetIfDirty(timeline);
        }
    }
}