using Cinemachine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;


/*
 * Grammar of screenplay.
 * In simple terms, skip everything until we see a shot-id-line ([1-10-100]...),
 * then following lines are part of that shot (dialog) until the next shot-id
 * or start of a section (heading, location, or similar).`
 * 
 * screenplay = other-line* { section-divider other-line* | shot-id-line other-line* directive-line* other-line* { speaker-line speaker-mood-line? dialog-line* } }
 * 
 * section-divider = heading-line | location-line | music-line
 * 
 * heading-line = "#" text
 * 
 * location-line = ("INT."|"EXT.") text
 * 
 * music-line = "MUSIC:" text
 * 
 * shot-id-line = "[" episode-number "-" part-number "-" shot-number "]" text
 * episode-number = digit+
 * part-number = digit+
 * shot-number = digit+
 * 
 * directive-line = "{" target arg* "}"
 * target = word
 * arg = arg-name "=" arg-value
 * arg-name = word
 * arg-value = word   (maybe extendto quoted text in future)
 * 
 * speaker-line = "-" speaker-name "-"
 * speaker-name = text
 * 
 * speaker-mood-line = "(" mood ")"
 * mood = text
 * 
 * dialog-line = text
 * 
 * other-line = text
 * 
 */
namespace OrdinaryCartoonMaker
{
    public class ParseScreenplay
    {
        public class Directive
        {
            public string target;
            public Dictionary<string, string> args;
            public Dictionary<string, string> originalArgs;

            override public string ToString()
            {
                var s = "{" + target;
                foreach (var arg in originalArgs)
                {
                    s += " " + arg.Key + ":" + arg.Value;
                }
                s += "}";
                return s;
            }
        }

        public class Line
        {
            public string speaker;
            public string mood;
            public string dialog;

            override public string ToString()
            {
                return "-" + speaker + "- (" + mood + ") " + dialog;
            }
        }

        public class Stanza
        {
            public string episodeNumber;
            public string partNumber;
            public string shotNumber;
            public List<Directive> directives = new();
            public List<Line> lines = new();

            override public string ToString()
            {
                var s = ShotCode();
                foreach (var directive in directives)
                {
                    s += " " + directive.ToString();
                }
                foreach (var line in lines)
                {
                    s += " " + line.ToString();
                }
                return s;
            }

            public string ShotCode()
            {
                return $"[{episodeNumber}-{partNumber}-{shotNumber}]";
            }
        }

        // Parse the screenplay and create all the missing shot sequences from it.
        public static void ParseAndProcess(string episodeName, string screenplay)
        {
            Debug.Log("==== Starting making missing sequences...");
            var stanzas = ExtractStanzasFromScreenplay(screenplay);
            ProcessScreenplay(episodeName, stanzas);
            Debug.Log("==== Done making missing sequences...");
        }

        private static void ProcessScreenplay(string episodeName, List<Stanza> stanzas)
        {
            foreach (var stanza in stanzas)
            {
                // Skip the stanza if it references a character that does not exist. This allows the error to be corrected and retried.
                // (It would be good to check for all errors, but its too hard to do them all.)
                if (!CheckAllCharactersExist(stanza))
                {
                    continue;
                }

                string partNumber = stanza.partNumber;
                string shotNumber = stanza.shotNumber;
                string shotCode = "ep" + stanza.episodeNumber + "-" + stanza.partNumber + "-" + stanza.shotNumber;

                // Create the missing Sequence objects for this shot.
                var shotSeq = CreateMissingSequences(episodeName, partNumber, shotNumber);
                if (shotSeq == null)
                {
                    continue;
                }

                // Add the recorder track and clip.
                AssemblyUtils.AddRecorderTrack(episodeName, shotCode, shotSeq, AssemblyUtils.RecordingTypeEnum.Movie);

                // Add characters to scene first so cameras can find characters to look at.
                foreach (var directive in stanza.directives)
                {
                    if (directive.target != "shot" && directive.target != "cm")
                    {
                        ProcessCharacter(shotSeq, directive);
                    }
                }

                // Create the cameras now characters are all created, so we can lookAt and follow them
                GameObject mainCameraGO = null;
                List<GameObject> cinemachineCameras = new();
                int cmNumber = 1;

                foreach (var directive in stanza.directives)
                {
                    if (directive.target == "shot")
                    {
                        mainCameraGO = ProcessShot(shotSeq, directive);
                    }
                    else if (directive.target == "cm")
                    {
                        GameObject cmCameraGO = ProcessCinemachineCamera(shotSeq, directive);
                        if (cmCameraGO != null)
                        {
                            cmCameraGO.name = cmCameraGO.name + " " + cmNumber++;
                            cinemachineCameras.Add(cmCameraGO);
                        }
                    }
                }

                if (mainCameraGO == null)
                {
                    var path = TemplateManager.MainCameraTemplates.GetTemplatePath("16mm");
                    mainCameraGO = AssemblyUtils.InstantiatePrefab(shotSeq, path);
                }

                if (cinemachineCameras.Count == 0)
                {
                    var path = TemplateManager.CinemachineCameraTemplates.GetTemplatePath("CM Camera");
                    var cm = AssemblyUtils.InstantiatePrefab(shotSeq, path);
                    cinemachineCameras.Add(cm);
                }

                if (mainCameraGO != null && cinemachineCameras.Count > 1)
                {
                    // Add a CinemachineTrack pointing to the CM cameras.
                    AssemblyUtils.AddCinemachineBrainTrack(shotSeq, mainCameraGO, cinemachineCameras);
                }

                // Dialog.
                double talkingStartTime = 2;
                foreach (var line in stanza.lines)
                {
                    var character = AssemblyUtils.FindCharacterByName(shotSeq, line.speaker);
                    if (character != null)
                    {
                        var clip = AssemblyUtils.AddTalkingTrack(shotSeq, character, line.mood == "thinking", line.dialog, talkingStartTime);
                        talkingStartTime = clip.start + clip.duration + 0.5;
                    }
                }

                // Save it all to disk.
                var timeline = SequencesApi.GetTimelineSequence(shotSeq).timeline;
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssetIfDirty(timeline);
            }            
        }

        private static GameObject ProcessCinemachineCamera(Transform shotSeq, Directive directive)
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
                Debug.LogError("Failed to determine camera for CM.");
            }
            else
            {
                var cm = camera.GetComponent<CinemachineVirtualCamera>();

                var direction = ParseDirection(directive, "look");
                camera.transform.rotation = Quaternion.Euler(0, direction, 0);

                bool sitting = ParseFlag(directive, "sitting");

                Transform lookAt = ParseCharacter(directive, "lookAt", shotSeq);
                if (lookAt != null)
                {
                    AssemblyUtils.CinemachineCameraLookAt(cm, lookAt, sitting);
                }

                var follow = ParseCharacter(directive, "follow", shotSeq);
                if (follow != null)
                {
                    AssemblyUtils.CinemachineCameraFollow(cm, follow, ParseFrom(directive, "from"), sitting);
                    if (lookAt == null)
                    {
                        // If a "follow" without a "lookAt", then look at the follow target.
                        AssemblyUtils.CinemachineCameraLookAt(cm, follow, sitting);
                    }
                }
                else
                {
                    float cameraDistance = ParseShotFraming(directive, "frame");
                    Vector3 fromOffset = ParseFrom(directive, "from");
                    AssemblyUtils.PositionNonFollowCinemachineCamera(cm, cameraDistance * fromOffset, sitting);
                }
            }
            ReportUnprocessedDirectiveArguments(directive);

            return camera;
        }

        private static bool ParseFlag(Directive directive, string argName)
        {
            if (directive.args.ContainsKey(argName))
            {
                directive.args.Remove(argName);
                return true;
            }
            return false;
        }

        private static GameObject ProcessShot(Transform shotSeq, Directive directive)
        {
            // Main camera
            var mainCameraGO = TemplateDirectiveArgument(shotSeq, directive, "camera", TemplateManager.MainCameraTemplates);
            TemplateDirectiveArgument(shotSeq, directive, "light", TemplateManager.LightTemplates);
            TemplateDirectiveArgument(shotSeq, directive, "wind", TemplateManager.WindTemplates);
            TemplateDirectiveArgument(shotSeq, directive, "cloud", TemplateManager.CloudTemplates);
            ReportUnprocessedDirectiveArguments(directive);
            return mainCameraGO;
        }

        private static void ProcessCharacter(Transform shotSeq, Directive directive)
        {
            var character = AssemblyUtils.AddCharacterToShot(shotSeq, directive.target);
            if (character == null)
            {
                Debug.LogError("Failed to find character referenced by directive: " + directive.target);
            }
            else
            {
                AnimationTrack animationTrack = AssemblyUtils.FindAnimationTrackForCharacter(shotSeq, character);
                if (animationTrack != null)
                {
                    AddClipsToCharacter(directive, "body", "Body", animationTrack);
                    AddClipsToCharacter(directive, "upper", "Upper Body", animationTrack);
                    AddClipsToCharacter(directive, "head", "Head", animationTrack);
                    AddClipsToCharacter(directive, "face", "Face", animationTrack);
                    AddClipsToCharacter(directive, "rh", "Right Hand", animationTrack);
                    AddClipsToCharacter(directive, "lh", "Left Hand", animationTrack);
                    AddClipsToCharacter(directive, "clip", "Generic", animationTrack);

                    // look:up/down/left/right/.../12oclock
                    var direction = ParseUpDownLeftRight(directive, "look");
                    if (direction != Vector2.zero)
                    {
                        AssemblyUtils.CharacterLookInDirection(character, 0.5f * direction);
                    }

                    // lookAt:Hank
                    var lookAt = ParseCharacter(directive, "lookAt", shotSeq);
                    if (lookAt != null)
                    {
                        AssemblyUtils.CharacterLookAt(character, lookAt);
                    }

                    // rotate:180
                    var rotation = ParseFloat(directive, "rotate");
                    if (rotation >= 0)
                    {
                        var currentDirection = character.transform.rotation.eulerAngles;
                        character.transform.rotation = Quaternion.Euler(currentDirection.x, currentDirection.y + rotation, currentDirection.z);
                    }
                }
            }

            ReportUnprocessedDirectiveArguments(directive);

            // Start and stop playing, to pose the character for camera offset calculations.
            AssemblyUtils.StartAndStopTimeline(shotSeq);
        }

        // Return true if all the referenced characters in the shot in {character...} directives exist.
        // (This is used to abort before creating the shot Sequence if its not going to work, allowing a retry after fixing errors in the screenplay.)
        private static bool CheckAllCharactersExist(Stanza stanza)
        {
            bool charactersAllExist = true;
            foreach (var directive in stanza.directives)
            {
                if (directive.target != "shot" && directive.target != "cm")
                {
                    var path = TemplateManager.CharacterTemplates.GetTemplatePath(directive.target);
                    if (path == null)
                    {
                        Debug.LogError("Unable to create shot sequence for " + stanza.ShotCode() + " as " + directive.target + " does not exist.");
                        charactersAllExist = false;
                    }
                }
            }
            return charactersAllExist;
        }

        // Create the master, part, and shot Sequences.
        private static Transform CreateMissingSequences(string episodeName, string partNumber, string shotNumber)
        {
            // Create master sequence if it does not exist.
            if (SequencesApi.RootSequence(episodeName) == null)
            {
                Debug.Log("Creating master sequence " + episodeName);
                SequencesApi.CreateMasterSequence(episodeName, 24);
            }

            if (SequencesApi.PartSequence(episodeName, partNumber) == null)
            {
                Debug.Log("Creating part " + episodeName + "/" + partNumber);
                SequencesApi.CreatePartSequence(episodeName, partNumber, 10);
            }

            var shotSeq = SequencesApi.ShotSequence(episodeName, partNumber, shotNumber);
            if (shotSeq == null)
            {
                Debug.Log("Creating shot " + episodeName + "/" + partNumber + "/" + shotNumber);
                shotSeq = SequencesApi.CreateShotSequence(episodeName, partNumber, shotNumber);
            }
            return shotSeq;
        }

        private static Vector2 ParseUpDownLeftRight(Directive directive, string argName)
        {
            string direction;
            if (directive.args.TryGetValue(argName, out direction))
            {
                directive.args.Remove(argName);
                direction = direction.ToLower();

                if (direction == "up") return new Vector2(0, 1);
                if (direction == "down") return new Vector2(0, -1);
                if (direction == "left") return new Vector2(-1, 0);
                if (direction == "right") return new Vector2(1, 0);

                Debug.LogError("Unknown direction " + direction + " for argument " + argName + " of: " + directive.ToString());
            }
            return Vector2.zero;
        }

        private static float ParseFloat(Directive directive, string argName)
        {
            string numAsStr;
            if (directive.args.TryGetValue(argName, out numAsStr))
            {
                directive.args.Remove(argName);

                float num;
                if (float.TryParse(numAsStr, out num))
                {
                    return num;
                }
            }
            return -1;
        }

        private static void AddClipsToCharacter(Directive directive, string argName, string clipType, AnimationTrack track)
        {
            string clipNames;
            if (directive.args.TryGetValue(argName, out clipNames))
            {
                directive.args.Remove(argName);
                foreach (var clipName in clipNames.Split(","))
                {
                    AssemblyUtils.AddClipToCharacter(track, clipType, clipName);
                }
            }
        }

        private static Dictionary<string, float> shotFramingLookup = new()
        {
            { "closeup", 0.5f },
            { "mid", 1f },
            { "wide", 2f },
        };

        private static float ParseShotFraming(Directive directive, string argName)
        {
            string value;
            if (directive.args.TryGetValue(argName, out value))
            {
                directive.args.Remove(argName);
                value = value.ToLower();
                float offset;
                if (shotFramingLookup.TryGetValue(value, out offset))
                {
                    return offset;
                }
                else
                {
                    Debug.LogError("Unknown offset for " + argName + ":" + value + " in " + directive.ToString());
                }
            }
            return shotFramingLookup.GetValueOrDefault("mid");
        }

        private static Dictionary<string, Vector3> fromDirectionsLookup = new()
        {
            { "Front", new Vector3(0, 0, 1) },
            { "Behind", new Vector3(0, 0, -1) },
            { "BehindLeftShoulder", new Vector3(-0.2f, 0, -0.6f) },
            { "BehindRightShoulder", new Vector3(0.2f, 0, -0.6f) },
            { "Left", new Vector3(-1, 0, 0) },
            { "Right", new Vector3(1, 0, 0) },
        };

        private static Vector3 ParseFrom(Directive directive, string argName)
        {
            string value;
            if (directive.args.TryGetValue(argName, out value))
            {
                directive.args.Remove(argName);
                Vector3 offset;
                if (fromDirectionsLookup.TryGetValue(value, out offset))
                {
                    return offset;
                }
                else
                {
                    Debug.LogError("Unknown offset for " + argName + ":" + value + " in " + directive.ToString());
                }
            }
            return new Vector3(0, 0, 1);
        }

        private static Transform ParseCharacter(Directive directive, string argName, Transform shot)
        {
            string value;
            if (directive.args.TryGetValue(argName, out value))
            {
                directive.args.Remove(argName);
                var character = AssemblyUtils.FindCharacterByName(shot, value);
                if (character != null)
                {
                    return character.transform;
                }
            }
            return null;
        }

        private static GameObject TemplateDirectiveArgument(Transform shot, Directive directive, string argName, TemplateManager templateManager)
        {
            GameObject first = null;
            string value;
            if (directive.args.TryGetValue(argName, out value))
            {
                directive.args.Remove(argName);
                foreach (var val in value.Split(","))
                {
                    var path = templateManager.GetTemplatePath(val);
                    if (path != null)
                    {
                        var go = AssemblyUtils.InstantiatePrefab(shot, path);
                        if (first == null)
                        {
                            first = go;
                        }
                    }
                    else
                    {
                        Debug.LogError("Failed to find " + val + " from "+ directive.ToString());
                    }
                }
            }
            return first;
        }

        private static void ReportUnprocessedDirectiveArguments(Directive directive)
        {
            if (directive.args.Count > 0)
            {
                Debug.LogError("Unused directive argument(s): " + string.Join(", ", directive.args.Keys) + "  " + directive.ToString());
            }
        }

        private static Dictionary<string, float> directions = new Dictionary<string, float>
        {
            { "north", 0 },
            { "northeast", 45 },
            { "east", 90 },
            { "southeast", 135 },
            { "south", 180 },
            { "southwest", 225 },
            { "west", 270 },
            { "northwest", 315 },
            { "n", 0 },
            { "ne", 45 },
            { "e", 90 },
            { "se", 135 },
            { "s", 180 },
            { "sw", 225 },
            { "w", 270 },
            { "nw", 315 },
        };

        private static float ParseDirection(Directive directive, string argName)
        {
            string name;
            if (directive.args.TryGetValue(argName, out name))
            {
                name = name.ToLower();
                directive.args.Remove(argName);
                float value;
                if (directions.TryGetValue(name, out value))
                {
                    return value;
                }
                if (float.TryParse(name, out value))
                {
                    return value;
                }
                Debug.LogError("Unknown direction for " + argName + ":" + name);
            }
            return -1;
        }

        // Returns an array of strings that starts with [n-n-n] at start of first line, then following lines are dialog (all caps text).
        private static List<Stanza> ExtractStanzasFromScreenplay(string script)
        {
            // There could be a \r\n in there as well, so trim off extra whitespace on each line.
            string[] lines = script.Split("\n");
            for (int i = 0; i < lines.Length; i++)
            {
                lines[i] = lines[i].Trim();
            }

            List<Stanza> stanzas = new();
            var shotNumberRegex = new Regex(@"^\[([0-9a-zA-Z]+)-([0-9a-zA-Z]+)-([0-9a-zA-Z]+)\]");
            var directiveRegex = new Regex(@"{([^ ]*) *([^}]*)}");
            var speakerRegex = new Regex(@"^[ ]*-([A-Za-z].*)-[ ]*$");
            var moodRegex = new Regex(@"^[ ]*\(([A-Za-z].*)\)[ ]*$");
            var inDialog = false;
            Stanza currentStanza = null;
            string currentSpeaker = "";
            string currentMood = "";

            foreach (var line in lines)
            {
                if (line.Length == 0)
                {
                    continue;
                }

                // New section (forces end of stanza)
                if (line.StartsWith("INT.") || line.StartsWith("EXT.") || line.StartsWith("MUSIC:") || line.StartsWith("#"))
                {
                    if (currentStanza != null)
                    {
                        stanzas.Add(currentStanza);
                        currentStanza = null;
                    }
                    continue;
                }

                var m = shotNumberRegex.Match(line);
                if (m.Success)
                {
                    if (currentStanza != null)
                    {
                        stanzas.Add(currentStanza);
                    }
                    currentStanza = new Stanza { episodeNumber = m.Groups[1].Value, partNumber = m.Groups[2].Value, shotNumber = m.Groups[3].Value };
                    inDialog = false;
                    continue;
                }

                m = directiveRegex.Match(line);
                if (m.Success && currentStanza != null)
                {
                    currentStanza.directives.Add(new Directive {
                        target = m.Groups[1].Value,
                        args = ParseDirectiveArgs(m.Groups[2].Value),
                        originalArgs = ParseDirectiveArgs(m.Groups[2].Value),
                    });
                    continue;
                }

                m = speakerRegex.Match(line);
                if (m.Success && currentStanza != null)
                {
                    currentSpeaker = m.Groups[1].Value;
                    currentMood = "";
                    inDialog = true;
                    continue;
                }

                m = moodRegex.Match(line);
                if (m.Success && currentStanza != null)
                {
                    currentMood = m.Groups[1].Value;
                    continue;
                }

                if (inDialog)
                {
                    if (currentStanza != null)
                    {
                        currentStanza.lines.Add(new Line { speaker = currentSpeaker, mood = currentMood, dialog = line });
                    }
                    continue;
                }
            }

            if (currentStanza != null)
            {
                stanzas.Add(currentStanza);
            }

#if false
            Debug.Log("=== PARSED SCREENPLAY ===");
            foreach (var stanza in stanzas)
            {
                Debug.Log(stanza.ToString());
            }
            Debug.Log("=== END ===");
#endif

            return stanzas;
        }

        private static Dictionary<string, string> ParseDirectiveArgs(string value)
        {
            var args = new Dictionary<string, string>();

            var pairs = value.Split(" ");
            foreach (var pair in pairs)
            {
                string argName = null;
                string argValue = null;
                var i = pair.IndexOf(":");
                if (i > 0)
                {
                    argName = pair.Substring(0, i);
                    argValue = pair.Substring(i + 1);
                }
                else if (pair != "")
                {
                    argName = pair;
                    argValue = "";
                }

                if (argName != null)
                {
                    if (args.ContainsKey(argName))
                    {
                        Debug.LogError("Directive arguments contains duplicate key " + argName + " (" + value + ").");
                    }
                    else
                    {
                        args.Add(argName, argValue);
                    }
                }
            }

            return args;
        }
    }
}
