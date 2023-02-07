using System;
using System.Collections.Generic;
using UnityEngine;


namespace UniHumanoid
{
    public static class BvhAnimation
    {
        class CurveSet
        {
            BvhNode Node;
            Func<float, float, float, Quaternion> EulerToRotation;
            public CurveSet(BvhNode node)
            {
                Node = node;
            }

            public ChannelCurve PositionX;
            public ChannelCurve PositionY;
            public ChannelCurve PositionZ;
            public Vector3 GetPosition(int i)
            {
                return new Vector3(
                    PositionX.Keys[i],
                    PositionY.Keys[i],
                    PositionZ.Keys[i]);
            }

            public ChannelCurve RotationX;
            public ChannelCurve RotationY;
            public ChannelCurve RotationZ;
            public Quaternion GetRotation(int i)
            {
                if (EulerToRotation == null)
                {
                    EulerToRotation = Node.GetEulerToRotation();
                }
                return EulerToRotation(
                    RotationX.Keys[i],
                    RotationY.Keys[i],
                    RotationZ.Keys[i]
                    );
            }

            static void AddCurve(Bvh bvh, AnimationClip clip, ChannelCurve ch, float scaling)
            {
                if (ch == null) return;
                var pathWithProp = default(Bvh.PathWithProperty);
                bvh.TryGetPathWithPropertyFromChannel(ch, out pathWithProp);
                var curve = new AnimationCurve();
                for (int i = 0; i < bvh.FrameCount; ++i)
                {
                    var time = (float)(i * bvh.FrameTime.TotalSeconds);
                    var value = ch.Keys[i] * scaling;
                    curve.AddKey(time, value);
                }
                clip.SetCurve(pathWithProp.Path, typeof(Transform), pathWithProp.Property, curve);
            }

            public void AddCurves(Bvh bvh, AnimationClip clip, float scaling)
            {
                AddCurve(bvh, clip, PositionX, -scaling);
                AddCurve(bvh, clip, PositionY, scaling);
                AddCurve(bvh, clip, PositionZ, scaling);

                var pathWithProp = default(Bvh.PathWithProperty);
                bvh.TryGetPathWithPropertyFromChannel(RotationX, out pathWithProp);

                // rotation
                var curveX = new AnimationCurve();
                var curveY = new AnimationCurve();
                var curveZ = new AnimationCurve();
                var curveW = new AnimationCurve();
                for (int i = 0; i < bvh.FrameCount; ++i)
                {
                    var time = (float)(i * bvh.FrameTime.TotalSeconds);
                    var q = GetRotation(i).ReverseX();
                    curveX.AddKey(time, q.x);
                    curveY.AddKey(time, q.y);
                    curveZ.AddKey(time, q.z);
                    curveW.AddKey(time, q.w);
                }
                clip.SetCurve(pathWithProp.Path, typeof(Transform), "localRotation.x", curveX);
                clip.SetCurve(pathWithProp.Path, typeof(Transform), "localRotation.y", curveY);
                clip.SetCurve(pathWithProp.Path, typeof(Transform), "localRotation.z", curveZ);
                clip.SetCurve(pathWithProp.Path, typeof(Transform), "localRotation.w", curveW);
            }
        }

        public static AnimationClip CreateAnimationClip(Bvh bvh, float scaling, bool humanoidClip)
        {
            var clip = new AnimationClip();

            clip.legacy = true;

            var curveMap = new Dictionary<BvhNode, CurveSet>();

            int j = 0;
            foreach (var node in bvh.Root.Traverse())
            {
                var set = new CurveSet(node);
                curveMap[node] = set;

                for (int i = 0; i < node.Channels.Length; ++i, ++j)
                {
                    var curve = bvh.Channels[j];
                    switch (node.Channels[i])
                    {
                        case Channel.Xposition: set.PositionX = curve; break;
                        case Channel.Yposition: set.PositionY = curve; break;
                        case Channel.Zposition: set.PositionZ = curve; break;
                        case Channel.Xrotation: set.RotationX = curve; break;
                        case Channel.Yrotation: set.RotationY = curve; break;
                        case Channel.Zrotation: set.RotationZ = curve; break;
                        default: throw new Exception();
                    }
                }
            }

            if (!humanoidClip)
            {
                // Copy all curves into the animation clip.
                foreach (var set in curveMap)
                {
                    set.Value.AddCurves(bvh, clip, scaling);
                }
            }
            else
            {
                // We go through the humanoid skeleton structure, pulling out what we can, what we think is useful.
                clip.legacy = false;
                ExtractHumanoidCurves(bvh, clip, curveMap);
            }

            clip.EnsureQuaternionContinuity();

            return clip;
        }

        // Useful blog: https://blog.unity.com/technology/mecanim-humanoids
        // Elbow: Z-Axis = stretch, X-Axis = roll in/out, Y not used.

        struct MuscleDetails
        {
            public string Name;
            public float Min;
            public float Mid;
            public float Max;
        }

        struct BoneToMuscles
        {
            public MuscleDetails XAxis;
            public MuscleDetails YAxis;
            public MuscleDetails ZAxis;
        };

        // TODO: Jaw Close, Jaw Left-Right
        // TODO: IK movements of left/right hand/feet, body (root motion) and body (hips)
        // Left/Right Q/T for feet, hands,

        private static Dictionary<string, string> synonyms = new Dictionary<string, string>
        {
            { "J_Bip_C_Chest", "Chest" },
            { "J_Bip_C_Spine", "Spine" },
            { "J_Bip_C_UpperChest", "UpperChest" },
            { "J_Bip_L_UpperLeg", "LeftUpperLeg" },
            { "J_Bip_L_LowerLeg", "LeftLowerLeg" },
            { "J_Bip_L_Foot", "LeftFoot" },
            { "J_Bip_L_ToeBase", "LeftToes" },
            { "J_Bip_C_Neck", "Neck" },
            { "J_Bip_C_Head", "Head" },
            { "J_Bip_L_Shoulder", "LeftShoulder" },
            { "J_Adj_L_FaceEye", "LeftEye" },
            { "J_Adj_R_FaceEye", "RightEye" },
            { "J_Bip_L_UpperArm", "LeftUpperArm" },
            { "J_Bip_L_LowerArm", "LeftLowerArm" },
            { "J_Bip_L_Hand", "LeftHand" },
            { "J_Bip_L_Index1", "LeftIndexProximal" },
            { "J_Bip_L_Index2", "LeftIndexIntermediate" },
            { "J_Bip_L_Index3", "LeftIndexDistral" },
            { "J_Bip_L_Little1", "LeftLittleProximal" },
            { "J_Bip_L_Little2", "LeftLittleIntermediate" },
            { "J_Bip_L_Little3", "LeftLittleDistral" },
            { "J_Bip_L_Middle1", "LeftMiddleProximal" },
            { "J_Bip_L_Middle2", "LeftMiddleIntermediate" },
            { "J_Bip_L_Middle3", "LeftMiddleDistral" },
            { "J_Bip_L_Ring1", "LeftRingProximal" },
            { "J_Bip_L_Ring2", "LeftRingIntermediate" },
            { "J_Bip_L_Ring3", "LeftRingDistral" },
            { "J_Bip_L_Thumb1", "LeftThumbProximal" },
            { "J_Bip_L_Thumb2", "LeftThumbIntermediate" },
            { "J_Bip_L_Thumb3", "LeftThumbDistral" },
        };

        private static Dictionary<string, BoneToMuscles> boneMappings = new Dictionary<string, BoneToMuscles> {
            { "Chest", new BoneToMuscles {
                XAxis = { Name = "Chest Front-Back", Min = 40, Mid = 0, Max = -40 },
                YAxis = { Name = "Chest Left-Right", Min = -40, Mid = 0, Max = 40 },
                ZAxis = { Name = "Chest Twist Left-Right", Min = 40, Mid = 0, Max = -40 }
            } },
            { "Spine", new BoneToMuscles {
                XAxis = { Name = "Spine Front-Back", Min = 40, Mid = 0, Max = -40 },
                YAxis = { Name = "Spine Twist Left-Right", Min = -40, Mid = 0, Max = 40 },
                ZAxis = { Name = "Spine Left-Right", Min = 40, Mid = 0, Max = -40 },
            } },
            { "UpperChest", new BoneToMuscles {
                XAxis = { Name = "Chest Front-Back", Min = 20, Mid = 0, Max = -20 },
                YAxis = { Name = "Chest Twist Left-Right", Min = -20, Mid = 0, Max = 20 },
                ZAxis = { Name = "Chest Left-Right", Min = 20, Mid = 0, Max = -20 },
            } },
            { "LeftUpperLeg", new BoneToMuscles {
                XAxis = { Name = "Left Upper Leg Front-Back", Min = -120, Mid = -30, Max = 20 },
                YAxis = { Name = "Left Upper Leg Twist In-Out", Min = -35, Mid = 0, Max = 35 },
                ZAxis = { Name = "Left Upper Leg In-Out", Min = -60, Mid = 0, Max = 60 },
            } },
            { "LeftLowerLeg", new BoneToMuscles {
                XAxis = { Name = "Left Lower Leg Stretch", Min = 160, Mid = 80, Max = 0 },
                YAxis = { Name = "Left Lower Leg Twist In-Out", Min = -45, Mid = 0, Max = 45 }
            } },
            { "LeftFoot", new BoneToMuscles {
                XAxis = { Name = "Left Foot Twist In-Out", Min = -50, Mid = 0, Max = 50 },
                ZAxis = { Name = "Left Foot Up-Down", Min = 30, Mid = 0, Max = -30 },
            } },
            { "LeftToes", new BoneToMuscles {
                XAxis = { Name = "Left Toes Up-Down", Min = -50, Mid = 0, Max = 50 },
            } },
            { "RightUpperLeg", new BoneToMuscles {
                XAxis = { Name = "Right Upper Leg Front-Back", Min = -120, Mid = -30, Max = 20 },
                YAxis = { Name = "Right Upper Leg Twist In-Out", Min = 35, Mid = 0, Max = -35 },
                ZAxis = { Name = "Right Upper Leg In-Out", Min = 60, Mid = 0, Max = -60 },
            } },
            { "RightLowerLeg", new BoneToMuscles {
                XAxis = { Name = "Right Lower Leg Stretch", Min = 160, Mid = 80, Max = 0 },
                YAxis = { Name = "Right Lower Leg Twist In-Out", Min = 45, Mid = 0, Max = -45 }
            } },
            { "RightFoot", new BoneToMuscles {
                XAxis = { Name = "Right Foot Twist In-Out", Min = -50, Mid = 0, Max = 50 },
                ZAxis = { Name = "Right Foot Up-Down", Min = 30, Mid = 0, Max = -30 },
            } },
            { "RightToes", new BoneToMuscles {
                XAxis = { Name = "Right Toes Up-Down", Min = -50, Mid = 0, Max = 50 },
            } },
            { "Neck", new BoneToMuscles {
                XAxis = { Name = "Neck Nod Down-Up", Min = 40, Mid = 0, Max = -40 },
                YAxis = { Name = "Neck Turn Left-Right", Min = -40, Mid = 0, Max = 40 },
                ZAxis = { Name = "Neck Tilt Left-Right", Min = 40, Mid = 0, Max = -40 },
            } },
            { "Head", new BoneToMuscles {
                XAxis = { Name = "Head Nod Down-Up", Min = 40, Mid = 0, Max = -40 },
                YAxis = { Name = "Head Turn Left-Right", Min = -40, Mid = 0, Max = 40 },
                ZAxis = { Name = "Head Tilt Left-Right", Min = 40, Mid = 0, Max = -40 },
                // TODO "Jaw Close" and "Jaw Left-Right" would go on a jaw bone
            } },
            { "LeftShoulder", new BoneToMuscles {
                YAxis = { Name = "Left Shoulder Front-Back", Min = 14, Mid = 0, Max = -14 },
                ZAxis = { Name = "Left Shoulder Down-Up", Min = 15, Mid = 0, Max = -30 },
            } },
            { "LeftEye", new BoneToMuscles {
                XAxis = { Name = "Left Eye Down-Up", Min = 10, Mid = 0, Max = -15 },
                YAxis = { Name = "Left Eye In-Out", Min = 10, Mid = 0, Max = -15 },
            } },
            { "RightEye", new BoneToMuscles {
                //XAxis = { Name = "Right Eye Down-Up", Min = -90, Max = 90 },
                //YAxis = { Name = "Right Eye In-Out", Min = -90, Max = 90 },
            } },
            { "LeftUpperArm", new BoneToMuscles {
                XAxis = { Name = "Left Arm Front-Back", Min = -60, Mid = 0, Max = 60 },
                YAxis = { Name = "Left Arm Twist In-Out", Min = 90, Mid = 40, Max = -10 }, //
                ZAxis = { Name = "Left Arm Down-Up", Min = 100, Mid = 30, Max = -60 },
            } },
            { "LeftLowerArm", new BoneToMuscles {
                XAxis = { Name = "Left Forearm Twist In-Out", Min = 40, Mid = 0, Max = -40 },
                YAxis = { Name = "Left Forearm Stretch", Min = 160, Mid = 80, Max = 0 },
            } },
            { "LeftHand", new BoneToMuscles {
                YAxis = { Name = "Left Hand In-Out", Min = 40, Mid = 0, Max = -40 },
                ZAxis = { Name = "Left Hand Down-Up", Min = 80, Mid = 0, Max = -80 },
            } },
            { "LeftIndexProximal", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Index.Spread", Min = -10, Mid = 7.5f, Max = 25 },
                ZAxis = { Name = "LeftHand.Index.1 Stretched", Min = 80, Mid = 30, Max = -20 },
            } },
            { "LeftIndexIntermediate", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Index.2 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftIndexDistral", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Index.3 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftLittleProximal", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Little.Spread", Min = -10, Mid = 7.5f, Max = 25 },
                ZAxis = { Name = "LeftHand.Little.1 Stretched", Min = 80, Mid = 30, Max = -20 },
            } },
            { "LeftLittleIntermediate", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Little.2 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftLittleDistral", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Little.3 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftMiddleProximal", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Middle.Spread", Min = -10, Mid = 7.5f, Max = 25 },
                ZAxis = { Name = "LeftHand.Middle.1 Stretched", Min = 80, Mid = 30, Max = -20 },
            } },
            { "LeftMiddleIntermediate", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Middle.2 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftMiddleDistral", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Middle.3 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftRingProximal", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Ring.Spread", Min = -10, Mid = 7.5f, Max = 25 },
                ZAxis = { Name = "LeftHand.Ring.1 Stretched", Min = 80, Mid = 30, Max = -20 },
            } },
            { "LeftRingIntermediate", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Ring.2 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftRingDistral", new BoneToMuscles {
                ZAxis = { Name = "LeftHand.Ring.3 Stretched", Min = 80, Mid = 35, Max = -10 },
            } },
            { "LeftThumbProximal", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Thumb.1 Stretched", Min = 80, Mid = 30, Max = -20 },
                ZAxis = { Name = "LeftHand.Thumb.Spread", Min = 30, Mid = 14, Max = -5 },
            } },
            { "LeftThumbIntermediate", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Thumb.2 Stretched", Min = -60, Mid = -20, Max = 10 },
            } },
            { "LeftThumbDistral", new BoneToMuscles {
                YAxis = { Name = "LeftHand.Thumb.3 Stretched", Min = -60, Mid = -20, Max = 10 },
            } },
        };

        private static void ExtractHumanoidCurves(Bvh bvh, AnimationClip clip, Dictionary<BvhNode, CurveSet> curveMap)
        {
            TestCurveNormalization();

            // (Root|LeftFoot|RightFoot|LeftHand|RightHand){T.x/y/z, Q.x/y/z/w}  -- root and hands/feet IK.
            // Muscle (Spine Front-Back, Spine Left-Right, Spine Twist Left-Right, Chest Front-Back, Chest Left-Right, ...)
            var fps = (float)(1f / bvh.FrameTime.TotalSeconds);

            // Root position.
            {
                var root = bvh.Root;
                if (curveMap.ContainsKey(root))
                {
                    var set = curveMap[root];

                    var curveX = MakeCurve(set.PositionX, fps);
                    var curveY = MakeCurve(set.PositionY, fps);
                    var curveZ = MakeCurve(set.PositionZ, fps);
                    clip.SetCurve("", typeof(Animator), "RootT.x", curveX);
                    clip.SetCurve("", typeof(Animator), "RootT.y", curveY);
                    clip.SetCurve("", typeof(Animator), "RootT.z", curveZ);

                    // TODO: Probably need to turn Euler angles into Quanternion angles.
                    var rotX = MakeCurve(set.RotationX, fps);
                    var rotY = MakeCurve(set.RotationY, fps);
                    var rotZ = MakeCurve(set.RotationZ, fps);
                    var rotW = MakeZeroCurve();
                    clip.SetCurve("", typeof(Animator), "RootQ.x", rotX);
                    clip.SetCurve("", typeof(Animator), "RootQ.y", rotY);
                    clip.SetCurve("", typeof(Animator), "RootQ.z", rotZ);
                    clip.SetCurve("", typeof(Animator), "RootQ.w", rotW);
                }
            }

            // Compute parent/child relationships (BvhNode does not have a parent pointer)
            var parentLookup = new Dictionary<BvhNode, BvhNode>();
            WalkBvhTree(parentLookup, bvh.Root, bvh.Root);

            var DebugTargetBone = "LeftLowerLeg";
            var DebugTargetFrame = 100;

            // Go through all the bones and see which ones have muscles defined for them.
            foreach (var node in bvh.Root.Traverse())
            {
                string name = node.Name;

                if (synonyms.ContainsKey(name))
                {
                    name = synonyms[name];
                }

                if (boneMappings.ContainsKey(name))
                {
                    var muscle = boneMappings[name];
                    var set = curveMap[node];
                    var parentSet = curveMap[parentLookup[node]];

                    // Debug
                    if (name == DebugTargetBone) Debug.Log(DebugTargetBone + ": parent=" + ParentRotationToString(parentSet, DebugTargetFrame) + " me=" + ParentRotationToString(set, DebugTargetFrame));

                    if (muscle.XAxis.Name != null)
                    {
                        var curve = MakeMuscleCurve(muscle.XAxis, set.RotationX, parentSet.RotationX, fps);
                        clip.SetCurve("", typeof(Animator), muscle.XAxis.Name, curve);
                        if (name == DebugTargetBone) Debug.Log(DebugTargetBone + ": X-curve=" + curve[DebugTargetFrame].value);
                    }
                    if (muscle.YAxis.Name != null)
                    {
                        var curve = MakeMuscleCurve(muscle.YAxis, set.RotationY, parentSet.RotationY, fps);
                        clip.SetCurve("", typeof(Animator), muscle.YAxis.Name, curve);
                        if (name == DebugTargetBone) Debug.Log(DebugTargetBone + ": Y-curve=" + curve[DebugTargetFrame].value);
                    }
                    if (muscle.ZAxis.Name != null)
                    {
                        var curve = MakeMuscleCurve(muscle.ZAxis, set.RotationZ, parentSet.RotationZ, fps);
                        clip.SetCurve("", typeof(Animator), muscle.ZAxis.Name, curve);
                        if (name == DebugTargetBone) Debug.Log(DebugTargetBone + ": Z-curve=" + curve[DebugTargetFrame].value);
                    }
                }
            }
        }

        private static string ParentRotationToString(CurveSet set, int frame)
        {
            return "(" + set.RotationX.Keys[frame] + "," + set.RotationY.Keys[frame] + "," + set.RotationZ.Keys[frame] + ")";
        }

        private static void WalkBvhTree(Dictionary<BvhNode, BvhNode> map, BvhNode parent, BvhNode node)
        {
            map[node] = parent;
            foreach (var child in node.Children)
            {
                WalkBvhTree(map, node, child);
            }
        }

        private static AnimationCurve MakeCurve(ChannelCurve curve, float fps)
        {
            float frameDuration = 1f / fps;
            var animCurve = new AnimationCurve();
            for (int frame = 0; frame < curve.Keys.Length; frame++)
            {
                var key = new Keyframe();
                key.time = frame * frameDuration;
                key.value = curve.Keys[frame];
                // TODO lots of weighted settings that I don't understand yet
                animCurve.AddKey(key);
            }

            return animCurve;
        }

        private static AnimationCurve MakeMuscleCurve(MuscleDetails muscle, ChannelCurve data, ChannelCurve parentData, float fps)
        {
            float frameDuration = 1f / fps;
            var animCurve = new AnimationCurve();
            for (int frame = 0; frame < data.Keys.Length; frame++)
            {
                var key = new Keyframe();
                key.time = frame * frameDuration;
// TODO: I AM WONDERING IF ROTATIONS ARE WORLD SPACE, NOT LOCAL, SO NOT RELATIVE TO PARENT, MESSING THINGS UP FOR ME.
                key.value = NormalizeMuscleStrength(muscle, data.Keys[frame] /*- parentData.Keys[frame]*/);
                // TODO lots of weighted settings that I don't understand yet
                animCurve.AddKey(key);
            }

            return animCurve;
        }

        private static void TestCurveNormalization()
        {
            {
                var muscle = new MuscleDetails { Name = "Test -100 to 100", Min = -100, Mid = 0, Max = 100 };
                CheckMuscle(muscle, 0, 0);
                CheckMuscle(muscle, 10, 0.1f);
                CheckMuscle(muscle, 100, 1);
                CheckMuscle(muscle, -10, -0.1f);
                CheckMuscle(muscle, -100, -1);
            }
            {
                var muscle = new MuscleDetails { Name = "Test 100 to -100", Min = 100, Mid = 0, Max = -100 };
                CheckMuscle(muscle, 0, 0);
                CheckMuscle(muscle, 10, -0.1f);
                CheckMuscle(muscle, 100, -1);
                CheckMuscle(muscle, -10, 0.1f);
                CheckMuscle(muscle, -100, 1);
            }
            {
                var muscle = new MuscleDetails { Name = "Test -50 to 150", Min = -50, Mid = 50, Max = 150 };
                CheckMuscle(muscle, 0, -0.5f);
                CheckMuscle(muscle, 50, 0);
                CheckMuscle(muscle, 60, 0.1f);
                CheckMuscle(muscle, 150, 1);
                CheckMuscle(muscle, 40, -0.1f);
                CheckMuscle(muscle, -50, -1);
            }
            {
                var muscle = new MuscleDetails { Name = "Test 150 to -50", Min = 150, Mid = 50, Max = -50 };
                CheckMuscle(muscle, 0, 0.5f);
                CheckMuscle(muscle, 50, 0);
                CheckMuscle(muscle, 60, -0.1f);
                CheckMuscle(muscle, 150, -1);
                CheckMuscle(muscle, 40, 0.1f);
                CheckMuscle(muscle, -50, 1);
            }
        }

        private static void CheckMuscle(MuscleDetails muscle, float input, float wantedResult)
        {
            var actualResult = NormalizeMuscleStrength(muscle, input);
            if (actualResult != wantedResult) Debug.Log("WRONG! " + muscle.Name + " input=" + input + " result=" + actualResult + " WANTED=" + wantedResult);
        }

        private static float NormalizeMuscleStrength(MuscleDetails muscle, float value)
        {
            // -1=>Min, 0=>Mid, 1=>Max
            if ((muscle.Min < muscle.Max && value >= muscle.Mid) || (muscle.Min >= muscle.Max && value < muscle.Mid))
            {
                return (value - muscle.Mid) / (muscle.Max - muscle.Mid);
            }
            else
            {
                return (value - muscle.Mid) / (muscle.Mid - muscle.Min);
            }
        }

        // Zero is the only value.
        private static AnimationCurve MakeZeroCurve()
        {
            var animCurve = new AnimationCurve();

            var key = new Keyframe();
            key.time = 0;
            key.value = 0;
            // TODO lots of weighted settings that I don't understand yet
            animCurve.AddKey(key);

            return animCurve;
        }
    }
}
