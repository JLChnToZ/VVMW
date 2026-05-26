using System;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityObject = UnityEngine.Object;
#endif

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// An editor component to help auto configurate VRChat interactable UI canvas.
    /// </summary>
    [ExecuteInEditMode, EditorOnly]
    [RequireComponent(typeof(RectTransform), typeof(BoxCollider))]
    [AddComponentMenu("VizVid/Common/VRC Canvas Utility")]
    public class VRCCanvasUtility : MonoBehaviour {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
        [SerializeField] Axis fixedAxis = Axis.Horizontal;
        new RectTransform transform;
        new BoxCollider collider;
        [NonSerialized] bool scaleChanging;
        [NonSerialized] Vector2 lastLocalScale;

        static float GetGlobalAspect(Transform transform, Vector2 size) {
            if (transform == null) return size.x / size.y;
            Span<Vector3> v = stackalloc Vector3[2];
            v[0].x = size.x;
            v[1].y = size.y;
            transform.TransformVectors(v);
            return v[0].magnitude / v[1].magnitude;
        }

        static void SavePrefabChanges(UnityObject obj) {
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);   
        }

        void Update() {
            if (AnimationMode.InAnimationMode() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (FixUpTransformAndGetRect(out var localScale))
                SavePrefabChanges(transform);
            if (FixUpCollider())
                SavePrefabChanges(collider);
            lastLocalScale = localScale;
        }

        bool FixUpTransformAndGetRect(out Vector2 localScale) {
            if (transform == null) {
                if (!TryGetComponent(out transform)) {
                    localScale = default;
                    return false;
                }
                lastLocalScale = localScale = transform.localScale;
            } else
                localScale = transform.localScale;
            var localSize = transform.sizeDelta;
            if (localScale != lastLocalScale) {
                scaleChanging = true;
                return false;
            }
            if (scaleChanging) {
                scaleChanging = false;
                return false;
            }
            float targetAspect = GetGlobalAspect(transform, localSize);
            var parentAspect = GetGlobalAspect(transform.parent, localScale);
            if (Mathf.Approximately(parentAspect, 1F)) return false;
            switch (fixedAxis) {
                case Axis.Horizontal: localScale.y *= parentAspect; break;
                case Axis.Vertical: localScale.x /= parentAspect; break;
                default: return false;
            }
            Undo.RecordObject(transform, "Fixup Canvas");
            Vector3 localScaleV3 = localScale;
            localScaleV3.z = (localScale.x + localScale.y) * 0.5F;
            transform.localScale = localScaleV3;
            if (Mathf.Approximately(targetAspect, GetGlobalAspect(transform, localSize))) return true;
            switch (fixedAxis) {
                case Axis.Horizontal: localSize.y = localSize.x / targetAspect; break;
                case Axis.Vertical: localSize.x = localSize.y * targetAspect; break;
                default: return true;
            }
            transform.sizeDelta = localSize;
            return true;
        }

        bool FixUpCollider() {
            if (collider == null && !TryGetComponent(out collider))
                return false;
            var rect = transform.rect;
            Vector3 newCenter = rect.center;
            Vector3 orgScale = collider.size;
            Vector2 newScale = rect.size, orgV2Scale = orgScale;
            if (collider.isTrigger && collider.center == newCenter && orgV2Scale == newScale)
                return false;
            Undo.RecordObject(collider, "Fixup Canvas");
            collider.isTrigger = true;
            collider.center = newCenter;
            collider.size = newScale;
            return true;
        }

        enum Axis {
            Horizontal = 0,
            Vertical = 1,
        }
#endif
    }
}
