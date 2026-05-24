using System;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// An editor component to help auto configurate VRChat interactable UI canvas.
    /// </summary>
    [ExecuteInEditMode, EditorOnly]
    [RequireComponent(typeof(RectTransform), typeof(BoxCollider))]
    [AddComponentMenu("VizVid/Common/VRC Canvas Utility")]
    public class VRCCanvasUtility : MonoBehaviour {
        new RectTransform transform;
        new BoxCollider collider;
        bool hasInit;
        Vector2 lastLocalSize, lastLocalScale;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        static Vector2 GetTransformedSize(Transform transform, Vector2 size) {
            Span<Vector3> v = stackalloc Vector3[2];
            v[0] = new(size.x, 0, 0);
            v[1] = new(0, size.y, 0);
            transform.TransformVectors(v);
            return new(v[0].magnitude, v[1].magnitude);
        }
#endif

        void Awake() {
            if (hasInit) return;
            transform = GetComponent<RectTransform>();
            collider = GetComponent<BoxCollider>();
            hasInit = true;
        }

        void OnEnable() => Fixup();

        void Update() {
            if (!hasInit) Awake();
            Fixup();
        }

        void Fixup() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (AnimationMode.InAnimationMode() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var rect = transform.rect;
            var localSize = rect.size;
            var size = GetTransformedSize(transform, localSize);
            var localAspectRatio = localSize.x / localSize.y;
            var worldAspectRatio = size.x / size.y;
            if (!Mathf.Approximately(localAspectRatio, worldAspectRatio)) {
                var localScale = transform.localScale;
                var parent = transform.parent;
                Vector2 worldScale = localScale;
                if (parent != null) worldScale = GetTransformedSize(parent, localScale);
                bool changed = false;
                if (!Mathf.Approximately(worldScale.x, worldScale.y)) {
                    if (!changed) Undo.RecordObject(transform, "Fixup Canvas");
                    changed = true;
                    localScale.y *= worldScale.x / worldScale.y;
                    transform.localScale = localScale;
                    lastLocalScale = localScale;
                }
                localSize = transform.sizeDelta;
                var newHeight = localSize.x / worldAspectRatio;
                if (localSize.y != newHeight) {
                    if (!changed) Undo.RecordObject(transform, "Fixup Canvas");
                    changed = true;
                    localSize.y = newHeight;
                    transform.sizeDelta = localSize;
                }
                lastLocalSize = localSize;
                if (changed && PrefabUtility.IsPartOfPrefabInstance(transform))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                rect = transform.rect;
            }
            Vector3 newCenter = rect.center;
            Vector3 orgScale = collider.size;
            Vector2 newScale = rect.size, orgV2Scale = orgScale;
            if (!collider.isTrigger || collider.center != newCenter || orgV2Scale != newScale) {
                Undo.RecordObject(collider, "Fixup Canvas");
                collider.isTrigger = true;
                collider.center = newCenter;
                collider.size = newScale;
                if (PrefabUtility.IsPartOfPrefabInstance(collider))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
            }
#endif
        }
    }
}