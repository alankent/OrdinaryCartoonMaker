using System.Collections;
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
using UnityEditor;
using VRM;

[ExecuteAlways]
public class AlansBlendShapeClip : MonoBehaviour
{
    public Object blendShapeClipDirectory;
    private Object m_lastBlendShapeClipDirectory = null;
    private string m_blendShapeClipDirectoryPath = null;
    private List<BlendShapeClip> m_clips = new List<BlendShapeClip>();
    private AlansBlendShapeClipVowels m_vowels;
    BlendShapeMerger m_merger;

    // Force deferred loading of blendshape clips (cannot be done in constructor).
    public bool ForceReload = true;

#if false
// This works, but would need an editor to look nice and introduces another layer of nesting to animate the weights, so would break all my existing animation clips.
    [System.Serializable]
    public class BlendShapeClipGroup
    {
        public bool Loaded = false;
        [Range(0f, 1f)] public float Weight = 0;
    }
    //public BlendShapeClip EyesClosedHappyGroup = new();
#endif

    [Range(0f, 1f)] public float Neutral = 0;
    [Range(0f, 1f)] public float A = 0;
    [Range(0f, 1f)] public float E = 0;
    [Range(0f, 1f)] public float I = 0; 
    [Range(0f, 1f)] public float O = 0;
    [Range(0f, 1f)] public float U = 0;
    [Range(0f, 1f)] public float Angry = 0;
    [Range(0f, 1f)] public float AngryVein = 0;
    [Range(0f, 1f)] public float BagStraps = 0;
    [Range(0f, 1f)] public float Blank = 0;    
    [Range(0f, 1f)] public float Blink = 0;    
    [Range(0f, 1f)] public float BlinkLeft = 0;
    [Range(0f, 1f)] public float BlinkRight = 0;
    [Range(0f, 1f)] public float Blush = 0;     
    [Range(0f, 1f)] public float Cry = 0;       
    [Range(0f, 1f)] public float Dark = 0;      
    [Range(0f, 1f)] public float Dirty = 0;     
    [Range(0f, 1f)] public float Dizzy = 0;     
    [Range(0f, 1f)] public float Extra = 0;     
    [Range(0f, 1f)] public float Flushed = 0;   
    [Range(0f, 1f)] public float Fun = 0;
    [Range(0f, 1f)] public float Joy = 0;
    [Range(0f, 1f)] public float Scary = 0;
    [Range(0f, 1f)] public float Sorrow = 0;
    [Range(0f, 1f)] public float Surprised = 0;
    [Range(0f, 1f)] public float Sweat = 0;
    [Range(0f, 1f)] public float Weep = 0;
    [Range(0f, 1f)] public float EyesWide = 0;
    [Range(0f, 1f)] public float EyesClosedHappy = 0;
    [Range(0f, 1f)] public float MouthGrin = 0;
    [Range(0f, 1f)] public float IrisShrink = 0;
    [Range(0f, 1f)] public float Snear = 0;
    [Range(0f, 1f)] public float Smile = 0;
    [Range(0f, 1f)] public float Worried = 0;

    public GameObject Bag;

    // Warning messages about clips that were not found (read only).
    [Tooltip("Blendshape clips that could not be found in the blend shape clip directory for this character")]
    //[TextArea(5, 10)]
    public string ClipsNotLoaded = "";

    public AlansBlendShapeClip()
    {
    }

    void Awake()
    {
        ForceReload = true;
        UpdateBlendShapeClips();
    }

    void OnValidate()
    {
        UpdateBlendShapeClips();
    }

    public void LateUpdate()
    {
        UpdateBlendShapeClips();
    }

    public void UpdateBlendShapeClips()
    {
        // If not initialized yet or directory has changed, force it to reload.
        if (ForceReload || m_merger == null || blendShapeClipDirectory != m_lastBlendShapeClipDirectory)
        {
            ForceReload = false;
            m_lastBlendShapeClipDirectory = blendShapeClipDirectory;
            m_blendShapeClipDirectoryPath = null;
            LoadAllBlendShapeClips();
            m_vowels = GetComponent<AlansBlendShapeClipVowels>();
        }

        Accumulate(new BlendShapeKey(BlendShapePreset.Neutral), Neutral);

        // If the current object has a AlansBlendShapeClipVowels component attached (used when talking), max with it's vowels.
        Accumulate(new BlendShapeKey(BlendShapePreset.A), (m_vowels == null) ? A : Mathf.Max(A, m_vowels.A));
        Accumulate(new BlendShapeKey(BlendShapePreset.E), (m_vowels == null) ? E : Mathf.Max(E, m_vowels.E));
        Accumulate(new BlendShapeKey(BlendShapePreset.I), (m_vowels == null) ? I : Mathf.Max(I, m_vowels.I));
        Accumulate(new BlendShapeKey(BlendShapePreset.O), (m_vowels == null) ? O : Mathf.Max(O, m_vowels.O));
        Accumulate(new BlendShapeKey(BlendShapePreset.U), (m_vowels == null) ? U : Mathf.Max(U, m_vowels.U));

        Accumulate(new BlendShapeKey(BlendShapePreset.Angry), Angry);
        Accumulate(new BlendShapeKey("AngryVein"), AngryVein);
        Accumulate(new BlendShapeKey("BagStraps"), BagStraps);
        if (Bag != null) Bag.SetActive(BagStraps > 0.5f);
        Accumulate(new BlendShapeKey("Blank"), Blank);
        Accumulate(new BlendShapeKey(BlendShapePreset.Blink), Blink);
        Accumulate(new BlendShapeKey(BlendShapePreset.Blink_L), BlinkLeft);
        Accumulate(new BlendShapeKey(BlendShapePreset.Blink_R), BlinkRight);
        FourLevels(Blush, "Blush25", "Blush50", "Blush75", "Blush99");
        Accumulate(new BlendShapeKey("Cry"), Cry);
        Accumulate(new BlendShapeKey("Dark"), Dark);
        Accumulate(new BlendShapeKey("Dirty"), Dirty);
        Accumulate(new BlendShapeKey("Dizzy"), Dizzy);
        Accumulate(new BlendShapeKey("Extra"), Extra);
        FourLevels(Flushed, "Flushed25", "Flushed50", "Flushed75", "Flushed99");
        Accumulate(new BlendShapeKey(BlendShapePreset.Fun), Fun);
        Accumulate(new BlendShapeKey(BlendShapePreset.Joy), Joy);
        Accumulate(new BlendShapeKey("Scary"), Scary);
        Accumulate(new BlendShapeKey(BlendShapePreset.Sorrow), Sorrow);
        Accumulate(new BlendShapeKey("Surprised"), Surprised);
        Accumulate(new BlendShapeKey("Sweat"), Sweat);
        Accumulate(new BlendShapeKey("Weep"), Weep);
        Accumulate(new BlendShapeKey("EyesWide"), EyesWide);
        Accumulate(new BlendShapeKey("EyesClosedHappy"), EyesClosedHappy);
        Accumulate(new BlendShapeKey("MouthGrin"), MouthGrin);
        Accumulate(new BlendShapeKey("IrisShrink"), IrisShrink);
        Accumulate(new BlendShapeKey("Snear"), Snear);
        Accumulate(new BlendShapeKey("Smile"), Smile);
        Accumulate(new BlendShapeKey("Worried"), Worried);

        m_merger.Apply();
    }

    private void Accumulate(BlendShapeKey key, float value)
    {
        m_merger.AccumulateValue(key, value);
    }

    private void FourLevels(float value, string level25, string level50, string level75, string level100)
    {
        float v25 = 0f, v50 = 0f, v75 = 0f, v100 = 0f;
        if (value > 0.875f) v100 = 1f;
        else if (value > 0.625f) v75 = 1f;
        else if (value > 0.375f) v50 = 1f;
        else if (value > 0.125f) v25 = 1f;
        m_merger.AccumulateValue(new BlendShapeKey(level25), v25);
        m_merger.AccumulateValue(new BlendShapeKey(level50), v50);
        m_merger.AccumulateValue(new BlendShapeKey(level75), v75);
        m_merger.AccumulateValue(new BlendShapeKey(level100), v100);
    }

    private void LoadAllBlendShapeClips()
    {
        m_clips.Clear();
        ClipsNotLoaded = "";

        LoadClip("BlendShape.Neutral.asset");
        LoadClip("BlendShape.A.asset");
        LoadClip("BlendShape.E.asset");
        LoadClip("BlendShape.I.asset");
        LoadClip("BlendShape.O.asset");
        LoadClip("BlendShape.U.asset");
        LoadClip("BlendShape.Angry.asset");
        LoadClip("FaceTile.AngryVein.asset");
        LoadClip("ClothesTile.BagStraps.asset");
        LoadClip("EyeTile.Blank.asset");
        LoadClip("BlendShape.Blink.asset");
        LoadClip("BlendShape.Blink_L.asset");
        LoadClip("BlendShape.Blink_R.asset");
        LoadClip("FaceTile.Blush25.asset");
        LoadClip("FaceTile.Blush50.asset");
        LoadClip("FaceTile.Blush75.asset");
        LoadClip("FaceTile.Blush99.asset");
        LoadClip("FaceTile.Cry.asset");
        LoadClip("FaceTile.Dark.asset");
        LoadClip("FaceTile.Dirty.asset");
        LoadClip("EyeTile.Dizzy.asset");
        LoadClip("BlendShape.Extra.asset");
        LoadClip("FaceTile.Flushed25.asset");
        LoadClip("FaceTile.Flushed50.asset");
        LoadClip("FaceTile.Flushed75.asset");
        LoadClip("FaceTile.Flushed99.asset");
        LoadClip("BlendShape.Fun.asset");
        LoadClip("BlendShape.Joy.asset");
        LoadClip("FaceTile.Scary.asset");
        LoadClip("BlendShape.Sorrow.asset");
        LoadClip("Blendshape.Surprised.asset");
        LoadClip("FaceTile.Weep.asset");
        LoadClip("FaceTile.Sweat.asset");
        LoadClip("Face.EyesWide.asset");
        LoadClip("Face.EyesClosedHappy.asset");
        LoadClip("Face.MouthGrin.asset");
        LoadClip("Face.IrisShrink.asset");
        LoadClip("Face.Snear.asset");
        LoadClip("Face.Smile.asset");
        LoadClip("Face.Worried.asset");

        m_merger = new BlendShapeMerger(m_clips, transform);
    }

    private BlendShapeClip LoadClip(string name)
    {
        if (blendShapeClipDirectory == null)
        {
            Debug.Log("Blend Shape Clip Directory for " + name + " of " + gameObject.name + " - must be set to Asset folder holding blendshape clips");
            return null;
        }

        if (m_blendShapeClipDirectoryPath == null)
        {
            m_blendShapeClipDirectoryPath = AssetDatabase.GetAssetPath(blendShapeClipDirectory);
            if (m_blendShapeClipDirectoryPath == null)
            {
                Debug.Log("Failed to determine path for " + name + " from asset folder in Blend Shape Clip Directory");
                return null;
            }
        }

        string path = m_blendShapeClipDirectoryPath + "/" + name;
        var clip = AssetDatabase.LoadAssetAtPath<BlendShapeClip>(path);

        if (clip == null)
        {
            //Debug.Log("Warning: Blendshape clip '" + path + "' not found or could not be loaded.");
            ClipsNotLoaded += name + " ";
            return null;
        }

        m_clips.Add(clip);
        return clip;
    }
}
