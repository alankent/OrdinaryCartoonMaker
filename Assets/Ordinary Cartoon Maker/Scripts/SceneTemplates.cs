using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{

    public static class SceneTemplates
    {
        private static string[] TemplateDirectories = new[] { "Assets/Ordinary Cartoon Maker/Templates/Scenes", "Assets/_LOCAL/Ordinary Cartoon Maker/Templates/Scenes" };

        public static List<string> AvailableTemplates()
        {
            List<string> templates = new();

            var assets = AssetDatabase.FindAssets("t:Scene", TemplateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Get a clean name for the scene
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Remove(name.LastIndexOf('.'));
                if (name.EndsWith(" SceneTemplate"))
                {
                    name = name.Remove(name.LastIndexOf(' '));
                }

                templates.Add(name);
            }

            return templates;
        }

        public static string GetTemplatePath(string sceneName)
        {
            var assets = AssetDatabase.FindAssets("t:Scene", TemplateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Get a clean name for the scene
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Remove(name.LastIndexOf('.'));
                if (name.EndsWith(" SceneTemplate"))
                {
                    name = name.Remove(name.LastIndexOf(' '));
                }

                if (name == sceneName)
                {
                    return path;
                }
            }
            return null;
        }
    }

}