using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;


// GREAT VIDEO! https://www.youtube.com/watch?v=UEuM-Fckx5w


public class TalkingBehaviour : PlayableBehaviour
{
    public string Visemes;
    public float Weight;
    public bool HideCaption;
    public bool ShowCaptionAtTop;
    public string Text;
    public string Speaker;
    public OrdinaryCaptions Captions;
    public float startPad;
    public float endPad;

    private AlansBlendShapeClipVowels m_abscv = null;

    class VisemeWeights
    {
        public float A;
        public float E;
        public float I;
        public float O;
        public float U;

        public VisemeWeights(float A, float E, float I, float O, float U)
        {
            this.A = A;
            this.E = E;
            this.I = I;
            this.O = O;
            this.U = U;
        }
    }

    public override void ProcessFrame(Playable playable, FrameData info, object playerData)
    {
        // We have the text in m_Text
        // We have AlansBlendShapeClipVowels in playerData
        // We have the current frame to display in info

        // Time in seconds since start of clip and the duration of the clip.
        float time = (float) playable.GetTime();
        float clipDuration = (float) playable.GetDuration();

        float mouthTime = time - startPad;
        float mouthDuration = clipDuration - startPad - endPad;

        if (mouthTime >= 0)
        {
            // Work out duration per viseme.
            float visemeDuration = mouthDuration / (Visemes.Length - 1);

            // Work out the viseme index 
            int vi = (int)(mouthTime / visemeDuration);
            vi = (vi >= Visemes.Length) ? Visemes.Length - 1 : vi;
            char vc1 = Visemes[vi];
            char vc2 = (vi + 1 < Visemes.Length) ? Visemes[vi + 1] : Visemes[vi];

            VisemeWeights vw1 = CharToViseme(vc1);
            VisemeWeights vw2 = CharToViseme(vc2);

            // Offset into current viseme.
            float lerp = (mouthTime / visemeDuration) - vi;

            AlansBlendShapeClipVowels abscv = (AlansBlendShapeClipVowels)playerData;
            if (abscv != null && Weight > 0f)
            {
                m_abscv = abscv;
                m_abscv.A = (vw1.A + (vw2.A - vw1.A) * lerp) * Weight;
                m_abscv.E = (vw1.E + (vw2.E - vw1.E) * lerp) * Weight;
                m_abscv.I = (vw1.I + (vw2.I - vw1.I) * lerp) * Weight;
                m_abscv.O = (vw1.O + (vw2.O - vw1.O) * lerp) * Weight;
                m_abscv.U = (vw1.U + (vw2.U - vw1.U) * lerp) * Weight;
                m_abscv.UpdateBlendShapeClips();
            }
        }

        // Update the captions
        if (Captions != null)
        {
            if (time < 0 || time >= clipDuration)
            {
                Captions.HideCaption(ShowCaptionAtTop, Speaker, Text);
            }
            else
            {
                if (!HideCaption)
                {
                    Captions.ShowCaption(ShowCaptionAtTop, Speaker, Text);
                }
            }
        }
    }

    public override void OnBehaviourPause(Playable playable, FrameData info)
    {
        if (m_abscv != null)
        {
            m_abscv.A = 0;
            m_abscv.E = 0;
            m_abscv.I = 0;
            m_abscv.O = 0;
            m_abscv.U = 0;
            m_abscv.UpdateBlendShapeClips();
        }

        // From forum thread https://forum.unity.com/threads/code-example-how-to-detect-the-end-of-the-playable-clip.659617/
        var duration = playable.GetDuration();
        var count = playable.GetTime() + info.deltaTime;

        if ((info.effectivePlayState == PlayState.Paused && count > duration) || playable.GetGraph().GetRootPlayable(0).IsDone())
        {
            if (Captions != null)
            {
                Captions.ClearCaptions();
            }
        }
    }

    private static VisemeWeights AWeights = new(1, 0, 0, 0, 0);
    private static VisemeWeights EWeights = new(0, 1, 0, 0, 0);
    private static VisemeWeights IWeights = new(0, 0.5f, 0.5f, 0, 0); // "i" does not open mouth much
    private static VisemeWeights OWeights = new(0, 0, 0, 1, 0);
    private static VisemeWeights UWeights = new(0, 0, 0, 0, 1);
    private static VisemeWeights SWeights = new(0, 0, 1, 0, 1);
    private static VisemeWeights CWeights = new(0.3f, 0.3f, 0.3f, 0, 0);
    private static VisemeWeights ZeroWeights = new(0, 0, 0, 0, 0);

    private VisemeWeights CharToViseme(char c)
    {
        switch (c)
        {
            case 'A': return AWeights;
            case 'E': return EWeights;
            case 'I': return IWeights;
            case 'O': return OWeights;
            case 'U': return UWeights;
            case 'C': return CWeights;
            case 'S': return SWeights;
            default: return ZeroWeights;
        }
    }
}
