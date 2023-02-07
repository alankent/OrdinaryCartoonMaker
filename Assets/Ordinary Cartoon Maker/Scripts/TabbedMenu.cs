using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace OrdinaryCartoonMaker
{
    public class TabbedMenu : MonoBehaviour
    {
        private TabbedMenuController controller;

        private void OnEnable()
        {
            UIDocument menu = GetComponent<UIDocument>();
            VisualElement root = menu.rootVisualElement;
            controller = new(root);
            controller.RegisterTabCallbacks();
        }
    }
}