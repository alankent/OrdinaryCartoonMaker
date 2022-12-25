using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{
    public static class CameraTypeTemplates
    {
        private static string[] TemplateDirectories = new[] { "Assets/Ordinary Cartoon Maker/Templates/Camera Types", "Assets/_LOCAL/Ordinary Cartoon Maker/Templates/Camera Types" };

        public static List<string> AvailableTemplates()
        {
            List<string> templates = new();

            var assets = AssetDatabase.FindAssets("t:Prefab", TemplateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Get a clean name for the scene
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Remove(name.LastIndexOf('.'));
                if (name.EndsWith(" CameraTypeTemplate"))
                {
                    name = name.Remove(name.LastIndexOf(' '));
                }

                templates.Add(name);
            }

            return templates;
        }

        public static string GetTemplatePath(string cameraType)
        {
            var assets = AssetDatabase.FindAssets("t:Prefab", TemplateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);

                // Get a clean name for the scene
                var name = path.Substring(path.LastIndexOf('/') + 1);
                name = name.Remove(name.LastIndexOf('.'));
                if (name.EndsWith(" CameraTypeTemplate"))
                {
                    name = name.Remove(name.LastIndexOf(' '));
                }

                if (name == cameraType)
                {
                    return path;
                }
            }
            return null;
        }
    }

}