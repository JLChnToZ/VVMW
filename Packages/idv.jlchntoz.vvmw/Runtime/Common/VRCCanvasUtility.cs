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

        void Awake() {
            if (hasInit) return;
            transform = GetComponent<RectTransform>();
            collider = GetComponent<BoxCollider>();
            hasInit = true;
        }

        void OnEnable() => Fixup();

        void Update() {
            if (!hasInit) Awake();
            if (transform.hasChanged) {
                Fixup();
                transform.hasChanged = false;
            }
            collider.isTrigger = true;
            var rect = transform.rect;
            collider.center = rect.center;
            Vector3 scale = rect.size;
            scale.z = 1;
            collider.size = scale;
        }

        void Fixup() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (AnimationMode.InAnimationMode() || EditorApplication.isPlayingOrWillChangePlaymode) return;
            var localSize = transform.rect.size;
            Vector2 size;
            {
                Span<Vector3> tempV3 = stackalloc Vector3[2];
                tempV3[0] = new(localSize.x, 0, 0);
                tempV3[1] = new(0, localSize.y, 0);
                transform.TransformVectors(tempV3);
                size = new(tempV3[0].magnitude, tempV3[1].magnitude);
            }
            var localAspectRatio = localSize.x / localSize.y;
            var worldAspectRatio = size.x / size.y;
            if (Mathf.Approximately(localAspectRatio, worldAspectRatio)) return;
            var localScale = transform.localScale;
            var parent = transform.parent;
            Vector2 worldScale = localScale;
            if (parent != null) {
                Span<Vector3> tempV3 = stackalloc Vector3[2];
                tempV3[0] = new(localScale.x, 0, 0);
                tempV3[1] = new(0, localScale.y, 0);
                parent.TransformVectors(tempV3);
                worldScale = new(tempV3[0].magnitude, tempV3[1].magnitude);
            }
            if (!Mathf.Approximately(worldScale.x, worldScale.y)) {
                localScale.y *= worldScale.x / worldScale.y;
                transform.localScale = localScale;
                lastLocalScale = localScale;
            }
            localSize = transform.sizeDelta;
            localSize.y = localSize.x / worldAspectRatio;
            transform.sizeDelta = localSize;
            lastLocalSize = localSize;
#endif
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        void OnValidate() {
            if (!isActiveAndEnabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.delayCall += Update;
        }
#endif
    }
}