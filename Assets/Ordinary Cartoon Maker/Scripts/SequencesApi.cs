using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor.Sequences;
using UnityEngine.Sequences;
using System;
using UnityEditor;

namespace OrdinaryCartoonMaker
{
    // Calls to interrogate and manipulate sequences, brought into a single class so changes
    // to the Sequences package API will not impact the rest of this package.
    public class SequencesApi
    {
        public class SequenceReference
        {
            private MasterSequence master;
            private TimelineSequence seq;

            public SequenceReference(MasterSequence master, TimelineSequence seq)
            {
                this.master = master;
                this.seq = seq;
            }

            public MasterSequence GetMaster()
            {
                return master;
            }

            public SequenceReference Child(string childName)
            {
                foreach (var child in seq.children)
                {
                    if (child.name == childName)
                    {
                        return new SequenceReference(master, child as TimelineSequence);
                    }
                }
                return null;
            }

            public TimelineSequence GetTimelineSequence()
            {
                return seq as TimelineSequence;
            }
        }

        // Returns the root master sequence if it exists, or null if it is not found.
        public static SequenceReference RootSequence(string episodeName)
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            GameObject[] gameObjects = scene.GetRootGameObjects();
            foreach (var go in gameObjects)
            {
                if (go.name == episodeName)
                {
                    var sf = go.GetComponent<SequenceFilter>();
                    if (sf != null && sf.masterSequence != null)
                    {
                        return new SequenceReference(sf.masterSequence, sf.masterSequence.rootSequence);
                    }
                }
            }
            //Debug.Log("Did not find master sequence: " + episodeName);
            return null;
        }

        // Create the master sequence and return it.
        public static SequenceReference CreateMasterSequence(string episodeName, float fps)
        {
            var master = SequenceUtility.CreateMasterSequence(episodeName, fps);
            var seq = master.rootSequence;
            return new SequenceReference(master, seq);
        }

        public static SequenceReference PartSequence(string episodeName, string partNumber)
        {
            var root = RootSequence(episodeName);
            var part = root.Child(partNumber);
            return part;
        }

        public static SequenceReference CreatePartSequence(string episodeName, string partNumber, int partDuration)
        {
            var root = RootSequence(episodeName);
            var part = SequenceUtility.CreateSequence(partNumber, root.GetMaster());

            part.editorialClip.duration = partDuration;

            // Extend the parent clip length so it fits.
            if (part.editorialClip.start + part.editorialClip.duration > root.GetTimelineSequence().editorialClip.duration)
            {
                root.GetTimelineSequence().editorialClip.duration = part.editorialClip.start + part.editorialClip.duration;
                EditorUtility.SetDirty(root.GetTimelineSequence().timeline);
                AssetDatabase.SaveAssetIfDirty(root.GetTimelineSequence().timeline);
            }

            EditorUtility.SetDirty(part.timeline);
            AssetDatabase.SaveAssetIfDirty(part.timeline);

            return new SequenceReference(root.GetMaster(), part);
        }

        public static SequenceReference ShotSequence(string episodeName, string partNumber, string shotNumber)
        {
            var root = RootSequence(episodeName);
            var part = root.Child(partNumber);
            var shot = part.Child(shotNumber);
            return shot;
        }

        public static SequenceReference CreateShotSequence(string episodeName, string partNumber, string shotNumber)
        {
            var root = RootSequence(episodeName);
            var part = root.Child(partNumber).GetTimelineSequence();
            var shot = SequenceUtility.CreateSequence(shotNumber, root.GetMaster(), part);

            shot.editorialClip.duration = 10;

            // Extend the parent clip length so it fits.
            if (shot.editorialClip.start + shot.editorialClip.duration > part.editorialClip.duration)
            {
                part.editorialClip.duration = shot.editorialClip.start + shot.editorialClip.duration;
                EditorUtility.SetDirty(part.timeline);
                AssetDatabase.SaveAssetIfDirty(part.timeline);
            }

            EditorUtility.SetDirty(shot.timeline);
            AssetDatabase.SaveAssetIfDirty(shot.timeline);

            return new SequenceReference(root.GetMaster(), shot);
        }
    }
}

