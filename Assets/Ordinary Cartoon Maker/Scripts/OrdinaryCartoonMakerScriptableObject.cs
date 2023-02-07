using Cinemachine;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Timeline;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.UIElements;

namespace OrdinaryCartoonMaker
{
    public class OrdinaryCartoonMakerScriptableObject : ScriptableObject
    {
        public string EpisodeNumber = "";
        public string EpisodeTitle = "";
        public float FrameRate = 24;
        public string CreateEpisodeErrorMessage = "";

        public string ScreenplayContents = "";
        public string ParseScreenplayErrorMessage = "";

        public string SceneTemplate;
        public string PartNumber = "";
        public int PartDuration = 100;
        public string CreatePartErrorMessage = "";

        public string ShotNumber = "";
        public AssemblyUtils.RecordingTypeEnum RecordingType = AssemblyUtils.RecordingTypeEnum.Movie;
        public string MainCamera = "";
        public string CinemachineCamera = "";
        public string CameraPosition = "";
        public string CreateShotErrorMessage = "";

#if false
        // Not working reliably (and possibly not needed).
        public string RecordShotErrorMessage = "";
#endif

        public string CharacterSelection = "";
        public string AddCharacterErrorMessage = "";

        public string AnimateCharacter = "";
        public AnimationTrack AnimateRootTrack = null;
        public PlayableDirector AnimatePlayableDirector = null;
        public string Body = "";
        public string UpperBody = "";
        public string Head = "";
        public string Face = "";
        public string LeftHand = "";
        public string RightHand = "";
        public string Generic = "";
        public string Dialog = "";
        public string AnimateErrorMessage = "";

        public string VmcStatus = "Off";

#if SPEECH_TAB
        public string SpeechCharacterSelection = "";
        public string SpeechText = "";
#if SPEECH_BUBBLES
        public bool SpeechBubbleAutoWrap = false;
        public AssemblyUtils.SpeechBubblePositionEnum SpeechBubblePosition = AssemblyUtils.SpeechBubblePositionEnum.TopLeft;
#endif
        public string CreateSpeechErrorMessage = "";

        // Call this when the list of characters in the current shot changes (so dropdowns in UI can be updated).
        private Action<List<string>> characterListUpdater;
#endif

        private bool vmcRunning = false;
        private bool vmcRecording = false;
        private VmcManager vmcManager = new VmcManager();

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
                    var templateScenePath = TemplateManager.SceneTemplates.GetTemplatePath(SceneTemplate);
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
#if SPEECH_TAB
            characterListUpdater(new List<string>());
#endif
        }

        public void StartStopVmcReceiver(Button startStopVmcReceiverButton, Button startStopVmcRecordingButton)
        {
            if (!vmcRunning)
            {
                vmcManager.StartVmcReceiver();
                vmcRunning = true;
                startStopVmcReceiverButton.text = "Stop Receiving VMC Packets";
                startStopVmcRecordingButton.visible = true;
                startStopVmcRecordingButton.text = "Record";
                VmcStatus = "VMC receiver started";
            }
            else
            {
                vmcManager.StopVmcReceiver();
                vmcRunning = false;
                startStopVmcReceiverButton.text = "Start Receiving VMC Packets";
                startStopVmcRecordingButton.visible = false;
                VmcStatus = "VMC receiver stopped";
            }
        }

        public void StopVmcReceiver()
        {
            vmcManager.StopVmcReceiver();
        }

        public void StartStopVmcRecording(Button startStopVmcRecordingButton)
        {
            if (!vmcRecording)
            {
                if (AnimateRootTrack == null)
                {
                    VmcStatus = "Select character to record.";
                }
                else
                {
                    VmcStatus = "Recording started";
                    vmcManager.StartVmcRecording();
                    startStopVmcRecordingButton.text = "Stop Recording";
                    vmcRecording = true;
                }
            }
            else
            {
                var path = vmcManager.StopVmcRecording();
                //Debug.Log("DONE RECORDING: " + path);
                vmcRecording = false;
                startStopVmcRecordingButton.text = "Record";
                VmcStatus = "Recording stopped";

                if (path != null)
                {
                    var filename = path.Substring(path.LastIndexOf('/') + 1);
                    var timestamp = filename.Substring(filename.IndexOf("_"));
                    timestamp = timestamp.Remove(timestamp.LastIndexOf("_"));
                    var shotCode = $"ep{EpisodeNumber}-{PartNumber}-{ShotNumber}";
                    var newPath = "Assets/_LOCAL/Episodes/" + AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle) + "/" + shotCode + timestamp + ".anim";
                    var msg = AssetDatabase.MoveAsset(path, newPath);
                    if (msg != null && msg != "")
                    {
                        Debug.Log("Failed to move " + path + " to " + newPath + " because: " + msg);
                        // Add with old path.
                        AssemblyUtils.AddRecordedAnimationTrack(AnimateRootTrack, path);
                    }
                    else
                    {
                        // Move worked. Add with new path.
                        AssemblyUtils.AddRecordedAnimationTrack(AnimateRootTrack, newPath);
                    }
                }
            }
        }    

#if SPEECH_TAB
        //public void RegisterCharacterListUpdater(Action<List<string>> handler)
        //{
        //    Debug.Log("Registered character-list-updater (characters in the scene)");
        //    characterListUpdater = handler;
        //}
#endif

        public static string AssembleEpisodeTitle(string episodeNumber, string episodeTitle)
        {
            return $"Episode {episodeNumber} - {episodeTitle}";
        }

        public void Update()
        {
            // Need to give VMC a change to record each frame.
            vmcManager.Update();
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

            List<string> characterNames = new();
            if (shotNodeName != "" && shotNodeName != null)
            {
                ShotNumber = shotNodeName;
                characterNames = AssemblyUtils.CharactersInShot(SequencesApi.ShotSequence(episodeNodeName, partNodeName, shotNodeName));
            }
#if SPEECH_TABf
            if (characterListUpdater != null)
            {
                characterListUpdater(characterNames);
            }
#endif
        }

        public void SelectionChange()
        {
            // If a character, find its animation track
            AnimateCharacter = "";
            AnimateRootTrack = null;
            AnimatePlayableDirector = null;

            if (Selection.activeObject is AnimationTrack && Selection.activeContext is PlayableDirector)
            {
                var director = Selection.activeContext as PlayableDirector;
                var character = director.GetGenericBinding(Selection.activeObject) as Animator;
                if (director != null && character != null)
                {
                    AnimateCharacter = character.gameObject.name;
                    AnimateRootTrack = Selection.activeObject as AnimationTrack;
                    AnimatePlayableDirector = Selection.activeContext as PlayableDirector;
                    vmcManager.Model = character.gameObject;
                }
            }

            if (AnimateRootTrack == null && Selection.activeGameObject != null && Selection.activeGameObject.GetComponent<Animator>() != null)
            {
                var shot = Selection.activeGameObject.transform.parent;
                var director = shot.GetComponent<PlayableDirector>();
                var timelineSequence = SequencesApi.GetTimelineSequence(shot);
                if (director != null && timelineSequence != null)
                {
                    var timeline = timelineSequence.timeline;
                    foreach (var track in timeline.GetRootTracks())
                    {
                        var binding = director.GetGenericBinding(track);
                        if (track is AnimationTrack && binding == Selection.activeGameObject)
                        {
                            AnimateCharacter = Selection.activeGameObject.name;
                            AnimateRootTrack = track as AnimationTrack;
                            AnimatePlayableDirector = director;
                            vmcManager.Model = binding as GameObject;
                            break;
                        }
                    }
                }
            }
        }

        // ================== Parse Screenplay ==================

        public void PopulateEpisodeFromScreenplay()
        {
            ParseScreenplayErrorMessage = "";

            if (EpisodeNumber == "" || EpisodeTitle == "")
            {
                ParseScreenplayErrorMessage = "Episode number and title must be supplied";
                return;
            }

            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            ParseScreenplay.ParseAndProcess(episodeName, ScreenplayContents);
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
            var shot = SequencesApi.CreateShotSequence(episodeName, PartNumber, ShotNumber);
            var shotCode = $"ep{EpisodeNumber}-{PartNumber}-{ShotNumber}";

            // Add the recorder track and clip.
            AssemblyUtils.AddRecorderTrack(episodeName, shotCode, shot, RecordingType);

            // Add camera prefab to scene hierarchy
            {
                var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
                Transform shotTransform = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
                if (shotTransform != null)
                {
                    var path = TemplateManager.MainCameraTemplates.GetTemplatePath(MainCamera);
                    if (path != null)
                    {
                        AssemblyUtils.InstantiatePrefab(shotTransform, path);
                    }

                    path = TemplateManager.CinemachineCameraTemplates.GetTemplatePath(CinemachineCamera);
                    if (path != null)
                    {
                        var camera = AssemblyUtils.InstantiatePrefab(shotTransform, path);

                        // Move cinemachine camera to requested position
                        var posPath = TemplateManager.CameraPositionTemplates.GetTemplatePath(CameraPosition);
                        AssemblyUtils.AlignWithPrefab(camera, posPath);
                    }
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
            var shot = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shot == null)
            {
                AddCharacterErrorMessage = "Create the shot sequence first.";
                return;
            }

            AssemblyUtils.AddCharacterToShot(shot, CharacterSelection);
        }

        // ================== Add To Character ==================

        public void AddToCharacter()
        {
            Debug.Log("Animate character");
            AnimateErrorMessage = "";

            if (AnimateRootTrack == null)
            {
                AnimateErrorMessage = "Select a character GameObject to add to";
                return;
            }

            bool added = false;
            added |= AddClipToCharacter(AnimateRootTrack, "Body", ref Body);
            added |= AddClipToCharacter(AnimateRootTrack, "Upper Body", ref UpperBody);
            added |= AddClipToCharacter(AnimateRootTrack, "Face", ref Face);
            added |= AddClipToCharacter(AnimateRootTrack, "Left Hand", ref LeftHand);
            added |= AddClipToCharacter(AnimateRootTrack, "Right Hand", ref RightHand);
            added |= AddClipToCharacter(AnimateRootTrack, "Generic", ref Generic);
            added |= AddDialogTrack(AnimateRootTrack, AnimatePlayableDirector, ref Dialog);
            
            if (!added)
            {
                AnimateErrorMessage = "No clip to add was selected";
            }

            TimelineEditor.Refresh(RefreshReason.ContentsAddedOrRemoved);
        }

        private bool AddClipToCharacter(AnimationTrack rootTrack, string clipType, ref string clipName)
        {
            if (AssemblyUtils.AddClipToCharacter(rootTrack, clipType, clipName))
            {
                clipName = "";
                return true;
            }
            return false;
        }

        private bool AddDialogTrack(AnimationTrack track, PlayableDirector context, ref string dialog)
        {
            if (dialog == null || dialog == "" || track == null || context == null || EpisodeNumber == "" || EpisodeTitle == "" || PartNumber == "" || ShotNumber == "")
            {
                return false;
            }

            var animator = context.GetGenericBinding(track) as Animator;
            var characterName = animator.gameObject.name;

            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            var shot = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shot == null)
            {
                return false;
            }

            Transform character = AssemblyUtils.FindCharacterByName(shot, characterName);
            if (character == null)
            {
                return false;
            }

            AssemblyUtils.AddTalkingTrack(shot, character, false, dialog, 2);
            dialog = "";

            return true;
        }

#if SPEECH_TAB
        // ================== Speech ==================

        public void CreateSpeech()
        {
            //Debug.Log("Create speech: " + SpeechText + ", position: " + SpeechBubblePosition.ToString());
            CreateSpeechErrorMessage = "";

            if (EpisodeNumber == "" || EpisodeTitle == "" || PartNumber == "" || ShotNumber == "")
            {
                CreateSpeechErrorMessage = "First select the shot to add to.";
                return;
            }
            if (SpeechText == "")
            {
                CreateSpeechErrorMessage = "Enter the speech to add.";
                return;
            }

            string episodeName = AssembleEpisodeTitle(EpisodeNumber, EpisodeTitle);
            var shot = SequencesApi.ShotSequence(episodeName, PartNumber, ShotNumber);
            if (shot == null)
            {
                CreateSpeechErrorMessage = "Create the shot sequence first.";
                return;
            }

#if SPEECH_BUBBLES
            // Removed speech bubbles for now, only using captions.
            AssemblyUtils.AddSpeechBubbleTrack(shot, SpeechText, SpeechBubbleAutoWrap, SpeechBubblePosition);
#endif

            Transform character = AssemblyUtils.FindCharacterByName(shot, SpeechCharacterSelection);
            if (character == null)
            {
                CreateSpeechErrorMessage = "Unable to find selected character.";
                return;
            }
            AssemblyUtils.AddTalkingTrack(shot, character, false, SpeechText, 2);
            
        }
#endif
    }
}