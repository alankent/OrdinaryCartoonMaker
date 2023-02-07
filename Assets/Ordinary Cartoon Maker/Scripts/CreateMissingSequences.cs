#if false
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{
    public class CreateMissingSequences
    {
        public static void ParseScreenplay(string episodeName, string script)
        {
            Debug.Log("Starting making missing sequences...");
            var stanzas = ParseScreenplay()
            var masterSequence = SequencesApi.RootSequence(episodeName);
            if (masterSequence == null)
            {
                masterSequence = SequencesApi.CreateMasterSequence(episodeName, 24);
            }
            foreach (var stanza in stanzas)
            {
                string partNumber = stanza.partNumber;
                string shotNumber = stanza.shotNumber;
                string shotCode = "ep" + stanza.episodeNumber + "-" + stanza.partNumber + "-" + stanza.shotNumber;

                var partSeq = SequencesApi.PartSequence(episodeName, partNumber);
                if (partSeq == null)
                {
                    Debug.Log("Creating part " + partNumber);
                    partSeq = SequencesApi.CreatePartSequence(episodeName, partNumber, 10);
                }

                var shotSeq = SequencesApi.ShotSequence(episodeName, partNumber, shotNumber);
                if (shotSeq == null)
                {
                    Debug.Log("- Creating shot " + shotNumber);
                    shotSeq = SequencesApi.CreateShotSequence(episodeName, partNumber, shotNumber);

                    // Add the recorder track and clip.
                    AssemblyUtils.AddRecorderTrack(episodeName, shotCode, shotSeq, AssemblyUtils.RecordingTypeEnum.Movie);

                    var timelineSequence = SequencesApi.GetTimelineSequence(shotSeq);

#if false
                    // Progress bar track
                    {
                        var progressTrack = shot.timeline.CreateTrack<ProgressTrackAsset>("Progress");
                        var timelineClip = progressTrack.CreateClip<ProgressClip>();
                        timelineClip.start = 1;
                        timelineClip.duration = 5;
                        var progressClip = timelineClip.asset as ProgressClip;
                        progressClip.VideoFilename = shotCode;
                    }
#endif
                    // Add characters to scene before dialog, or else dialog won't find characters.
                    bool foundMainCamera = false;
                    bool foundCinemachineCamera = false;
                    GameObject mainCameraGO = null;
                    List<GameObject> cinemachineCameras = new();
                    int cmNumber = 1;

                    foreach (var directive in stanza.directives)
                    {
                        if (directive.target == "shot")
                        {
                            // Main camera
                            mainCameraGO = TemplateDirectiveArgument(shotSeq, directive, "camera", TemplateManager.MainCameraTemplates);
                            if (mainCameraGO != null)
                            {
                                foundMainCamera = true;
                            }
                            TemplateDirectiveArgument(shotSeq, directive, "light", TemplateManager.LightTemplates);
                            TemplateDirectiveArgument(shotSeq, directive, "wind", TemplateManager.WindTemplates);
                            TemplateDirectiveArgument(shotSeq, directive, "cloud", TemplateManager.CloudTemplates);
                            ReportUnprocessedDirectiveArguments(directive);
                        }
                        else if (directive.target == "cm")
                        {
                            // Cinemachine camera
                            var camera = TemplateDirectiveArgument(shotSeq, directive, "camera", TemplateManager.CinemachineCameraTemplates);
                            if (camera == null)
                            {
                                var path = TemplateManager.CinemachineCameraTemplates.GetTemplatePath("CM Camera");
                                if (path != null)
                                {
                                    camera = AssemblyUtils.InstantiatePrefab(shotSeq, path);
                                }
                            }
                            if (camera == null)
                            {
                                Debug.Log("Failed to determine camera for CM.");
                            }
                            else
                            {
                                camera.name = camera.name + " " + cmNumber++;
                                cinemachineCameras.Add(camera);

                                var cm = camera.GetComponent<CinemachineVirtualCamera>();
                                foundCinemachineCamera = true;

                                var direction = ParseDirection(directive, "look");
                                camera.transform.rotation = Quaternion.Euler(0, direction, 0);

                                Transform lookAt = ParseCharacter(directive, "lookAt", shotSeq);
                                if (lookAt != null)
                                {
                                    AssemblyUtils.CinemachineCameraLookAt(cm, lookAt);
                                }

                                var follow = ParseCharacter(directive, "follow", shotSeq);
                                if (follow != null)
                                {
                                    AssemblyUtils.CinemachineCameraFollow(cm, follow, ParseFrom(directive, "from"));
                                    if (lookAt == null)
                                    {
                                        AssemblyUtils.CinemachineCameraLookAt(cm, follow);
                                    }
                                }
                                else
                                {
                                    float cameraDistance = ParseShotFraming(directive, "frame");
                                    //cm.transform.position = cm.LookAt.transform.position + cameraDistance * cm.LookAt.transform.forward;
                                    Vector3 fromOffset = ParseFrom(directive, "from");
                                    cm.transform.position = cm.LookAt.transform.position + fromOffset.z * cm.LookAt.transform.forward + fromOffset.x * cm.LookAt.transform.right;
                                    cm.transform.rotation = Quaternion.Euler(0f, cm.LookAt.transform.rotation.eulerAngles.y + 180f, 0f);
                                }
                            }
                        }
                        else
                        {
                            // Character
                            var character = AssemblyUtils.AddCharacterToShot(shotSeq, directive.target);
                            if (character == null)
                            {
                                Debug.Log("Failed to find character referenced by directive: " + directive.target);
                            }
                            else
                            {
                                var timeline = shotSeq.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
                                AnimationTrack animationTrack = null;
                                foreach (var track in timeline.GetRootTracks())
                                {
                                    var binding = shotSeq.GetComponent<PlayableDirector>().GetGenericBinding(track) as GameObject;
                                    if (track is AnimationTrack && binding == character)
                                    {
                                        animationTrack = track as AnimationTrack;
                                        break;
                                    }
                                }

                                if (animationTrack != null)
                                {
                                    AddClipsToCharacter(directive, "body", "Body", animationTrack);
                                    AddClipsToCharacter(directive, "upper", "Upper Body", animationTrack);
                                    AddClipsToCharacter(directive, "rh", "Right Hand", animationTrack);
                                    AddClipsToCharacter(directive, "lh", "Left Hand", animationTrack);
                                    AddClipsToCharacter(directive, "face", "Left Hand", animationTrack);
                                    AddClipsToCharacter(directive, "clip", "Generic", animationTrack);

                                    // look:up/down/left/right/.../12oclock
                                    var direction = ParseUpDownLeftRight(directive, "look");
                                    if (direction != Vector2.zero)
                                    {
                                        var eyesLookAt = character.GetComponent<AlansEyesLookAt>();
                                        if (eyesLookAt == null)
                                        {
                                            eyesLookAt = character.AddComponent<AlansEyesLookAt>();
                                        }
                                        eyesLookAt.enabled = true;
                                        eyesLookAt.horizontal = direction.x;
                                        eyesLookAt.vertical = direction.y;
                                        eyesLookAt.headTurnSpeed = 1;
                                        eyesLookAt.headTurnSpeed = 0.5f;
                                    }

                                    // lookAt:Hank
                                    var lookAt = ParseCharacter(directive, "lookAt", shotSeq);
                                    if (lookAt != null)
                                    {
                                        AssemblyUtils.LookAt(character, lookAt);
                                    }
                                }
                            }
                        }

                        ReportUnprocessedDirectiveArguments(directive);
                    }

                    if (!foundMainCamera)
                    {
                        if (TemplateManager.MainCameraTemplates.AvailableTemplates().Count == 0)
                        {
                            Debug.Log("Cannot default main camera as there are no prefabs defined.");
                        }
                        else
                        {
                            var template = TemplateManager.MainCameraTemplates.AvailableTemplates()[0];
                            var path = TemplateManager.MainCameraTemplates.GetTemplatePath(template);
                            mainCameraGO = AssemblyUtils.InstantiatePrefab(shotSeq, path);
                        }
                    }

                    if (!foundCinemachineCamera)
                    {
                        if (TemplateManager.CinemachineCameraTemplates.AvailableTemplates().Count == 0)
                        {
                            Debug.Log("Cannot default cinemachine camera as there are no prefabs defined.");
                        }
                        else
                        {
                            var template = TemplateManager.CinemachineCameraTemplates.AvailableTemplates()[0];
                            var path = TemplateManager.CinemachineCameraTemplates.GetTemplatePath(template);
                            AssemblyUtils.InstantiatePrefab(shotSeq, path);
                        }
                    }

                    if (mainCameraGO != null && cinemachineCameras.Count > 1)
                    {
                        // Add a CinemachineTrack pointing to the CM cameras.
                        AssemblyUtils.AddCinemachineBrainTrack(shotSeq, mainCameraGO, cinemachineCameras);
                    }

                    // Dialog.
                    foreach (var line in stanza.lines)
                    {
                        var character = AssemblyUtils.FindCharacterByName(shotSeq, line.speaker);
                        if (character != null)
                        {
                            AssemblyUtils.AddTalkingTrack(shotSeq, character, line.dialog);
                        }
                    }

#if false
                    // Add cameras at end so can look-at characters in the shot.
                    {
                        // First, we need to find the game object for the Shot sequence to add the cameras under.
                        // Look for master sequence game object at the root of the current scene.
                        var rootObjects = UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects();
                        var msGO = rootObjects.FirstOrDefault(go => go.GetComponent<SequenceFilter>() != null);
                        if (msGO != null)
                        {
                            var mainCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_SHARED/Assembly Prefabs/Photography/MainCamera.prefab");
                            if (mainCameraPrefab != null)
                            {
                                var inst = PrefabUtility.InstantiatePrefab(mainCameraPrefab) as GameObject;
                                inst.transform.parent = shotSeq;
                            }

                            var virtualCameraPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_SHARED/Assembly Prefabs/Photography/CM vcam static.prefab");
                            if (virtualCameraPrefab != null)
                            {
                                var inst = PrefabUtility.InstantiatePrefab(virtualCameraPrefab) as GameObject;
                                inst.transform.parent = shotSeq;
                            }
                        }
                    }
#endif

                    EditorUtility.SetDirty(timelineSequence.timeline);
                    AssetDatabase.SaveAssetIfDirty(timelineSequence.timeline);
                }
            }

            Debug.Log("Done making missing sequences...");
        }

    }
}
#endif