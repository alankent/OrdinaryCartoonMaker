using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

// Contains clip data which is passed to the behavior at run time.
public class TalkingClip : PlayableAsset, ITimelineClipAsset
{
    [Tooltip("Text to be spoken")]
    [TextArea(5, 10)]
    [SerializeField]
    public string Text;

    public bool ConvertTextToVisemes = true;

    [Tooltip("Visemes computed from text")]
    [TextArea(5, 10)]
    [SerializeField]
    public string Visemes;

    [Range(0f, 1f)] public float VisemeWeight = 1f;

    public bool HideCaptions = false;
    public bool ShowCaptionAtTop = false;
    public string Speaker = "";
    public float startPad = 0;
    public float endPad = 0;

    // We dont support blending or anything fancy.
    public ClipCaps clipCaps => ClipCaps.None;

    public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
    {
        // 'owner' you can call GetComponent<PlayableDirector>(); on

        var playable = ScriptPlayable<TalkingBehaviour>.Create(graph);

        if (playable.GetBehaviour() != null)
            SetAttributes(playable.GetBehaviour());

        return playable;
    }

    void SetAttributes(TalkingBehaviour talkingBehavior)
    {
        if (ConvertTextToVisemes)
        {
            UpdateVisemesFromText();
        }
        talkingBehavior.Visemes = Visemes;
        talkingBehavior.Weight = VisemeWeight;
        talkingBehavior.Text = Text;
        talkingBehavior.Speaker = Speaker;
        talkingBehavior.ShowCaptionAtTop = ShowCaptionAtTop;
        talkingBehavior.HideCaption = HideCaptions;
        talkingBehavior.Captions = FindObjectOfType<OrdinaryCaptions>();
        talkingBehavior.startPad = startPad;
        talkingBehavior.endPad = endPad;
    }

    // Convert text into a viseme sequence
    public void UpdateVisemesFromText()
    {
        // Really crude lipsync algorithm.
        // Space = mouth closed, A = open mouth (ah), E = wide open a bit (ee), I = wide not open (it), O = wider version of A, U = narrow
        string text = Text ?? "";

        string visemes = " ";
        for (int i = 0; i < text.Length; i++)
        {
            char c = char.ToLower(text[i]);
            char nextc = (i + 1 < text.Length) ? char.ToLower(text[i + 1]) : ' ';
            switch (c)
            {
                // A, E, I, O, U, Y, EE, AI, OO, EA, OA, IR, OW, OU, AW, EW, OI, OY, AR, OR, AY 
                case 'a':
                    if (nextc == 'y' || nextc == 'i')
                    {
                        visemes += "AE";
                        i++;
                    }
                    else if (nextc == 'w')
                    {
                        visemes += "U";
                        i++;
                    }
                    else if (nextc == 'r')
                    {
                        visemes += "A";
                        i++;
                    }
                    else
                    {
                        visemes += "A";
                    }
                    break;
                case 'e':
                    if (nextc == 'y')
                    {
                        visemes += "AE";
                        i++;
                    }
                    else if (nextc == 'e' || nextc == 'a')
                    {
                        visemes += "E";
                        i++;
                    }
                    else if (nextc == 'r')
                    {
                        visemes += "A";
                        i++;
                    }
                    else
                    {
                        visemes += "E";
                    }
                    break;
                case 'i':
                case '|':
                    if (nextc == 'y')
                    {
                        visemes += "IE";
                        i++;
                    }
                    else
                    {
                        visemes += "I";
                    }
                    break;
                case 'o':
                    if (nextc == 'i')
                    {
                        visemes += "OE";
                        i++;
                    }
                    else if (nextc == 'o' || nextc == 'r')
                    {
                        visemes += "U";
                    }
                    else if (nextc == 'u')
                    {
                        visemes += "AU";
                    }
                    else
                    {
                        visemes += "O";
                    }
                    break;
                case 'u':
                    visemes += "U";
                    break;
                case 'y':
                    visemes += "E";
                    break;
                case 'w':
                    visemes += "U";
                    if (nextc == 'h') i++;
                    break;
                case 's':
                    visemes += "S";
                    if (nextc == 's' || nextc == 't' || nextc == 'h') i++;
                    break;
                case 'c':
                case 'k':
                    visemes += "C";
                    if (nextc == 'k' || nextc == 'h') i++;
                    break;
                default:
                    // Anything else (consonant, space between words) means should close mouth
                    if (visemes[visemes.Length - 1] != ' ')
                    {
                        visemes += " ";
                    }
                    break;
            }
        }

        if (visemes[visemes.Length - 1] != ' ')
        {
            visemes += " ";
        }

        Visemes = visemes;
    }
}
