using System.Collections;
using System.Collections.Generic;
using UniGLTF;
using UnityEngine;
using VRM;

// This component goes on VRoid character root for animating where the eyes look at.
// The eyes can look between different targets or at specified left/right/up/down position.
// The idea is this scripts controls the position of a separate target object that must be created in the scene that the VRM look at scripts then specify as their look at target.
// That is, this does not modify what the eyes look at directly, but rather moves a target object that that eyes must look at.
//
[ExecuteAlways]
public class AlansEyesLookAt : MonoBehaviour
{
    public Transform computedLookAtTarget;
    public Transform character;
    public Transform neck;
    public Transform head;
    public Transform leftEye;
    public Transform rightEye;

    [Range(0f, 1f)] public float headTurnStrength = 0f; // Does not work very well in combination with animation clip head turns
    [Range(0f, 10f)] public float headTurnSpeed = 5f;

    [Range(-2f, 2f)] public float vertical;
    [Range(-2f, 2f)] public float horizontal;

    public Transform target1;
    [Range(0f, 1f)] public float target1Lerp;

    public Transform target2;
    [Range(0f, 1f)] public float target2Lerp;

    public Transform target3;
    [Range(0f,1f)] public float target3Lerp;

    public Vector3 maxHeadTurn;
    public Vector3 maxEyeTurn;
    public float eyeRotationFactor;

    public Vector3 neckRotationOffset;


    // Keep track of what *we* think current head rotation is (other things can compete to update this)
    private Quaternion currentHeadRotation;

    public void Awake()
    {
        currentHeadRotation = transform.rotation;
        OnValidate();
    }

    public void LateUpdate()
    {
        if (computedLookAtTarget == null) return;
        Recompute();
        TurnHeadTowardsTarget();
        TurnEyesTowardsTarget();
    }

    public void OnValidate()
    {
        // These magic numbers work well for VRoid characters generally
        if (maxHeadTurn.x <= 0f) maxHeadTurn.x = 20f;
        if (maxHeadTurn.y <= 0f) maxHeadTurn.y = 45f;
        if (maxHeadTurn.z <= 0f) maxHeadTurn.z = 2f;
        if (maxEyeTurn.x <= 0f) maxEyeTurn.x = 10f;
        if (maxEyeTurn.y <= 0f) maxEyeTurn.y = 15f;
        if (maxEyeTurn.z <= 0f) maxEyeTurn.z = 2f;
        if (eyeRotationFactor <= 0f) eyeRotationFactor = 0.2f;

        // Default to the current character, but allow it to be a proxy object to make animated clip editing easier using UMotion.
        if (character == null)
        {
            character = transform;
        }
    }

    void Recompute()
    {
        computedLookAtTarget.position = TargetPoint();
    }

    // Use lerp's etc of various targets to work out what we should be looking at.
    private Vector3 TargetPoint()
    {
        if (target1 == null) target1Lerp = 0;
        if (target2 == null) target2Lerp = 0;
        if (target3 == null) target3Lerp = 0;

        if (neck == null)
        {
            // Bone structure used by VRoid Studio
            neck = character.Find("Root/J_Bip_C_Hips/J_Bip_C_Spine/J_Bip_C_Chest/J_Bip_C_UpperChest/J_Bip_C_Neck");
        }
        if (head == null)
        {
            // Bone structure used by VRoid Studio
            head = neck.Find("J_Bip_C_Head");
        }
        if (leftEye == null)
        {
            // Bone structure used by VRoid Studio
            leftEye = head.Find("J_Adj_L_FaceEye");
        }
        if (rightEye == null)
        {
            // Bone structure used by VRoid Studio
            rightEye = head.Find("J_Adj_R_FaceEye");
        }
        Vector3 betweenEyes = (leftEye.position + rightEye.position) / 2f;

        Vector3 ahead = betweenEyes + character.forward;

        // Work out the target point based on the relative weights for the targets.
        float totalLerp = target1Lerp + target2Lerp + target3Lerp;
        Vector3 averagedLookAt = Vector3.zero;
        if (totalLerp > 0f)
        {
            Vector3 targetPosition = Vector3.zero;
            if (target1 != null && target1Lerp > 0f)
            {
                targetPosition += target1.position * (target1Lerp / totalLerp);
            }
            if (target2 != null && target2Lerp > 0f)
            {
                targetPosition += target2.position * (target2Lerp / totalLerp);
            }
            if (target3 != null && target3Lerp > 0f)
            {
                targetPosition += target3.position * (target3Lerp / totalLerp);
            }

            // Use the total, so if animating from one lerp to another, both at 50% is treated as 100% looking at midway between the two targets.
            float attentionLerp = target1Lerp + target2Lerp + target3Lerp;
            if (attentionLerp > 1f) attentionLerp = 1f;

            averagedLookAt = Vector3.Lerp(ahead, targetPosition, attentionLerp) - ahead;
        }

        // Looking at other objects - make horiz/vert an offset from the other objects (not projected forward on Z access)
        Vector3 delta = character.TransformPoint(new Vector3(horizontal, vertical, 0f)) - character.position;

        // Return the point we should be looking at.
        return ahead + averagedLookAt + delta;
    }

    // Turns the head towards the target, slowly. Heads cannot move fast.
    private void TurnHeadTowardsTarget()
    {
        // Let the animation clip win if strength is zero.
        if (headTurnStrength == 0) return;

        Vector3 headTarget = Vector3.Lerp(head.position + character.forward, computedLookAtTarget.position, headTurnStrength);

        // Based on https://answers.unity.com/questions/862380/how-to-slow-down-transformlookat.html
        Vector3 relativePos = headTarget - head.position;
        Quaternion toRotation = Quaternion.LookRotation(relativePos);
        currentHeadRotation = Quaternion.Lerp(currentHeadRotation, toRotation, headTurnSpeed * Time.deltaTime);
        var fullRotation = currentHeadRotation; // ClampRotation(currentHeadRotation, character.rotation.eulerAngles - maxHeadTurn, character.rotation.eulerAngles + maxHeadTurn);
        var halfRotation = Quaternion.Lerp(character.rotation, fullRotation, 0.5f);
        neck.rotation = halfRotation * Quaternion.Euler(neckRotationOffset);
        head.rotation = fullRotation;
 
        //neck.localRotation = ClampRotation(neck.localRotation, maxHeadTurn);
        //head.localRotation = ClampRotation(head.localRotation, maxHeadTurn);
    }

    // Turn eyes towards the target, instantly. Unlike the head, there is no delay as eyes move much faster than the head.
    private void TurnEyesTowardsTarget()
    {
        /*
        Vector3 relativePos = computedLookAtTarget.position - head.position;
        //Quaternion toRotation = ClampRotation(Quaternion.LookRotation(relativePos), character.rotation.eulerAngles - maxEyeTurn, character.rotation.eulerAngles + maxEyeTurn);
        Quaternion toRotation = Quaternion.LookRotation(relativePos);
        leftEye.rotation = toRotation;
        rightEye.rotation = toRotation;
        */

        // Theoretically this is correct, but it looks "off" in practice - eye turns too much.
        //leftEye.rotation = Quaternion.LookRotation(computedLookAtTarget.position - leftEye.position);
        //rightEye.rotation = Quaternion.LookRotation(computedLookAtTarget.position - rightEye.position);

        // Look in direction, but not as strong as computed as pupil within eye looks "off" visually.
        leftEye.rotation = Quaternion.Lerp(head.rotation, Quaternion.LookRotation(computedLookAtTarget.position - leftEye.position), eyeRotationFactor);
        rightEye.rotation = Quaternion.Lerp(head.rotation, Quaternion.LookRotation(computedLookAtTarget.position - rightEye.position), eyeRotationFactor);

        // Limit max movement of eyes.
        leftEye.localRotation = ClampRotation(leftEye.localRotation, maxEyeTurn);
        rightEye.localRotation = ClampRotation(rightEye.localRotation, maxEyeTurn);
    }

    // From https://forum.unity.com/threads/how-do-i-clamp-a-quaternion.370041/ (talks about several approaches)
    private static Quaternion ClampRotation(Quaternion q, Vector3 bounds)
    {
        if (bounds == null) return q;

        q.x /= q.w;
        q.y /= q.w;
        q.z /= q.w;
        q.w = 1.0f;

        float angleX = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.x);
        angleX = Mathf.Clamp(angleX, -bounds.x, bounds.x);
        q.x = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleX);

        float angleY = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.y);
        angleY = Mathf.Clamp(angleY, -bounds.y, bounds.y);
        q.y = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleY);

        float angleZ = 2.0f * Mathf.Rad2Deg * Mathf.Atan(q.z);
        angleZ = Mathf.Clamp(angleZ, -bounds.z, bounds.z);
        q.z = Mathf.Tan(0.5f * Mathf.Deg2Rad * angleZ);

        return q.normalized;
    }
}
