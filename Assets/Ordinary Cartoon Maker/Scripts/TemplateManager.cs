using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{
    public class TemplateManager
    {
        public static TemplateManager SceneTemplates = new TemplateManager("Scenes", "SceneTemplate", "t:Scene");
        public static TemplateManager MainCameraTemplates = new TemplateManager("Main Cameras", "MainCameraTemplate", "t:Prefab");
        public static TemplateManager CinemachineCameraTemplates = new TemplateManager("Cinemachine Cameras", "CinemachineCameraTemplate", "t:Prefab");
        public static TemplateManager CameraPositionTemplates = new TemplateManager("Camera Positions", "CameraPositionTemplate", "t:Prefab");
        public static TemplateManager CharacterTemplates = new TemplateManager("Characters", "CharacterTemplate", "t:Prefab");
        public static TemplateManager WindTemplates = new TemplateManager("Wind", "WindTemplate", "t:Prefab");
        public static TemplateManager CloudTemplates = new TemplateManager("Cloud", "CloudTemplate", "t:Prefab");
        public static TemplateManager LightTemplates = new TemplateManager("Lights", "LightTemplate", "t:Prefab");

        // Implementation

        private string[] templateDirectories;
        private string suffix;
        private string filter;

        private TemplateManager(string folder, string suffix, string filter)
        {
            this.templateDirectories = new[] {
                "Assets/_LOCAL/Ordinary Cartoon Maker/Templates/" + folder + "/",
                "Assets/_SHARED/Ordinary Cartoon Maker/Templates/" + folder + "/",
                "Assets/Ordinary Cartoon Maker/Templates/" + folder + "/",
            };
            this.suffix = " " + suffix;
            this.filter = filter;
        }

        public List<string> AvailableTemplates()
        {
            List<string> templates = new();

            var assets = AssetDatabase.FindAssets(filter, templateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                templates.Add(CleanName(path));
            }

            return templates;
        }

        public string GetTemplatePath(string assetName)
        {
            var assets = AssetDatabase.FindAssets(filter, templateDirectories);
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (CleanName(path) == assetName)
                {
                    return path;
                }
            }
            return null;
        }

        private string CleanName(string path)
        {
            var name = path.Remove(path.LastIndexOf('.'));
            if (name.EndsWith(suffix))
            {
                name = name.Remove(name.Length - suffix.Length);
            }
            foreach (var folder in templateDirectories)
            {
                if (name.StartsWith(folder))
                {
                    name = name.Substring(folder.Length);
                }
            }
            return name.Replace(" % ", "/");
        }
    }
}