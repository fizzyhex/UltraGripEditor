using UnityEngine;
using UnityEditor;
using SLZ.Marrow.Utilities;

namespace UltraGripEditor
{
    public static class HandPoseSnapping
    {
        private const string EnabledKey = "UltraGripEditor.Snap.Enabled";
        private const string MoveKey = "UltraGripEditor.Snap.Move";
        private const string RotateKey = "UltraGripEditor.Snap.Rotate";

        private const float MovedThreshold = 0.000_01f;

        public static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set => EditorPrefs.SetBool(EnabledKey, value);
        }

        public static float Move
        {
            get => EditorPrefs.GetFloat(MoveKey, 0.005f);
            set => EditorPrefs.SetFloat(MoveKey, Mathf.Max(value, 0f));
        }

        public static float Rotate
        {
            get => EditorPrefs.GetFloat(RotateKey, 5f);
            set => EditorPrefs.SetFloat(RotateKey, Mathf.Max(value, 0f));
        }

        public static float Snap(float value, float increment)
        {
            if(increment <= 0f) return value;
            return Mathf.Round(value / increment) * increment;
        }

        // snaps moved axes to a grid in the space's local axes
        public static Vector3 SnapPosition(SimpleTransform space, Vector3 from, Vector3 to, float increment)
        {
            if(increment <= 0f) return to;

            Vector3 localFrom = space.InverseTransformPoint(from);
            Vector3 local = space.InverseTransformPoint(to);
            for(int i = 0; i < 3; i++)
            {
                if(Mathf.Abs(local[i] - localFrom[i]) > MovedThreshold) local[i] = Snap(local[i], increment);
            }
            return space.TransformPoint(local);
        }

        // snaps the angle of the world-space rotation from `from` to `to`
        public static Quaternion SnapRotation(Quaternion from, Quaternion to, float increment)
        {
            if(increment <= 0f) return to;

            (to * Quaternion.Inverse(from)).ToAngleAxis(out float angle, out Vector3 axis);
            if(float.IsNaN(axis.x) || float.IsInfinity(axis.x)) return from;
            return Quaternion.AngleAxis(Snap(Mathf.DeltaAngle(0f, angle), increment), axis) * from;
        }

        // degrees in [-180, 180]
        // positive when the rotation axis points along axis
        public static float SignedAngle(Quaternion from, Quaternion to, Vector3 axis)
        {
            (to * Quaternion.Inverse(from)).ToAngleAxis(out float angle, out Vector3 deltaAxis);
            angle = Mathf.DeltaAngle(0f, angle);
            return Vector3.Dot(deltaAxis, axis) < 0f ? -angle : angle;
        }
    }
}
