using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using SLZ.Marrow;
using SLZ.Marrow.Utilities;
using System;
using UnityEditor.Overlays;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using SLZ.Marrow.Warehouse;
using UltraGripEditor;

using static GripVisualizer;

[Overlay(typeof(SceneView), id: ID_OVERLAY_HANDPOSEEDITOR, displayName: "Hand Pose Editor")]
public class HandPoseEditorOverlay : Overlay, ITransientOverlay
{
    private const string ID_OVERLAY_HANDPOSEEDITOR = "hand-pose-editor-overlay";
    private string VISUALTREE_PATH = AssetDatabase.GUIDToAssetPath("2859e1c9b07c64a178cb6eba348ff0bf");
    private string VISUALIZER_PATH = AssetDatabase.GUIDToAssetPath("bf692f29001bd42aab4da2220b211fb0");
    private TargetGrip lastSelectedTargetGrip;
    private GameObject visualizerObject;
    private GripVisualizer visualizer;

    private int overlayMode = 0;
    private SelectedHand selectedHand = SelectedHand.Left;
    private int radiusIndex = 0;
    private int pryIndex = 0;
    private VisualElement visualizerGroup;
    private VisualElement avatarGroup;
    private Label avatarStatus;
    private PropertyField avatarField;
    private AvatarPreviewTarget avatarTarget;
    private VisualElement editorGroup;
    private VisualElement editOff;
    private VisualElement editOn;
    private SliderInt radiusSlider;
    private SliderInt prySlider;
    private GameObject editingSelectedObject;
    private PoseDataReference currentEditingPose;
    private Transform currentEditingBone;
    private Quaternion globalHandleRotation = Quaternion.identity;
    private int dragControl;
    private Vector3 dragStartPosition;
    private Vector3 dragPosition;
    private Quaternion dragStartRotation;
    private Quaternion dragRotation;
    private float dragStartAngle;

    // Holds the last recorded state of currentEditingPose
    private HandPoseEditTarget editTarget;

    private bool _lastVisible = false;
    public bool visible
    {
        get
        {
            if (Selection.activeGameObject != null)
            {
                TargetGrip potentialGrip = Selection.activeGameObject.GetComponent<TargetGrip>();
                if (potentialGrip != null && potentialGrip.handPose != null)
                {
                    if(!_lastVisible || potentialGrip != lastSelectedTargetGrip)
                    {
                        lastSelectedTargetGrip = potentialGrip;
                        OnGripSelected(lastSelectedTargetGrip);
                    }
                    OnUpdated(true);
                    _lastVisible = true;
                    return true;
                }
            }
            OnUpdated(false);
            if(_lastVisible) OnGripDeselected();
            _lastVisible = false;
            return false;
        }
    }

    private void OnSceneGUI(SceneView sceneView)
    {
        if(visualizer == null) return;
        if(visualizer.targetGrip == null) return;
        if(overlayMode == 2) DrawEditorHandles();
    }

    void DrawEditorHandles()
    {
        if(currentEditingPose != null)
        {
            if(editingSelectedObject != null)
                Selection.activeGameObject = editingSelectedObject;

            Transform[] transforms = visualizer.viewingHandReferences.MoveableBoneList;

            Event e = Event.current;

            int controlId = GUIUtility.GetControlID(FocusType.Passive);

            if (e.type == EventType.Layout)
            {
                // Makes this tool the default thing that receives mouse clicks
                HandleUtility.AddDefaultControl(controlId);
            }

            EditorGUI.BeginChangeCheck();

            foreach(Transform bone in transforms)
            {
                float boneHandleSize = HandleUtility.GetHandleSize(bone.position);

                if(bone != currentEditingBone)
                {
                    float handleSize = boneHandleSize * 0.15f;
                    Handles.color = Color.green;
                    if(Handles.Button(bone.position, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
                    {
                        currentEditingBone = bone;
                    }
                }
                else
                {
                    // This bone is SELECTED. #Goated
                    float handleSize = boneHandleSize * 0.25f;
                    Handles.color = Color.red;
                    if(Handles.Button(bone.position, Quaternion.identity, handleSize, handleSize, Handles.SphereHandleCap))
                    {
                        currentEditingBone = null;
                        continue;
                    }

                    if(bone == visualizer.viewingHandReferences.hand)
                    {
                        DrawHandHandle();
                    }
                    else if(bone == visualizer.viewingHandReferences.index1)
                    {
                        DrawBoneRotationHandle(bone, ref currentEditingPose.poseData.index1);
                    }
                    else if(bone == visualizer.viewingHandReferences.middle1)
                    {
                        DrawBoneRotationHandle(bone, ref currentEditingPose.poseData.middle1);
                    }
                    else if(bone == visualizer.viewingHandReferences.ring1)
                    {
                        DrawBoneRotationHandle(bone, ref currentEditingPose.poseData.ring1);
                    }
                    else if(bone == visualizer.viewingHandReferences.pinky1)
                    {
                        DrawBoneRotationHandle(bone, ref currentEditingPose.poseData.pinky1);
                    }
                    else if(bone == visualizer.viewingHandReferences.thumb1)
                    {
                        DrawBoneRotationHandle(bone, ref currentEditingPose.poseData.thumb1);
                    }
                    else if(bone == visualizer.viewingHandReferences.index2)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.index2, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.index3)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.index3, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.middle2)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.middle2, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.middle3)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.middle3, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.ring2)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.ring2, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.ring3)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.ring3, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.pinky2)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.pinky2, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.pinky3)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.pinky3, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.thumb2)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.thumb2, bone, boneHandleSize);
                    }
                    else if(bone == visualizer.viewingHandReferences.thumb3)
                    {
                        Handles.color = Color.blue;
                        DrawAngleHandle(ref currentEditingPose.poseData.thumb3, bone, boneHandleSize);
                    }
                }
            }

            if(EditorGUI.EndChangeCheck())
            {
                RecordEdit("Edit Hand Pose");
            }
        }
    }

    void RecordEdit(string name)
    {
        if(editTarget == null || currentEditingPose == null) return;
        if(editTarget.poseData.Equals(currentEditingPose.poseData)) return;

        Undo.RecordObject(editTarget, name);
        editTarget.poseData = currentEditingPose.poseData;
    }

    void OnUndoRedo()
    {
        if(editTarget == null || currentEditingPose == null) return;
        if(editTarget.poseData.Equals(currentEditingPose.poseData)) return;

        currentEditingPose.poseData = editTarget.poseData;
        visualizer.UpdateFingers();
        SceneView.RepaintAll();
    }

    // Handle rotations return their absolute value since drag start, so the global handle keeps its rotation until the drag ends
    Quaternion GetHandleRotation(Quaternion rotation)
    {
        if(GUIUtility.hotControl == 0) globalHandleRotation = Quaternion.identity;
        return Tools.pivotRotation == PivotRotation.Global ? globalHandleRotation : rotation;
    }

    Quaternion ApplyHandleRotation(Quaternion rotation, Quaternion handleRotation, Quaternion newHandleRotation)
    {
        if(Tools.pivotRotation == PivotRotation.Global) globalHandleRotation = newHandleRotation;
        return Quaternion.Normalize(newHandleRotation * Quaternion.Inverse(handleRotation) * rotation);
    }

    bool IsDragging => dragControl != 0 && GUIUtility.hotControl == dragControl;

    void BeginDrag(Vector3 position, Quaternion rotation)
    {
        dragControl = GUIUtility.hotControl;
        dragStartPosition = position;
        dragStartRotation = rotation;
    }

    void DrawHandHandle()
    {
        Transform hand = visualizer.viewingHandReferences.hand;
        bool dragging = IsDragging;
        Vector3 position = dragging ? dragPosition : hand.position;
        Quaternion rotation = dragging ? dragRotation : hand.rotation;
        Quaternion handleRotation = GetHandleRotation(rotation);
        Quaternion newHandleRotation = handleRotation;
        Vector3 newPosition = position;

        EditorGUI.BeginChangeCheck();
        Handles.TransformHandle(ref newPosition, ref newHandleRotation);
        if(!EditorGUI.EndChangeCheck()) return;

        if(!dragging) BeginDrag(position, rotation);
        dragPosition = newPosition;
        dragRotation = ApplyHandleRotation(rotation, handleRotation, newHandleRotation);

        Vector3 offset = visualizer.HandOffset;
        Vector3 posePosition = dragPosition - offset;
        Quaternion poseRotation = dragRotation;
        if(HandPoseSnapping.Enabled)
        {
            SimpleTransform gripTarget = SimpleTransform.Create(visualizer.gripTarget);
            posePosition = HandPoseSnapping.SnapPosition(gripTarget, dragStartPosition - offset, posePosition, HandPoseSnapping.Move);
            poseRotation = HandPoseSnapping.SnapRotation(dragStartRotation, poseRotation, HandPoseSnapping.Rotate);
        }

        HandleConversion.HandleConfiguration resultingHandle = HandleConversion.WorldToGripHandle(visualizer.targetGrip, SimpleTransform.Create(posePosition, poseRotation), visualizer.viewingHand);
        if(visualizer.viewingHand == SelectedHand.Left)
        {
            currentEditingPose.poseData.leftHandle = resultingHandle.handle;
            currentEditingPose.poseData.invLeftHandle = resultingHandle.invHandle;
            currentEditingPose.poseData.leftArtHandle = resultingHandle.artHandle;
        }
        else
        {
            currentEditingPose.poseData.rightHandle = resultingHandle.handle;
            currentEditingPose.poseData.invRightHandle = resultingHandle.invHandle;
            currentEditingPose.poseData.rightArtHandle = resultingHandle.artHandle;
        }
    }

    void DrawBoneRotationHandle(Transform bone, ref Quaternion localRotation)
    {
        bool dragging = IsDragging;
        Quaternion rotation = dragging ? dragRotation : bone.rotation;
        Quaternion handleRotation = GetHandleRotation(rotation);

        EditorGUI.BeginChangeCheck();
        Quaternion newHandleRotation = Handles.RotationHandle(handleRotation, bone.position);
        if(!EditorGUI.EndChangeCheck()) return;

        if(!dragging) BeginDrag(bone.position, rotation);
        dragRotation = ApplyHandleRotation(rotation, handleRotation, newHandleRotation);

        Quaternion snapped = HandPoseSnapping.Enabled ? HandPoseSnapping.SnapRotation(dragStartRotation, dragRotation, HandPoseSnapping.Rotate) : dragRotation;
        localRotation = Quaternion.Inverse(bone.parent.rotation) * snapped;
        bone.localRotation = localRotation;
    }

    // Angle in degrees about the bone's forward axis, wrapped to [-180, 180]
    void DrawAngleHandle(ref float angle, Transform bone, float size)
    {
        bool dragging = IsDragging;
        Quaternion rotation = dragging ? dragRotation : bone.rotation;
        Vector3 axis = bone.forward;

        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.Disc(rotation, bone.position, axis, size, false, 0f);
        if(!EditorGUI.EndChangeCheck()) return;

        if(!dragging)
        {
            BeginDrag(bone.position, rotation);
            dragStartAngle = angle;
        }
        dragRotation = newRotation;

        float delta = HandPoseSnapping.SignedAngle(dragStartRotation, newRotation, axis);
        if(HandPoseSnapping.Enabled) delta = HandPoseSnapping.Snap(delta, HandPoseSnapping.Rotate);
        angle = Mathf.DeltaAngle(0f, dragStartAngle + delta);
    }

    private void OnGripSelected(TargetGrip grip)
    {
        if(visualizerObject == null) CreateVisualizer();
        visualizer.targetGrip = grip;

        radiusIndex = Math.Clamp(radiusIndex, 0, visualizer.targetGrip.handPose.poseData.Length - 1);
        pryIndex = Math.Clamp(pryIndex, 0, visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray.Length - 1);

        if(radiusSlider != null)
        {
            radiusSlider.highValue = visualizer.targetGrip.handPose.poseData.Length - 1;
            radiusSlider.value = radiusIndex;
        }
        if(prySlider != null)
        {
            prySlider.highValue = visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray.Length - 1;
            prySlider.value = pryIndex;
        }
    }

    private void OnGripDeselected()
    {
        visualizer.targetGrip = null;
        visualizer.UpdateFingers();
    }

    private void OnUpdated(bool isGripSelected)
    {
        if(isGripSelected) visualizer.UpdateFingers();
    }

    public override VisualElement CreatePanelContent()
    {
        VisualTreeAsset visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(VISUALTREE_PATH);
        VisualElement tree = visualTree.Instantiate();
        VisualElement rootVisualElement = new VisualElement();

        DropdownField modeDropdown = tree.Q<DropdownField>("ModeDropdown");
        modeDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
           overlayMode = modeDropdown.index;
           visualizer.show = overlayMode != 0;
           UpdatePanelContent();
        });
        modeDropdown.index = overlayMode;

        visualizerGroup = tree.Q<VisualElement>("VisualizeMode");

        avatarGroup = tree.Q<VisualElement>("AvatarGroup");
        avatarStatus = tree.Q<Label>("AvatarStatus");
        if(avatarTarget == null)
        {
            avatarTarget = ScriptableObject.CreateInstance<AvatarPreviewTarget>();
            avatarTarget.hideFlags = HideFlags.DontSave;
        }
        avatarTarget.avatar = new AvatarCrateReference(Barcode.IsValidString(AvatarPreview.Barcode) ? new Barcode(AvatarPreview.Barcode) : Barcode.EmptyBarcode());
        SerializedObject avatarObject = new SerializedObject(avatarTarget);
        avatarField = tree.Q<PropertyField>("AvatarCrate");
        avatarField.Bind(avatarObject);
        avatarField.TrackPropertyValue(avatarObject.FindProperty("avatar._barcode._id"), prop =>
        {
            AvatarPreview.Set(Barcode.IsValidString(prop.stringValue) ? prop.stringValue : "");
        });
        AvatarPreview.StatusChanged -= UpdateAvatarStatus;
        AvatarPreview.StatusChanged += UpdateAvatarStatus;
        UpdateAvatarStatus();
        
        DropdownField visHandDropdown = tree.Q<DropdownField>("VisualizeHandDropdown");
        visHandDropdown.RegisterCallback<ChangeEvent<string>>(evt =>
        {
            selectedHand = (SelectedHand)visHandDropdown.index;
            visualizer.viewingHand = selectedHand;
        });
        visHandDropdown.index = (int)selectedHand;

        prySlider = tree.Q<SliderInt>("VisualizePoseSlider");
        Label pryLabel = tree.Q<Label>("VisualizePryVector");
        prySlider.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            pryIndex = evt.newValue;
            pryLabel.text = FormatVectorInts(visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray[pryIndex].nativePry);
            SetVisualizerPry(pryIndex);
        });
        prySlider.highValue = visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray.Length - 1;
        prySlider.value = pryIndex;
        pryLabel.text = FormatVectorInts(visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray[pryIndex].nativePry);

        radiusSlider = tree.Q<SliderInt>("VisualizeRadiusSlider");
        Label radiusLabel = tree.Q<Label>("VisualizeRadiusNumber");
        radiusSlider.RegisterCallback<ChangeEvent<int>>(evt =>
        {
            radiusIndex = evt.newValue;
            radiusLabel.text = $"{visualizer.targetGrip.handPose.poseData[radiusIndex].radius}";
            SetVisualizerRadius(radiusIndex);
            if(prySlider != null)
            {
                prySlider.highValue = visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray.Length - 1;
                pryLabel.text = FormatVectorInts(visualizer.targetGrip.handPose.poseData[radiusIndex].poseArray[pryIndex].nativePry);
            }
        });
        radiusSlider.highValue = visualizer.targetGrip.handPose.poseData.Length - 1;
        radiusSlider.value = radiusIndex;
        radiusLabel.text = $"{visualizer.targetGrip.handPose.poseData[radiusIndex].radius}";

        editorGroup = tree.Q<VisualElement>("EditMode");
        editOff = tree.Q<VisualElement>("EditOff");
        editOn = tree.Q<VisualElement>("EditOn");

        Button startEditButton = tree.Q<Button>("BeginEditing");
        startEditButton.clicked += StartEditing;
        Button cancelEditButton = tree.Q<Button>("CancelEditing");
        cancelEditButton.clicked += CancelEditing;
        Button saveEditButton = tree.Q<Button>("SaveEditing");
        saveEditButton.clicked += EndEditing;

        Button editMirrorHandle = tree.Q<Button>("EditMirrorHandle");
        editMirrorHandle.clicked += MirrorHandle;

        ToolbarToggle snapToggle = tree.Q<ToolbarToggle>("EditSnapToggle");
        snapToggle.Add(new Image { image = EditorGUIUtility.IconContent(EditorGUIUtility.isProSkin ? "Snap/d_SceneViewSnap" : "Snap/SceneViewSnap").image });
        snapToggle.SetValueWithoutNotify(HandPoseSnapping.Enabled);

        FloatField snapMove = tree.Q<FloatField>("EditSnapMove");
        snapMove.SetValueWithoutNotify(HandPoseSnapping.Move);
        snapMove.RegisterValueChangedCallback(evt =>
        {
            HandPoseSnapping.Move = evt.newValue;
            snapMove.SetValueWithoutNotify(HandPoseSnapping.Move);
        });

        FloatField snapRotate = tree.Q<FloatField>("EditSnapRotate");
        snapRotate.SetValueWithoutNotify(HandPoseSnapping.Rotate);
        snapRotate.RegisterValueChangedCallback(evt =>
        {
            HandPoseSnapping.Rotate = evt.newValue;
            snapRotate.SetValueWithoutNotify(HandPoseSnapping.Rotate);
        });

        snapMove.SetEnabled(HandPoseSnapping.Enabled);
        snapRotate.SetEnabled(HandPoseSnapping.Enabled);
        snapToggle.RegisterValueChangedCallback(evt =>
        {
            HandPoseSnapping.Enabled = evt.newValue;
            snapMove.SetEnabled(evt.newValue);
            snapRotate.SetEnabled(evt.newValue);
        });

        UpdatePanelContent();

        rootVisualElement.Add(tree);
        return rootVisualElement;
    }

    void MirrorHandle()
    {
        if(currentEditingPose == null) return;

        Vector3 position = visualizer.viewingHandReferences.hand.position;
        Quaternion rotation = visualizer.viewingHandReferences.hand.rotation;

        HandleConversion.HandleConfiguration currentHandleConfig = HandleConversion.WorldToGripHandle(visualizer.targetGrip, new SimpleTransform(position - visualizer.HandOffset, rotation), visualizer.viewingHand);
        HandleConversion.HandleConfiguration flippedHandleConfig = HandleConversion.FlipHandle(currentHandleConfig);
        
        if(visualizer.viewingHand == SelectedHand.Left)
        {
            // Update current just to be safe
            currentEditingPose.poseData.leftHandle = currentHandleConfig.handle;
            currentEditingPose.poseData.invLeftHandle = currentHandleConfig.invHandle;
            currentEditingPose.poseData.leftArtHandle = currentHandleConfig.artHandle;

            // Apply flipped handle
            currentEditingPose.poseData.rightHandle = flippedHandleConfig.handle;
            currentEditingPose.poseData.invRightHandle = flippedHandleConfig.invHandle;
            currentEditingPose.poseData.rightArtHandle = flippedHandleConfig.artHandle;
        }
        else
        {
            currentEditingPose.poseData.leftHandle = flippedHandleConfig.handle;
            currentEditingPose.poseData.invLeftHandle = flippedHandleConfig.invHandle;
            currentEditingPose.poseData.leftArtHandle = flippedHandleConfig.artHandle;

            currentEditingPose.poseData.rightHandle = currentHandleConfig.handle;
            currentEditingPose.poseData.invRightHandle = currentHandleConfig.invHandle;
            currentEditingPose.poseData.rightArtHandle = currentHandleConfig.artHandle;
        }

        RecordEdit("Mirror Hand Pose Handle");
        visualizer.UpdateFingers();
    }

    void StartEditing()
    {
        PoseDataReference startingPoseData = new PoseDataReference(visualizer.CurrentPoseData);
        if(startingPoseData == null) return;

        currentEditingPose = startingPoseData;
        if(editTarget == null)
        {
            editTarget = ScriptableObject.CreateInstance<HandPoseEditTarget>();
            editTarget.hideFlags = HideFlags.DontSave;
        }
        editTarget.poseData = currentEditingPose.poseData;
        editingSelectedObject = visualizer.targetGrip.gameObject;
        visualizer.poseDataOverride = currentEditingPose;

        editOff.style.display = DisplayStyle.None;
        editOn.style.display = DisplayStyle.Flex;
    }

    void EndEditing()
    {
        visualizer.SetPoseData(currentEditingPose.poseData);

        CancelEditing();
    }

    void CancelEditing()
    {
        if(editTarget != null) Undo.ClearUndo(editTarget);

        currentEditingPose = null;
        currentEditingBone = null;
        editingSelectedObject = null;
        visualizer.poseDataOverride = null;
        editOff.style.display = DisplayStyle.Flex;
        editOn.style.display = DisplayStyle.None;
    }

    void UpdateAvatarStatus()
    {
        if(avatarStatus == null) return;
        avatarStatus.text = AvatarPreview.Status;
        avatarStatus.style.display = AvatarPreview.Status == "" ? DisplayStyle.None : DisplayStyle.Flex;
    }

    void UpdatePanelContent()
    {
        avatarGroup.style.display = overlayMode == 0 ? DisplayStyle.None : DisplayStyle.Flex;

        if (overlayMode == 0)
        {
            visualizerGroup.style.display = DisplayStyle.None;
            editorGroup.style.display = DisplayStyle.None;
        }
        else if (overlayMode == 1)
        {
            visualizerGroup.style.display = DisplayStyle.Flex;
            editorGroup.style.display = DisplayStyle.None;
        }
        else if (overlayMode == 2)
        {
            visualizerGroup.style.display = DisplayStyle.None;
            editorGroup.style.display = DisplayStyle.Flex;
        }
    }

    public override void OnCreated()
    {
        SceneView.duringSceneGui += OnSceneGUI;
        Undo.undoRedoPerformed += OnUndoRedo;
        base.OnCreated();
    }

    void CreateVisualizer()
    {
        if(visualizerObject != null) GameObject.DestroyImmediate(visualizerObject);
        GameObject visualizerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VISUALIZER_PATH);
        visualizerObject = GameObject.Instantiate(visualizerPrefab);
        visualizerObject.hideFlags = /*HideFlags.HideInHierarchy |*/ HideFlags.DontSave;
        visualizer = visualizerObject.GetComponent<GripVisualizer>();
        visualizer.show = overlayMode != 0;
        visualizer.viewingHand = selectedHand;
        SetVisualizerRadius(radiusIndex);
        SetVisualizerPry(pryIndex);
    }

    void DestroyVisualizer()
    {
        if(visualizerObject != null)
            GameObject.DestroyImmediate(visualizerObject);
    }

    void SetVisualizerRadius(int index)
    {
        if(visualizer == null) return;

        visualizer.radiusIndex = index;
        visualizer.UpdateFingers();
    }

    void SetVisualizerPry(int index)
    {
        if(visualizer == null) return;

        visualizer.pryIndex = index;
        visualizer.UpdateFingers();
    }

    public override void OnWillBeDestroyed()
    {
        AvatarPreview.StatusChanged -= UpdateAvatarStatus;
        Undo.undoRedoPerformed -= OnUndoRedo;
        if(editTarget != null)
        {
            Undo.ClearUndo(editTarget);
            ScriptableObject.DestroyImmediate(editTarget);
        }
        DestroyVisualizer();
        if(avatarTarget != null) ScriptableObject.DestroyImmediate(avatarTarget);
        base.OnWillBeDestroyed();
    }

    private string FormatVectorInts(Vector3 vec)
    {
        return $"{(int)vec.x}, {(int)vec.y}, {(int)vec.z}";
    }
}