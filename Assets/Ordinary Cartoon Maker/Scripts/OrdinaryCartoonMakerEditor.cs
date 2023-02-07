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
            rootVisualElement.Q<Button>("ParseScreenplayButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.PopulateEpisodeFromScreenplay());
            rootVisualElement.Q<Button>("CreatePartButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreatePart());
            rootVisualElement.Q<Button>("CreateShotButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreateShot());
            //rootVisualElement.Q<Button>("RecordShotButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.RecordShot());
            rootVisualElement.Q<Button>("AddCharacterButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.AddCharacter());
            rootVisualElement.Q<Button>("AddToCharacterButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.AddToCharacter());
#if SPEECH_BUBBLES
            rootVisualElement.Q<Button>("CreateSpeechButton").RegisterCallback<ClickEvent>((ClickEvent ev) => maker.CreateSpeech());
#endif
            var startStopVmcReceiverButton = rootVisualElement.Q<Button>("StartStopVmcReceiverButton");
            var startStopVmcRecordingButton = rootVisualElement.Q<Button>("StartStopVmcRecordingButton");
            startStopVmcReceiverButton.RegisterCallback<ClickEvent>((ClickEvent ev) => maker.StartStopVmcReceiver(startStopVmcReceiverButton, startStopVmcRecordingButton));
            startStopVmcRecordingButton.RegisterCallback<ClickEvent>((ClickEvent ev) => maker.StartStopVmcRecording(startStopVmcRecordingButton));

            // Default episode details from the current scene.
            maker.IdentifyEpisodePartShot(CurrentSceneName(), "", "");

            // If the scene changes for any reason, we need to create a new ScriptableObject.
            EditorSceneManager.activeSceneChangedInEditMode += SceneChanged;

            // Get list of template scenes.
            var sceneTemplate = rootVisualElement.Q<DropdownField>("SceneTemplate");
            sceneTemplate.choices = TemplateManager.SceneTemplates.AvailableTemplates();
            sceneTemplate.index = 0;

            // Populate dropdowns
            InitDropdown("MainCamera", TemplateManager.MainCameraTemplates);
            InitDropdown("CinemachineCamera", TemplateManager.CinemachineCameraTemplates);
            InitDropdown("Light", TemplateManager.LightTemplates);
            InitDropdown("Wind", TemplateManager.WindTemplates);
            InitDropdown("Cloud", TemplateManager.CloudTemplates);

            // Get list of camera position prefabs
            var cameraPosition = rootVisualElement.Q<DropdownField>("CameraPosition");
            List<string> positions = new();
            positions.Add("Align With View");
            positions.AddRange(TemplateManager.CameraPositionTemplates.AvailableTemplates());
            cameraPosition.choices = positions;
            cameraPosition.index = 0;

            // Get list of characters, positions, and animations for adding new characters
            var characterSelection = rootVisualElement.Q<DropdownField>("CharacterSelection");
            characterSelection.choices = TemplateManager.CharacterTemplates.AvailableTemplates();

            // Get body, facial expression, left hand, and right hand clips that can be added as well
            rootVisualElement.Q<DropdownField>("Body").choices = ExtraClips.AvailableClips("Body");
            rootVisualElement.Q<DropdownField>("UpperBody").choices = ExtraClips.AvailableClips("Upper Body");
            rootVisualElement.Q<DropdownField>("Face").choices = ExtraClips.AvailableClips("Face");
            rootVisualElement.Q<DropdownField>("Head").choices = ExtraClips.AvailableClips("Head");
            rootVisualElement.Q<DropdownField>("LeftHand").choices = ExtraClips.AvailableClips("Left Hand");
            rootVisualElement.Q<DropdownField>("RightHand").choices = ExtraClips.AvailableClips("Right Hand");
            rootVisualElement.Q<DropdownField>("Generic").choices = ExtraClips.AvailableClips("Generic");

            //maker.RegisterCharacterListUpdater(UpdateCharacterList);
        }

        private void InitDropdown(string dropdownName, TemplateManager templates)
        {
            var mainCamera = rootVisualElement.Q<DropdownField>(dropdownName);
            mainCamera.choices = templates.AvailableTemplates();
            mainCamera.index = 0;
        }

#if false
        private void UpdateCharacterList(List<string> characters)
        {
            var dropDown = rootVisualElement.Q<DropdownField>("SpeechCharacterSelection");
            dropDown.choices = characters;
            maker.SpeechCharacterSelection = characters.Count > 0 ? characters[0] : "";
        }
#endif

        private void OnSelectionChange()
        {
            //Debug.Log("OnSelectionChange()");
            if (Selection.activeGameObject == null)
            {
                return;
            }

            // Climb up ancestry of the game object to work out if under a episode/part/shot sequence hierarchy
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

            // See if a character with an animation track.
            maker.SelectionChange();
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

        void Update()
        {
            maker.Update();
        }

        private void OnDestroy()
        {
            maker.StopVmcReceiver();
        }
    }
}
