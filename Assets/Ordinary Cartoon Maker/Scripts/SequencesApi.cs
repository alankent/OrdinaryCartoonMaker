using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Sequences;
using UnityEngine.Sequences;
using System;
using UnityEditor;
using UnityEngine.Timeline;
using UnityEngine.Playables;
using UnityEngine.Sequences.Timeline;
using System.Reflection;

namespace OrdinaryCartoonMaker
{
    // Calls to interrogate and manipulate sequences, brought into a single class so changes
    // to the Sequences package API will not impact the rest of this package.
    public class SequencesApi
    {
        // Returns the root master sequence if it exists, or null if it is not found.
        public static Transform RootSequence(string episodeName)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] gameObjects = scene.GetRootGameObjects();
            foreach (var go in gameObjects)
            {
                if (go.name == episodeName)
                {
                    return go.transform;
                }
            }
            return null;
        }

        // Create the master sequence and return it.
        public static Transform CreateMasterSequence(string episodeName, float fps)
        {
            var master = SequenceUtility.CreateMasterSequence(episodeName, fps);
            return RootSequence(episodeName);
        }

        public static Transform PartSequence(string episodeName, string partNumber)
        {
            return RootSequence(episodeName).Find(partNumber);
        }

        public static Transform CreatePartSequence(string episodeName, string partNumber, int partDuration)
        {
            var root = RootSequence(episodeName);
            var rootTimeline = root.GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            var rootSequence = GetTimelineSequence(root);
            var part = SequenceUtility.CreateSequence(partNumber, GetMasterSequence(root), rootSequence);

            var partEditorialClip = FindEditorialClipForTimeline(root, part.timeline);
            partEditorialClip.duration = partDuration;

            // Extend the parent clip length so it fits.
            var rootEditorialClip = FindEditorialClipForTimeline(root, part.timeline);
            if (partEditorialClip.start + partEditorialClip.duration > rootEditorialClip.duration)
            {
                rootEditorialClip.duration = partEditorialClip.start + partEditorialClip.duration;
                EditorUtility.SetDirty(rootTimeline);
                AssetDatabase.SaveAssetIfDirty(rootTimeline);
            }

            EditorUtility.SetDirty(part.timeline);
            AssetDatabase.SaveAssetIfDirty(part.timeline);

            return root.Find(partNumber);
        }

        public static Transform ShotSequence(string episodeName, string partNumber, string shotNumber)
        {
            return RootSequence(episodeName).Find(partNumber).Find(shotNumber);
        }

        public static Transform CreateShotSequence(string episodeName, string partNumber, string shotNumber)
        {
            var root = RootSequence(episodeName);
            var part = root.Find(partNumber).GetComponent<PlayableDirector>().playableAsset as TimelineAsset;
            var partSeq = GetTimelineSequence(root.Find(partNumber));
            var shot = SequenceUtility.CreateSequence(shotNumber, GetMasterSequence(root), partSeq);

            var shotEditorialClip = FindEditorialClipForTimeline(root.Find(partNumber), shot.timeline);
            shotEditorialClip.duration = 10;
            var partEditorialClip = FindEditorialClipForTimeline(root, part);

            // Extend the parent clip length so it fits.
            if (shotEditorialClip.start + shotEditorialClip.duration > partEditorialClip.duration)
            {
                partEditorialClip.duration = shotEditorialClip.start + shotEditorialClip.duration;
                EditorUtility.SetDirty(part);
                AssetDatabase.SaveAssetIfDirty(part);
            }

            EditorUtility.SetDirty(shot.timeline);
            AssetDatabase.SaveAssetIfDirty(shot.timeline);

            return root.Find(partNumber).Find(shotNumber);
        }

        private static TimelineSequence GetTimelineSequence(Transform transform)
        {
            var sf = transform.GetComponent<SequenceFilter>();
            var t = typeof(SequenceFilter);
            var propertyInfo = t.GetProperty("sequence", BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.NonPublic);
            return propertyInfo.GetValue(sf) as TimelineSequence;
        }

        private static TimelineClip FindEditorialClipForTimeline(Transform parent, TimelineAsset childTimeline)
        {
            var director = parent.GetComponent<PlayableDirector>();
            var timeline = director.playableAsset as TimelineAsset;
            foreach (var track in timeline.GetRootTracks())
            {
                var editorialTrack = track as EditorialTrack;
                if (editorialTrack != null)
                {
                    foreach (var editorialClip in editorialTrack.GetClips())
                    {
                        var a = editorialClip.asset as EditorialPlayableAsset;
                        var t = typeof(EditorialPlayableAsset).GetField("m_Timeline", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(a) as TimelineAsset;
                        if (t == childTimeline)
                        {
                            return editorialClip;
                        }
                    }
                }
            }
            return null;            
        }

        public static MasterSequence GetMasterSequence(Transform transform)
        {
            var sf = transform.GetComponent<SequenceFilter>();
            var fieldInfo = typeof(SequenceFilter).GetField("m_MasterSequence", BindingFlags.NonPublic | BindingFlags.Instance);
            return fieldInfo.GetValue(sf) as MasterSequence;
        }
    }
}

