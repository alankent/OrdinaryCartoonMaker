using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AlansBlendShapeClipVowels : MonoBehaviour
{
    [Range(0f, 1f)] public float A;
    [Range(0f, 1f)] public float E;
    [Range(0f, 1f)] public float I;
    [Range(0f, 1f)] public float O;
    [Range(0f, 1f)] public float U;

    private AlansBlendShapeClip absc;

    // Start is called before the first frame update
    void Start()
    {
        absc = GetComponent<AlansBlendShapeClip>();
    }

    // Gets called from talk track when it adjusts weights.
    public void UpdateBlendShapeClips()
    {
        absc?.UpdateBlendShapeClips();        
    }
}
