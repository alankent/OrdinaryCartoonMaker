using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class OrdinaryCaptions : MonoBehaviour
{
    public UIDocument uiDocument;
    private Label topCaption1;
    private Label topCaption2;
    private Label bottomCaption1;
    private Label bottomCaption2;

    private void Init()
    {
        if (uiDocument == null)
        {
            uiDocument = GetComponent<UIDocument>();
        }

        if (topCaption1 == null)
        {
            topCaption1 = uiDocument.rootVisualElement[0] as Label;
            topCaption2 = uiDocument.rootVisualElement[1] as Label;
            bottomCaption1 = uiDocument.rootVisualElement[2] as Label;
            bottomCaption2 = uiDocument.rootVisualElement[3] as Label;

            topCaption1.visible = false;
            topCaption1.text = "";
            topCaption2.visible = false;
            topCaption2.text = "";
            bottomCaption1.visible = false;
            bottomCaption1.text = "";
            bottomCaption2.visible = false;
            bottomCaption2.text = "";
        }
    }

    private string MakeText(string speaker, string dialogue)
    {
        if (speaker != null && speaker != "")
        {
            return $"<color=yellow>{speaker}:</color> {dialogue}";
        }
        return dialogue;
    }

    // Display the given caption text.
    public void ShowCaption(bool atTop, string speaker, string dialogue)
    {
        Init();

        if (atTop)
        {
            topCaption1.text = MakeText(speaker, dialogue);
            topCaption1.visible = true;
            topCaption2.text = MakeText(speaker, dialogue);
            topCaption2.visible = true;
        }
        else
        {
            bottomCaption1.text = MakeText(speaker, dialogue);
            bottomCaption1.visible = true;
            bottomCaption2.text = MakeText(speaker, dialogue);
            bottomCaption2.visible = true;
        }
    }

    // Hide the caption if it matches our string (another caption might have
    // already overridden it - don't wipe in that case!
    public void HideCaption(bool atTop, string speaker, string dialogue)
    {
        Init();

        var text = MakeText(speaker, dialogue);
        if (atTop)
        {
            if (topCaption1.text == text)
            {
                topCaption1.text = "";
                topCaption1.visible = false;
                topCaption2.text = "";
                topCaption2.visible = false;
            }
        }
        else
        {
            if (bottomCaption1.text == text)
            {
                bottomCaption1.text = "";
                bottomCaption1.visible = false;
                bottomCaption2.text = "";
                bottomCaption2.visible = false;
            }
        }
    }

    // Clear the top and bottom captions.
    // When scrubbing the timelines, not all the 'end' caption events might occur nicely.
    public void ClearCaptions()
    {
        topCaption1.text = "";
        topCaption1.visible = false;
        topCaption2.text = "";
        topCaption2.visible = false;
        bottomCaption1.text = "";
        bottomCaption1.visible = false;
        bottomCaption2.text = "";
        bottomCaption2.visible = false;
    }
}
