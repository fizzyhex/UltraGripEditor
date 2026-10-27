using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using SLZ.Marrow;
using SLZ.Marrow.Utilities;
using UltraGripEditor;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class GripVisualizer : MonoBehaviour
{
    public class PoseDataReference
    {
        public HandPose.PoseData poseData;

        public PoseDataReference(HandPose.PoseData data)
        {
            this.poseData = data;
        }
    }

    public enum SelectedHand
    {
        Left,
        Right
    }

    public enum InterpolationMode
    {
        Index,
        Interpolated,
    }

    [System.Serializable]
    public class HandReference
    {
        public GameObject handMesh;
        public Transform handBase;
        public Transform hand;
        public Transform index1;
        public Transform index2;
        public Transform index3;
        public Transform middle1;
        public Transform middle2;
        public Transform middle3;
        public Transform ring1;
        public Transform ring2;
        public Transform ring3;
        public Transform pinky1;
        public Transform pinky2;
        public Transform pinky3;
        public Transform thumb1;
        public Transform thumb2;
        public Transform thumb3;
        public SimpleTransform handBaseToHand;

        private Transform[] _moveableBoneList;
        public Transform[] MoveableBoneList
        {
            get
            {
                if(_moveableBoneList == null)
                {
                    _moveableBoneList = new Transform[]
                    {
                        hand, index1, index2, index3, middle1, middle2, middle3, ring1, ring2, ring3, pinky1, pinky2, pinky3, thumb1, thumb2, thumb3
                    };
                }
                return _moveableBoneList;
            }
        }
    }

    public bool show = true;
    public TargetGrip targetGrip;
    public PoseDataReference poseDataOverride { get; set; }
    public HandPose.PoseDataGroup CurrentPoseDataGroup
    {
        get
        {
            if(targetGrip == null) return default;
            if(targetGrip.handPose == null) return default;
            if(radiusSelectionMode == InterpolationMode.Index)
                return targetGrip.handPose.poseData[radiusIndex];
            else
                return PoseLerper.GetPoseGroupAtRadius(targetGrip.handPose, targetGrip.radius);
        }
    }
    public HandPose.PoseData CurrentPoseData
    {
        get
        {
            if(poseDataOverride != null) return poseDataOverride.poseData;
            return CurrentPoseDataGroup.poseArray[pryIndex];
        }
    }

    public Transform gripTarget
    {
        get
        {
            if(targetGrip == null) return null;
            if(targetGrip.targetTransform == null) return targetGrip.transform;
            return targetGrip.targetTransform;
        }
    }

    public SelectedHand viewingHand;
    
    public InterpolationMode radiusSelectionMode = InterpolationMode.Index;

    public int radiusIndex;
    public int pryIndex;

    public HandReference rightHandReferences;
    public HandReference leftHandReferences;
    public HandReference viewingHandReferences => viewingHand == SelectedHand.Left ? leftHandReferences : rightHandReferences;

    private const string LeftAvatarName = "Avatar Hand (Left)";
    private const string RightAvatarName = "Avatar Hand (Right)";

    public static GameObject PreviewAvatar { get; private set; }

    private GameObject avatarSource;
    private bool avatarSynced;
    private bool validating;
    private AvatarHand leftAvatarHand;
    private AvatarHand rightAvatarHand;

    public static void SetPreviewAvatar(GameObject avatar)
    {
        PreviewAvatar = avatar;
        foreach(GripVisualizer visualizer in FindObjectsOfType<GripVisualizer>())
            visualizer.UpdateFingers();
    }

    public void SetPoseData(int groupIndex, int dataIndex, HandPose.PoseData data)
    {
        if(targetGrip == null) return;
        if(targetGrip.handPose == null) return;

#if UNITY_EDITOR
        Undo.RecordObject(targetGrip.handPose, "Edit HandPose");
        targetGrip.handPose.poseData[groupIndex].poseArray[dataIndex] = data;
        EditorUtility.SetDirty(targetGrip.handPose);
        AssetDatabase.SaveAssets();
#endif
    }

    public void SetPoseData(HandPose.PoseData data)
    {
        SetPoseData(radiusIndex, pryIndex, data);
    }

    void SetActiveHands(bool left, bool right)
    {
        if(!validating) SyncAvatarHands();

        leftHandReferences.handMesh.SetActive(left && leftAvatarHand == null);
        rightHandReferences.handMesh.SetActive(right && rightAvatarHand == null);
        leftAvatarHand?.root.SetActive(left);
        rightAvatarHand?.root.SetActive(right);
    }

    void SyncAvatarHands()
    {
        if(avatarSynced && avatarSource == PreviewAvatar) return;
        avatarSynced = true;
        avatarSource = PreviewAvatar;

        // Clear hands from before domain reloads
        DestroyChildren(LeftAvatarName);
        DestroyChildren(RightAvatarName);
        leftAvatarHand = null;
        rightAvatarHand = null;
        if(avatarSource == null) return;

        leftAvatarHand = AvatarHand.Create(avatarSource, leftHandReferences, true, transform, LeftAvatarName);
        rightAvatarHand = AvatarHand.Create(avatarSource, rightHandReferences, false, transform, RightAvatarName);
    }

    // World offset of the viewed hand from where its pose places it
    public Vector3 HandOffset => gripTarget != null ? LargeSphereOffset(gripTarget, viewingHand == SelectedHand.Left) : Vector3.zero;

    private static readonly FieldInfo SphereMaxRadius = typeof(SphereGrip).GetField("_maxRadius", BindingFlags.NonPublic | BindingFlags.Instance);

    // Radius above _maxRadius, along secondaryMovementAxis * primaryMovementAxis
    Vector3 LargeSphereOffset(Transform target, bool left)
    {
        if(!(targetGrip is SphereGrip sphereGrip)) return Vector3.zero;

        float maxRadius = SphereMaxRadius != null ? (float)SphereMaxRadius.GetValue(sphereGrip) : 0f;
        float excess = Mathf.Max(sphereGrip.radius - maxRadius, 0f);
        Vector3 tertiary = Vector3.Cross(sphereGrip.secondaryMovementAxis, sphereGrip.primaryMovementAxis).normalized;
        return target.rotation * tertiary * (left ? -excess : excess);
    }

    void DestroyChildren(string name)
    {
        for(Transform child = transform.Find(name); child != null; child = transform.Find(name))
            DestroyImmediate(child.gameObject);
    }

    void OnValidate()
    {
        validating = true;
        UpdateFingers();
        validating = false;
    }

    [ContextMenu("Update Vis")]
    public void UpdateFingers()
    {
        if(!show) { SetActiveHands(false, false); return; }
        if(targetGrip == null) { SetActiveHands(false, false); return; }
        if(targetGrip.handPose == null) { SetActiveHands(false, false); return; }

        Transform gripTarget = targetGrip.targetTransform;
        if(gripTarget == null) gripTarget = targetGrip.transform;
        HandPose handPose = targetGrip.handPose;

        radiusIndex = Mathf.Clamp(radiusIndex, 0, handPose.poseData.Length - 1);
        pryIndex = Mathf.Clamp(pryIndex, 0, handPose.poseData[radiusIndex].poseArray.Length - 1);

        HandPose.PoseData selectedPry = CurrentPoseData;

        bool viewingLeft = viewingHand == SelectedHand.Left;

        SetActiveHands(viewingLeft, !viewingLeft);

        var handle = viewingLeft ? selectedPry.leftHandle : selectedPry.rightHandle;
        var artHandle = viewingLeft ? selectedPry.leftArtHandle : selectedPry.rightArtHandle;

        artHandle = handle;

        SimpleTransform target = SimpleTransform.Create(gripTarget);
        Quaternion handToArtRot = viewingLeft ? Quaternion.Euler(-90, 90, 0) : Quaternion.Euler(90, -90, 0);
        SimpleTransform artHandInWorld = target.Transform(artHandle.inverse).Transform(Vector3.zero, handToArtRot);

        HandReference visualHand = viewingLeft ? leftHandReferences : rightHandReferences;
        SimpleTransform handBaseInWorld = artHandInWorld.Transform(visualHand.handBaseToHand.inverse);
        handBaseInWorld.position += LargeSphereOffset(gripTarget, viewingLeft);

        visualHand.handBase.SetPositionAndRotation(handBaseInWorld.position, handBaseInWorld.rotation);

        visualHand.index1.localRotation = selectedPry.index1;
        visualHand.index2.localRotation = Quaternion.AngleAxis(selectedPry.index2, Vector3.forward);
        visualHand.index3.localRotation = Quaternion.AngleAxis(selectedPry.index3, Vector3.forward);

        visualHand.middle1.localRotation = selectedPry.middle1;
        visualHand.middle2.localRotation = Quaternion.AngleAxis(selectedPry.middle2, Vector3.forward);
        visualHand.middle3.localRotation = Quaternion.AngleAxis(selectedPry.middle3, Vector3.forward);

        visualHand.ring1.localRotation = selectedPry.ring1;
        visualHand.ring2.localRotation = Quaternion.AngleAxis(selectedPry.ring2, Vector3.forward);
        visualHand.ring3.localRotation = Quaternion.AngleAxis(selectedPry.ring3, Vector3.forward);

        visualHand.pinky1.localRotation = selectedPry.pinky1;
        visualHand.pinky2.localRotation = Quaternion.AngleAxis(selectedPry.pinky2, Vector3.forward);
        visualHand.pinky3.localRotation = Quaternion.AngleAxis(selectedPry.pinky3, Vector3.forward);

        visualHand.thumb1.localRotation = selectedPry.thumb1;
        visualHand.thumb2.localRotation = Quaternion.AngleAxis(selectedPry.thumb2, Vector3.forward);
        visualHand.thumb3.localRotation = Quaternion.AngleAxis(selectedPry.thumb3, Vector3.forward);

        (viewingLeft ? leftAvatarHand : rightAvatarHand)?.Apply();
    }
}
