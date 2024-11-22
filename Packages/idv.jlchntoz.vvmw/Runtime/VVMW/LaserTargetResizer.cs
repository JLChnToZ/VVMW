using VRC.SDKBase;
using UdonSharp;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    [RequireComponent(typeof(BoxCollider))]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LaserTargetResizer : UdonSharpBehaviour {
        new BoxCollider collider;
        [SerializeField] RectTransform inactiveRect, activeRect;
        bool hasInit;

        void Init() {
            if (hasInit) return;
            hasInit = true;
            collider = GetComponent<BoxCollider>();
        }

        public void _OnActive() => SetRect(activeRect);

        public void _OnInactive() => SetRect(inactiveRect);

        void SetRect(RectTransform rectTransform) {
            if (!Utilities.IsValid(rectTransform)) return;
            Init();
            var rect = rectTransform.rect;
            collider.center = transform.InverseTransformPoint(rectTransform.TransformPoint(rect.center));
            Vector3 size = rect.size;
            size.z = 1;
            collider.size = transform.InverseTransformVector(rectTransform.TransformVector(size));
        }
    }
}