using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UltraGripEditor
{
    // Retargets a reference hand pose onto a humanoid avatar.
    public class AvatarHand
    {
        private class Bone
        {
            public Transform reference;
            public Transform target;

            public Quaternion referenceFrame;

            public Quaternion targetFrameInv;
        }

        // Shrink the unused body while keeping its skinning hierarchy intact.
        private const float BodyScale = 0.0001f;

        public GameObject root;
        private Transform body;
        private Transform hand;
        private Transform referenceHand;
        private Transform referenceMiddle;
        private Quaternion referenceHandFrame;
        private Quaternion handFrameInv;
        private Transform lowerArm;
        private Transform referenceHandBase;

        private Quaternion lowerArmFrameInv;

        private float forearmLength;

        private Vector3 palmCentre;

        private readonly List<Bone> bones = new List<Bone>();

        public static AvatarHand Create(GameObject avatar, GripVisualizer.HandReference reference, bool left, Transform parent, string name)
        {
            GameObject root = new GameObject(name);
            root.hideFlags = HideFlags.DontSave;
            root.transform.SetParent(parent, false);

            GameObject instance = Object.Instantiate(avatar, root.transform);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

            Animator animator = instance.GetComponentInChildren<Animator>();
            if(animator == null || !animator.isHuman)
            {
                Object.DestroyImmediate(root);
                return null;
            }

            AvatarHand avatarHand = new AvatarHand { root = root, body = instance.transform };
            if(!avatarHand.Bind(animator, reference, left))
            {
                Object.DestroyImmediate(root);
                return null;
            }

            // Components whose scripts fail to load come back null
            foreach(Behaviour behaviour in instance.GetComponentsInChildren<Behaviour>(true))
                if(behaviour != null) behaviour.enabled = false;
            foreach(Collider collider in instance.GetComponentsInChildren<Collider>(true))
                collider.enabled = false;
            foreach(SkinnedMeshRenderer skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skin.updateWhenOffscreen = true;

            // LOD switching can hide the hand once the body is shrunk.
            foreach(LODGroup lodGroup in instance.GetComponentsInChildren<LODGroup>(true))
            {
                LOD[] lods = lodGroup.GetLODs();
                for(int i = 1; i < lods.Length; i++)
                {
                    foreach(Renderer renderer in lods[i].renderers)
                        if(renderer != null) renderer.enabled = false;
                }
                lodGroup.enabled = false;
            }

            avatarHand.hand.SetParent(root.transform, true);
            if(avatarHand.lowerArm != null) avatarHand.lowerArm.SetParent(root.transform, true);
            avatarHand.body.localScale = Vector3.one * BodyScale;

            foreach(Transform t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSave;

            return avatarHand;
        }

        private bool Bind(Animator animator, GripVisualizer.HandReference reference, bool left)
        {
            int side = left ? 0 : 1;
            hand = animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Transform index = HumanBone(animator, side, HumanBodyBones.LeftIndexProximal);
            Transform middle = HumanBone(animator, side, HumanBodyBones.LeftMiddleProximal);
            Transform little = HumanBone(animator, side, HumanBodyBones.LeftLittleProximal);
            if(hand == null || index == null || middle == null || little == null) return false;

            referenceHand = reference.hand;
            referenceMiddle = reference.middle1;

            Func<Transform, Matrix4x4> targetRest = RestPose(animator.GetComponentsInChildren<SkinnedMeshRenderer>(true), hand);
            Func<Transform, Matrix4x4> referenceRest = RestPose(reference.handMesh.GetComponentsInChildren<SkinnedMeshRenderer>(true), reference.hand);

            Quaternion targetHand = HandFrame(targetRest, hand, index, middle, little, left);
            handFrameInv = Quaternion.Inverse(Quaternion.Inverse(targetRest(hand).rotation) * targetHand);
            Quaternion referenceHandRest = HandFrame(referenceRest, reference.hand, reference.index1, reference.middle1, reference.pinky1, left);
            referenceHandFrame = Quaternion.Inverse(referenceRest(reference.hand).rotation) * referenceHandRest;
            palmCentre = hand.InverseTransformPoint((hand.position + middle.position) * 0.5f);

            lowerArm = animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            referenceHandBase = reference.handBase;
            if(lowerArm != null)
            {
                Matrix4x4 lowerArmRest = targetRest(lowerArm);
                Vector3 forearm = (Vector3)targetRest(hand).GetColumn(3) - (Vector3)lowerArmRest.GetColumn(3);
                forearmLength = forearm.magnitude;
                Quaternion lowerArmFrame = Quaternion.LookRotation(forearm, targetHand * Vector3.up);
                lowerArmFrameInv = Quaternion.Inverse(Quaternion.Inverse(lowerArmRest.rotation) * lowerArmFrame);
            }

            Hand target = new Hand { rest = targetRest, palm = targetHand * Vector3.down, little = little };
            Hand referenceSide = new Hand { rest = referenceRest, palm = referenceHandRest * Vector3.down, little = reference.pinky1 };

            AddFinger(animator, side, HumanBodyBones.LeftThumbProximal, true, target, referenceSide, reference.thumb1, reference.thumb2, reference.thumb3);
            AddFinger(animator, side, HumanBodyBones.LeftIndexProximal, false, target, referenceSide, reference.index1, reference.index2, reference.index3);
            AddFinger(animator, side, HumanBodyBones.LeftMiddleProximal, false, target, referenceSide, reference.middle1, reference.middle2, reference.middle3);
            AddFinger(animator, side, HumanBodyBones.LeftRingProximal, false, target, referenceSide, reference.ring1, reference.ring2, reference.ring3);
            AddFinger(animator, side, HumanBodyBones.LeftLittleProximal, false, target, referenceSide, reference.pinky1, reference.pinky2, reference.pinky3);
            return true;
        }

        private struct Hand
        {
            public Func<Transform, Matrix4x4> rest;
            public Vector3 palm;
            public Transform little;
        }

        // unity places the right finger bones 15 enum values after the left ones
        private static Transform HumanBone(Animator animator, int side, HumanBodyBones leftBone)
        {
            return animator.GetBoneTransform(leftBone + side * 15);
        }

        private void AddFinger(Animator animator, int side, HumanBodyBones leftProximal, bool thumb, Hand target, Hand referenceSide, Transform ref1, Transform ref2, Transform ref3)
        {
            Transform[] references = { ref1, ref2, ref3 };
            Transform[] targets = new Transform[3];
            for(int i = 0; i < 3; i++)
                targets[i] = HumanBone(animator, side, leftProximal + i);

            Vector3 targetDir = Vector3.zero;
            Vector3 referenceDir = Vector3.zero;
            for(int i = 0; i < 3; i++)
            {
                if(targets[i] == null || references[i] == null) break;

                Transform targetChild = i < 2 ? targets[i + 1] : null;
                if(targetChild == null && targets[i].childCount > 0) targetChild = targets[i].GetChild(0);
                Transform referenceChild = i < 2 ? references[i + 1] : null;
                if(referenceChild == null && references[i].childCount > 0) referenceChild = references[i].GetChild(0);

                Quaternion? targetFrame = BoneFrame(target, targets[i], targetChild, thumb, ref targetDir);
                Quaternion? referenceFrame = BoneFrame(referenceSide, references[i], referenceChild, thumb, ref referenceDir);
                if(targetFrame == null || referenceFrame == null) break;

                bones.Add(new Bone
                {
                    reference = references[i],
                    target = targets[i],
                    referenceFrame = referenceFrame.Value,
                    targetFrameInv = Quaternion.Inverse(targetFrame.Value),
                });
            }
        }

        // Reuse the previous segment direction when the tip bone has no child.
        private static Quaternion? BoneFrame(Hand side, Transform bone, Transform child, bool thumb, ref Vector3 dir)
        {
            Matrix4x4 rest = side.rest(bone);
            Vector3 position = rest.GetColumn(3);
            if(child != null) dir = ((Vector3)side.rest(child).GetColumn(3) - position).normalized;
            if(dir == Vector3.zero) return null;

            // Thumb flexion needs a different axis from the other fingers.
            Vector3 toward = thumb ? (Vector3)side.rest(side.little).GetColumn(3) - position : side.palm;
            Vector3 hinge = Vector3.Cross(dir, toward);
            if(hinge == Vector3.zero) return null;

            return Quaternion.Inverse(rest.rotation) * Quaternion.LookRotation(dir, hinge);
        }

        // Use the same hand axes on both rigs: Z toward the fingers, Y toward the back of the hand.
        private static Quaternion HandFrame(Func<Transform, Matrix4x4> rest, Transform hand, Transform index, Transform middle, Transform little, bool left)
        {
            Vector3 handPosition = rest(hand).GetColumn(3);
            Vector3 forward = (Vector3)rest(middle).GetColumn(3) - handPosition;
            Vector3 across = (Vector3)rest(index).GetColumn(3) - (Vector3)rest(little).GetColumn(3);
            Vector3 palm = Vector3.Cross(forward, across) * (left ? -1 : 1);
            return Quaternion.LookRotation(forward, -palm);
        }

        // Read bind poses from the renderer containing the hand.
        private static Func<Transform, Matrix4x4> RestPose(SkinnedMeshRenderer[] skins, Transform anchor)
        {
            Dictionary<Transform, Matrix4x4> rest = new Dictionary<Transform, Matrix4x4>();
            foreach(SkinnedMeshRenderer skin in skins)
            {
                int anchorIndex = Array.IndexOf(skin.bones, anchor);
                if(skin.sharedMesh == null || anchorIndex < 0) continue;

                Matrix4x4[] bindposes = skin.sharedMesh.bindposes;

                if(anchorIndex >= bindposes.Length || bindposes[anchorIndex].isIdentity) continue;
                
                for(int i = 0; i < skin.bones.Length && i < bindposes.Length; i++)
                {
                    if(skin.bones[i] != null && !rest.ContainsKey(skin.bones[i]))
                        rest[skin.bones[i]] = skin.transform.localToWorldMatrix * bindposes[i].inverse;
                }

                break;
            }

            Matrix4x4 Get(Transform t)
            {
                if(rest.TryGetValue(t, out Matrix4x4 matrix)) return matrix;
                matrix = t.parent == null ? t.localToWorldMatrix : Get(t.parent) * Matrix4x4.TRS(t.localPosition, t.localRotation, t.localScale);
                rest[t] = matrix;
                return matrix;
            }
            return Get;
        }

        public void Apply()
        {
            if(hand == null) return;

            hand.rotation = referenceHand.rotation * referenceHandFrame * handFrameInv;
            Vector3 referencePalm = (referenceHand.position + referenceMiddle.position) * 0.5f;
            hand.position += referencePalm - hand.TransformPoint(palmCentre);

            foreach(Bone bone in bones)
                bone.target.rotation = bone.reference.rotation * bone.referenceFrame * bone.targetFrameInv;

            if(lowerArm == null)
            {
                body.position = hand.position;
                return;
            }

            Vector3 forearm = (referenceHand.position - referenceHandBase.position).normalized;
            Vector3 back = referenceHand.rotation * referenceHandFrame * Vector3.up;
            lowerArm.rotation = Quaternion.LookRotation(forearm, back) * lowerArmFrameInv;
            lowerArm.position = hand.position - forearm * forearmLength;
            body.position = lowerArm.position;
        }
    }
}
