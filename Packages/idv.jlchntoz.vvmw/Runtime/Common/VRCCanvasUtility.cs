using UnityEngine;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {
    [ExecuteInEditMode, EditorOnly]
    [RequireComponent(typeof(RectTransform), typeof(BoxCollider))]
    [AddComponentMenu("VizVid/Common/VRC Canvas Utility")]
    public class VRCCanvasUtility : MonoBehaviour {
        [SerializeField, LocalizedLabel] float canvasScale = 0;
        new RectTransform transform;
        new BoxCollider collider;
        bool hasInit;

        void Awake() {
            if (hasInit) return;
            transform = GetComponent<RectTransform>();
            collider = GetComponent<BoxCollider>();
            hasInit = true;
        }

        void Update() {
            if (!hasInit) Awake();
            Vector3 scale;
            if (float.IsInfinity(canvasScale) || float.IsNaN(canvasScale) || canvasScale <= 0) {
                scale = transform.localScale;
                canvasScale = (scale.x + scale.y) / 2;
            }
            if (transform.hasChanged && canvasScale > 0 && !float.IsInfinity(canvasScale) && !float.IsNaN(canvasScale)) {
                scale = transform.localScale / canvasScale;
                var sizeDelta = transform.sizeDelta;
                scale.x *= sizeDelta.x;
                scale.y *= sizeDelta.y;
                transform.sizeDelta = scale;
                transform.localScale = Vector3.one * canvasScale;
                transform.hasChanged = false;
            }
            collider.isTrigger = true;
            var rect = transform.rect;
            collider.center = rect.center;
            scale = rect.size;
            scale.z = 1;
            collider.size = scale;
        }

        void OnValidate() => Update();
    }
}