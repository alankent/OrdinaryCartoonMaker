using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace OrdinaryCartoonMaker
{

    public class OrdinaryCartoonMakerEditor : EditorWindow
    {
        [SerializeField]
        private VisualTreeAsset m_VisualTreeAsset = default;

        private OrdinaryCartoonMakerScriptableObject maker;

        [MenuItem("Window/Ordinary Cartoon Maker")]
        public static void ShowExample()
        {
            OrdinaryCartoonMakerEditor wnd = GetWindow<OrdinaryCartoonMakerEditor>();
            wnd.titleContent = new GUIContent("OrdinaryCartoonMakerEditor");
        }

        public void CreateGUI()
        {
            // Instantiate UXML
            VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
            rootVisualElement.Add(labelFromUXML);

            // Bind form to our scriptable object that does the real work.
            maker = ScriptableObject.CreateInstance<OrdinaryCartoonMakerScriptableObject>();
            rootVisualElement.Bind(new SerializedObject(maker));

            // Register callbacks for the tabbed menu handler to process clicks etc
            var controller = new TabbedMenuController(labelFromUXML);
            controller.RegisterTabCallbacks();

            // Find the buttons and bind actions to them.
            rootVisualElement.Q<Button>("CreateEpisodeButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreateEpisode() );
            rootVisualElement.Q<Button>("CreatePartButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreatePart());
            rootVisualElement.Q<Button>("CreateShotButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreateShot());
            rootVisualElement.Q<Button>("CreateSpeechBubbleButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreateSpeechBubble());

            // Default episode details from the current scene.
            maker.IdentifyEpisodePartShot(CurrentSceneName(), "", "");

            // If the scene changes for any reason, we need to create a new ScriptableObject.
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;

            // Get list of template scenes.
            var sceneTemplate = rootVisualElement.Q<DropdownField>("SceneTemplate");
            sceneTemplate.choices = SceneTemplates.AvailableTemplates();
            sceneTemplate.index = 0;

            // Get list of camera type prefabs
            var cameraType = rootVisualElement.Q<DropdownField>("CameraType");
            cameraType.choices = CameraTypeTemplates.AvailableTemplates();
            cameraType.index = 0;

            // Get list of camera type prefabs
            var cameraPosition = rootVisualElement.Q<DropdownField>("CameraPosition");
            List<string> positions = new();
            positions.Add("Align With View");
            positions.AddRange(CameraPositionTemplates.AvailableTemplates());
            cameraPosition.choices = positions;
            cameraPosition.index = 0;
        }

        private void OnSelectionChange()
        {
            //Debug.Log("OnSelectionChange()");
            if (Selection.activeGameObject == null)
            {
                return;
            }
            var obj = Selection.activeGameObject.transform;
            List<string> sequenceNames = new();
            while (obj != null)
            {
                //Debug.Log(obj.name);
                if (obj.GetComponent<UnityEngine.Sequences.SequenceFilter>() != null)
                {
                    sequenceNames.Add(obj.name);
                }
                obj = obj.parent;
            }

            if (sequenceNames.Count == 3)
            {
                maker.IdentifyEpisodePartShot(sequenceNames[2], sequenceNames[1], sequenceNames[0]);
            }
            else if (sequenceNames.Count == 2)
            {
                maker.IdentifyEpisodePartShot(sequenceNames[1], sequenceNames[0], "");
            }
            else if (sequenceNames.Count == 1)
            {
                maker.IdentifyEpisodePartShot(sequenceNames[0], "", "");
            }
        }

        private string CurrentSceneName()
        {
            return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        }

        private void SceneChanged(UnityEngine.SceneManagement.Scene oldScene, UnityEngine.SceneManagement.Scene newScene)
        {
            // If the scene changed, the bindings get lost. Create a new maker and start again.
            // Note: This is not perfect - it does lose old user input and error messages. But creating new scenes is rare, so too bad!
            //Debug.Log("---------- SCENE CHANGED");
            maker = ScriptableObject.CreateInstance<OrdinaryCartoonMakerScriptableObject>();
            rootVisualElement.Bind(new SerializedObject(maker));
            maker.IdentifyEpisodePartShot(CurrentSceneName(), "", "");
        }
    }
}