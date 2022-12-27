using Cinemachine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Timeline;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Sequences.Timeline;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace OrdinaryCartoonMaker
{
    public class OrdinaryCartoonMakerScriptableObject : ScriptableObject
    {
        public enum RecordingTypeEnum { Movie, Still };
        public enum SpeechBubblePositionEnum { TopLeft = 0, TopCenter, TopRight, MiddleLeft, MiddleCenter, MiddleRight, BottomLeft, BottomCenter, BottomRight };

        public string EpisodeNumber = "";
        public string EpisodeTitle = "";
        public float FrameRate = 24;
        public string CreateEpisodeErrorMessage = "";

        public string SceneTemplate;
        public string PartNumber = "";
        public int PartDuration = 100;
        public string CreatePartErrorMessage = "";

        public string ShotNumber = "";
        public RecordingTypeEnum RecordingType = RecordingTypeEnum.Movie;
        public string CameraType = "";
        public string CameraPosition = "";
        public string CreateShotErrorMessage = "";
#if false
        // Not working reliably (and possibly not needed).
        public string RecordShotErrorMessage = "";
#endif

        public List<string> CharacterSelectionChoices = new();
        public List<string> CharacterPositionChoices = new();
        public List<string> CharacterAnimationChoices = new();
        public string CharacterSelection = "";
        public string CharacterPosition = "";
        public string CharacterAnimation = "";
        public string AddCharacterErrorMessage = "";

        public string SpeechBubbleText = "";
        public bool SpeechBubbleAutoWrap = false;
        public SpeechBubblePositionEnum SpeechBubblePosition = SpeechBubblePositionEnum.TopLeft;
        public string CreateSpeechBubbleErrorMessage = "";

        // ================== Create Episode ==================

        public void CreateEpisode()
        {
            CreateEpisodeErrorMessage = "";

            //Debug.Log(EpisodeNumber);
            //Debug.Log(EpisodeTitle);

            IdentifyEpisodePartShot(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name, "", "");

            if (EpisodeNumber == "")
            {
                CreateEpisodeErrorMessage = "Episode number is required";
                return;
            }
            if (EpisodeTitle == "")
            {
                CreateEpisodeErrorMessage = "Episode title is required";
                return;
            }

            // Create directory for episode files (will do nothing if already exists)
            var episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            if (!AssetDatabase.IsValidFolder("Assets/_LOCAL"))
            {
                AssetDatabase.CreateFolder("Assets", "_LOCAL");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_LOCAL/Episodes"))
            {
                AssetDatabase.CreateFolder("Assets/_LOCAL", "Episodes");
            }
            if (!AssetDatabase.IsValidFolder("Assets/_LOCAL/Episodes/" + episodeName))
            {
                AssetDatabase.CreateFolder("Assets/_LOCAL/Episodes", episodeName);
            }

            // If we don't have scene open already, try to create it and open it.
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.name != episodeName)
            {
                /*if (currentScene.isDirty)
                {
                    CreateEpisodeErrorMessage = $"Please save or discard changes in current scene first";
                    return;
                }*/

                var scenePath = $"Assets/_LOCAL/Episodes/{episodeName}/{episodeName}.unity";
                if (AssetDatabase.GetMainAssetTypeAtPath(scenePath) == null)
                {
                    // Create it if it does not exist already.
                    // TODO: Could consider using official scene templates, but there was not t:SceneTemplate I could find to search by.
                    // Tuple<Scene, SceneAsset> SceneTemplate.Instantiate(SceneTemplateAsset sceneTemplate, bool loadAdditively, string newSceneOutputPath = null);
                    var templateScenePath = SceneTemplates.GetTemplatePath(SceneTemplate);
                    var newScene = EditorSceneManager.OpenScene(templateScenePath);
                    EditorSceneManager.SaveScene(newScene, scenePath, true);
                }
                currentScene = EditorSceneManager.OpenScene(scenePath);
            }

            // See if master sequence already exists
            //Debug.Log($"Does '{episodeName}' already exist?");
            if (SequencesApi.RootSequence(episodeName) != null)
            {
                CreateEpisodeErrorMessage = "The master sequence already exists.";
                return;
            }

            // Create a new master sequence
            SequencesApi.CreateMasterSequence(episodeName, FrameRate);
            EditorSceneManager.SaveScene(currentScene);
        }

        public static string AssembleEpisodeTitle(string episodeNumber, string episodeTitle)
        {
            return $"Episode {episodeNumber} - {episodeTitle}";
        }

        public void IdentifyEpisodePartShot(string episodeNodeName, string partNodeName, string shotNodeName)
        {
            Regex r = new Regex(@"Episode (\d+) - (.+)");
            Match m = r.Match(episodeNodeName);
            if (m.Success)
            {
                if (EpisodeNumber == "" || EpisodeNumber == null)
                {
                    EpisodeNumber = m.Groups[1].Value;
                }
                if (EpisodeTitle == "" || EpisodeTitle == null)
                {
                    EpisodeTitle = m.Groups[2].Value;
                }
            }

            if (partNodeName != "" && partNodeName != null)
            {
                PartNumber = partNodeName;
            }

            if (shotNodeName != "" && shotNodeName != null)
            {
                ShotNumber = shotNodeName;
            }
        }

        // ================== Create Part ==================

        public void CreatePart()
        {
            CreatePartErrorMessage = "";
            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            Debug.Log("CREATE PART " + PartNumber);

            if (PartNumber == "")
            {
                CreatePartErrorMessage = "Please supply the part number.";
                return;
            }
            if (SequencesApi.RootSequence(episodeName) == null)
            {
                CreatePartErrorMessage = "The master sequence must be created first.";
                return;
            }
            if (SequencesApi.PartSequence(episodeName, PartNumber) != null)
            {
                CreatePartErrorMessage = "The part sequence already exists.";
                return;
            }
            SequencesApi.CreatePartSequence(episodeName, PartNumber, PartDuration);
        }

        // ================== Create Shot ==================

        public void CreateShot()
        {
            //Debug.Log("CREATE SHOT");

            CreateShotErrorMessage = "";
            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
 
            if (ShotNumber == "")
            {
                CreateShotErrorMessage = "Please supply the shot number.";
                return;
            }
            if (SequencesApi.RootSequence(episodeName) == null)
            {
                CreateShotErrorMessage = "The master sequence must be created first.";
                return;
            }
            if (SequencesApi.PartSequence(episodeName, PartNumber) == null)
            {
                CreateShotErrorMessage = "The part sequence must be created first.";
                return;
            }
            if (SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber) != null)
            {
                CreateShotErrorMessage = "The shot sequence already exists.";
                return;
            }
            var shotSeq = SequencesApi.CreateShotSequence(episodeName, PartNumber, ShotNumber);
            var shot = shotSeq.GetTimelineSequence();
            var master = shotSeq.GetMaster();

            // Add recorder track and clip
            {
                var recorderTrack = shot.timeline.CreateTrack<RecorderTrack>("Recorder");
                var timelineClip = recorderTrack.CreateClip<RecorderClip>();
                var recorderClip = timelineClip.asset as RecorderClip;

                if (RecordingType == RecordingTypeEnum.Movie)
                {
                    timelineClip.start = 2;
                    timelineClip.duration = 5;
                    var mrs = ScriptableObject.CreateInstance<MovieRecorderSettings>();
                    mrs.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.WebM; // TODO: THIS IS NOT WORKING!
                    mrs.VideoBitRateMode = VideoBitrateMode.High;
                    mrs.FileNameGenerator.FileName = $"ep{EpisodeNumber}/ep{EpisodeNumber}-{PartNumber}-{ShotNumber}";
                    recorderClip.settings = mrs;
                    Debug.Log(mrs.ToString());
                }
                else
                {
                    timelineClip.start = 2;
                    timelineClip.duration = 1.0 / master.rootSequence.fps;
                    var mrs = ScriptableObject.CreateInstance<ImageRecorderSettings>();
                    mrs.OutputFormat = ImageRecorderSettings.ImageRecorderOutputFormat.PNG; // TODO: I AM NOT SURE THIS IS WORKING (it might be the default)
                    mrs.FileNameGenerator.FileName = $"ep{EpisodeNumber}/ep{EpisodeNumber}-{PartNumber}-{ShotNumber}";
                    recorderClip.settings = mrs;
                }
            }

            // Add camera prefab to scene hierarchy
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Transform shotTransform = null;
                foreach (var rootGO in currentScene.GetRootGameObjects())
                {
                    if (rootGO.name == episodeName)
                    {
                        shotTransform = rootGO.transform.Find(PartNumber).Find(ShotNumber);
                    }
                }

                if (shotTransform != null)
                {
                    var path = CameraTypeTemplates.GetTemplatePath(CameraType);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    var instance = PrefabUtility.InstantiatePrefab(prefab, shotTransform) as GameObject;
                    PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                }
            }

            // Move cinemachine camera to requested position
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Transform shotTransform = null;
                foreach (var rootGO in currentScene.GetRootGameObjects())
                {
                    if (rootGO.name == episodeName)
                    {
                        shotTransform = rootGO.transform.Find(PartNumber).Find(ShotNumber);
                    }
                }

                if (shotTransform != null)
                {
                    var path = CameraPositionTemplates.GetTemplatePath(CameraPosition);
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    Transform newPos = (prefab == null) ? UnityEditor.SceneView.lastActiveSceneView.camera.transform : (PrefabUtility.InstantiatePrefab(prefab) as GameObject).transform;

                    MoveVirtualCamera(shotTransform, newPos);
                }
            }
        }

#if false
        // This is not working reliably (and probably not needed since you can do it through context menu anyway).
        public void RecordShot()
        {
            Debug.Log("Record Shot");

            RecordShotErrorMessage = "";
            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);

            if (EpisodeNumber == "" || EpisodeTitle == "" || PartNumber == "" || ShotNumber == "")
            {
                RecordShotErrorMessage = "Please select shot first.";
                return;
            }
            var shot = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shot == null)
            {
                RecordShotErrorMessage = "Shot sequence not found.";
                return;
            }

            var rs = new RecordSequence();
            UnityEditor.Timeline.Actions.ActionContext context = new();
            var clipToRecord = new List<TimelineClip>();
            clipToRecord.Add(shot.GetTimelineSequence().editorialClip);
            Debug.Log(shot.GetTimelineSequence().name);
            Debug.Log(shot.GetTimelineSequence().editorialClip.displayName);
            context.clips = clipToRecord;
            context.tracks = new List<TrackAsset>();
            if (rs.Validate(context) != UnityEditor.Timeline.Actions.ActionValidity.Valid)
            {
                RecordShotErrorMessage = "Recorder says context not valid.";
                return;
            }
            rs.Execute(context);
        }
#endif

        private void MoveVirtualCamera(Transform node, Transform newPosition)
        {
            if (node.GetComponent<CinemachineVirtualCamera>() != null)
            {
                //Debug.Log("MoveVirtualCamera found camera " + node.name);
                node.transform.SetPositionAndRotation(newPosition.position, newPosition.rotation);
            }
            foreach (Transform child in node)
            {
                MoveVirtualCamera(child, newPosition);
            }
        }

        // ================== Add Character ==================

        public void AddCharacter()
        {
            Debug.Log("Add character");
            AddCharacterErrorMessage = "";

            if (EpisodeNumber == "" || EpisodeTitle == "" || PartNumber == "" || ShotNumber == "")
            {
                AddCharacterErrorMessage = "Select the shot to add to.";
                return;
            }
            if (CharacterSelection == "")
            {
                AddCharacterErrorMessage = "Select the character to add.";
                return;
            }

            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            var shotSeq = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shotSeq == null)
            {
                AddCharacterErrorMessage = "Create the shot sequence first.";
                return;
            }
            var shot = shotSeq.GetTimelineSequence();

            var path = CharacterTemplates.GetTemplatePath(CharacterSelection);
            Debug.Log(path);
            var instructions = AssetDatabase.LoadAssetAtPath<CharacterInstructions>(path);
            Debug.Log("Position: " + instructions.transform.position.ToString());

            var prefab = instructions.CharacterPrefab;
            if (prefab == null)
            {
                CreateSpeechBubbleErrorMessage = "Character instructions did not specify character.";
                return;
            }

            var inst = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            inst.transform.parent = shotSeq.GetTransform();

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

            TimelineAsset timeline = shot.timeline;
            if (inst.GetComponent<Animator>() == null)
            {
                AddCharacterErrorMessage = "Character prefab does not have Animator.";
                return;
            }
            AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, name);
            shotSeq.GetTransform().GetComponent<PlayableDirector>().SetGenericBinding(track, inst);
            track.trackOffset = TrackOffset.ApplySceneOffsets;

            if (instructions.BodyClip == null)
            {
                AddCharacterErrorMessage = "Character instructions body clip not set.";
                return;
            }
            var timelineClip = track.CreateClip(instructions.BodyClip);
            timelineClip.start = 1;
            timelineClip.duration = 5;

            if (instructions.LeftHandClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.LeftHandClip, "Left Hand");
            }
            if (instructions.RightHandClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.RightHandClip, "Right Hand");
            }
            if (instructions.FacialExpressionClip != null)
            {
                AddOverrideTrack(timeline, track, instructions.FacialExpressionClip, "Facial Expression");
            }

            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        }

        private void AddOverrideTrack(TimelineAsset timeline, AnimationTrack parent, AnimationClip clip, string name)
        {
            var overrideTrack = timeline.CreateTrack<AnimationTrack>(parent, name);
            overrideTrack.applyAvatarMask = true;
            overrideTrack.avatarMask = AssetDatabase.LoadAssetAtPath<AvatarMask>("Assets/Ordinary Cartoon Maker/Avatar Masks/" + name + " Avatar Mask.mask");

            var timelineClip = overrideTrack.CreateClip(clip);
            timelineClip.start = 1;
            timelineClip.duration = 5;
        }

        // ================== Speach Bubbles ==================

        private Vector2[] BubblePositions =
        {
            new Vector2(-500, 300),  new Vector2(0, 300),  new Vector2(500, 300),
            new Vector2(-500, 0),  new Vector2(0, 0),  new Vector2(500, 0),
            new Vector2(-500, -300), new Vector2(0, -300), new Vector2(500, -300),
        };
        private string[] BubbleImages =
        {
             // TODO: Different speech bubble types? (thought, ...)
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

        public void CreateSpeechBubble()
        {
            Debug.Log("Create speech bubble: " + SpeechBubbleText + ", position: " + SpeechBubblePosition.ToString());
            CreateSpeechBubbleErrorMessage = "";

            if (EpisodeNumber == "" || EpisodeTitle == "" || PartNumber == "" || ShotNumber == "")
            {
                CreateSpeechBubbleErrorMessage = "First select the shot to add to.";
                return;
            }
            if (SpeechBubbleText == "")
            {
                CreateSpeechBubbleErrorMessage = "Enter the speech to add.";
                return;
            }

            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            var shotSeq = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shotSeq == null)
            {
                CreateSpeechBubbleErrorMessage = "Create the shot sequence first.";
                return;
            }
            var shot = shotSeq.GetTimelineSequence();

            // Speech bubble
            var speechTrack = shot.timeline.CreateTrack<StoryboardWithTextTrack>(SpeechBubbleText.Length > 15 ? SpeechBubbleText.Substring(0, 12) + "..." : SpeechBubbleText);
            Debug.Log(speechTrack);
            var speechTimlineClip = speechTrack.CreateClip<StoryboardWithTextPlayableAsset>();
            var speechClip = speechTimlineClip.asset as StoryboardWithTextPlayableAsset;
            speechTimlineClip.start = 1;
            speechTimlineClip.duration = 5;
            speechClip.alpha = 0.9f;
            speechClip.font = AssetDatabase.LoadAssetAtPath<Font>("Assets/Ordinary Cartoon Maker/Fonts/CCComicrazy-Roman.ttf");
            speechClip.text = SpeechBubbleAutoWrap ? LineWrap(SpeechBubbleText.Trim()) : SpeechBubbleText;
            speechClip.board = AssetDatabase.LoadAssetAtPath<Texture>(BubbleImages[(int)SpeechBubblePosition]);
            speechClip.position = BubblePositions[(int)SpeechBubblePosition];

            EditorUtility.SetDirty(shot.timeline);
            AssetDatabase.SaveAssetIfDirty(shot.timeline);

            // TODO: Need to tell Timeline window to refresh! (It does not show the new track otherwise)
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

    }
}