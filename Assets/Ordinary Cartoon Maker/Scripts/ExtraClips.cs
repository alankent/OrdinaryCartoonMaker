using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{
    public static class ExtraClips
    {
        private static string[] ClipDirectories(string clipType)
        {
            return new string[] {
                "Assets/_LOCAL/Ordinary Cartoon Maker/Animation Clips/" + clipType,
                "Assets/_SHARED/Ordinary Cartoon Maker/Animation Clips/" + clipType,
                "Assets/Ordinary Cartoon Maker/Animation Clips/" + clipType,
            };
        }

        public static List<string> AvailableClips(string clipType)
        {
            // Find all prefabs (AnimationInstruction) that describe animation clips that can be used.

            List<string> templates = new();
            //Debug.Log("CLIPS: " + clipType);

            var assets = AssetDatabase.FindAssets("t:Prefab", ClipDirectories(clipType));
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Substring(0, name.LastIndexOf('.'));
                templates.Add(name);
                //Debug.Log(name);
            }

            return templates;
        }

        public static string GetClipPath(string clipType, string selection)
        {
            var assets = AssetDatabase.FindAssets("t:Prefab", ClipDirectories(clipType));
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Get a clean name for the scene
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Remove(name.LastIndexOf('.'));

                if (name == selection)
                {
                    return path;
                }
            }
            return null;
        }
    }
}
