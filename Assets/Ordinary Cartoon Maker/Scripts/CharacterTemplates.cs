using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace OrdinaryCartoonMaker
{
    public static class CharacterTemplates
    {
        private static string TemplateDirectory = "Assets/_LOCAL/Ordinary Cartoon Maker/Templates/Characters/";

        public static List<string> AvailableTemplates()
        {
            // Find all prefabs, expected to be in a subfolder such as Sam/Classroom/Sitting.
            // These should be prefabs with a CharacterInstructions component holding information
            // needed to create an animation track for the character.

            List<string> templates = new();

            // TODO: Try t:CharacterInstructions instead of t:prefab. Should work...
            var assets = AssetDatabase.FindAssets("t:prefab", new[] { TemplateDirectory });
            foreach (var guid in assets)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var name = path.Substring(TemplateDirectory.Length);
                name = name.Substring(0, name.LastIndexOf('.'));
                templates.Add(name);
            }

            return templates;
        }

        public static string GetTemplatePath(string characterSelection)
        {
            return TemplateDirectory + characterSelection + ".prefab";
        }
    }
}