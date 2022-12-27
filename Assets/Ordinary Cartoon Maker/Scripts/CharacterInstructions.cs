using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CharacterInstructions : MonoBehaviour
{
    public GameObject CharacterPrefab; // Must be a prefab?
    public AnimationClip BodyClip; // Important
    public AnimationClip LeftHandClip; // Optional
    public AnimationClip RightHandClip; // Optional
    public AnimationClip FacialExpressionClip; // Optional
}
