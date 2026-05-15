using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    [RequireComponent(typeof(BoxCollider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LaserTargetResizer : UdonSharpBehaviour {
        [SerializeField, HideInInspector, Resolve(".")] new BoxCollider collider;
        [SerializeField] RectTransform inactiveRect, activeRect;

        public void _OnActive() => SetRect(activeRect);

        public void _OnInactive() => SetRect(inactiveRect);

        void SetRect(RectTransform rectTransform) {
            if (!Utilities.IsValid(rectTransform)) return;
            var rect = rectTransform.rect;
            collider.center = transform.InverseTransformPoint(rectTransform.TransformPoint(rect.center));
            Vector3 size = rect.size;
            size.z = 1;
            collider.size = transform.InverseTransformVector(rectTransform.TransformVector(size));
        }
    }
}