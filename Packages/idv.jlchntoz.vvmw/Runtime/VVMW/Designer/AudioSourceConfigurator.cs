using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    [AddComponentMenu("VizVid/Audio Source Configurator")]
    public class AudioSourceConfigurator : MonoBehaviour {
        [LocalizedLabel] public AudioSource[] audioSources;
        [LocalizedLabel] public SphereBounds nearBounds;
        [LocalizedLabel] public SphereBounds farBounds;
        [NonSerialized] bool hasAwaken;

        static void TryEstimate(AudioSource[] audioSources, out SphereBounds nearBounds, out SphereBounds farBounds) {
            nearBounds = default;
            farBounds = default;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            using (PooledObjectExtensions.Get(out List<(Vector3 pos, float near, float far)> sources, audioSources.Length)) {
                ref var nearCenter = ref nearBounds.center;
                ref var nearRadius = ref nearBounds.radius;
                ref var farCenter = ref farBounds.center;
                ref var farRadius = ref farBounds.radius;
                foreach (var audioSource in audioSources) {
                    if (audioSource == null) continue;
                    var pos = audioSource.transform.position;
                    sources.Add(audioSource.TryGetComponent(out VRC_SpatialAudioSource spatialAudioSource) ?
                        (pos, spatialAudioSource.Near, spatialAudioSource.Far) :
                        (pos, audioSource.minDistance, audioSource.maxDistance)
                    );
                    farCenter += pos;
                }
                var count = sources.Count;
                if (count == 0) return;
                farCenter /= count;
                nearCenter = farCenter;
                var maxNear = float.PositiveInfinity;
                foreach (var (pos, near, far) in sources) {
                    farRadius = Mathf.Max(farRadius, Vector3.Distance(pos, farCenter) + far);
                    maxNear = Mathf.Min(maxNear, Mathf.Max(0F, near));
                }
                var iterations = Mathf.Max(32, count * 16);
                if (TryFindCommonCenter(sources, iterations, 0F, ref nearCenter)) {
                    var bestCenter = nearCenter;
                    var minRadius = 0F;
                    for (var iteration = 0; iteration < 20; iteration++) {
                        var candidateRadius = (minRadius + maxNear) * 0.5F;
                        if (TryFindCommonCenter(sources, iterations, candidateRadius, ref nearCenter)) {
                            minRadius = candidateRadius;
                            bestCenter = nearCenter;
                        } else
                            maxNear = candidateRadius;
                    }
                    nearCenter = bestCenter;
                    nearRadius = minRadius;
                }
            }
#endif
        }

        static bool TryFindCommonCenter(List<(Vector3 pos, float near, float far)> sources, int iterations, float commonRadius, ref Vector3 center) {
            for (var pass = 0; pass < iterations; pass++) {
                var largestOverlap = 0f;
                foreach (var (pos, near, _) in sources) {
                    var availableRadius = Mathf.Max(0f, near) - commonRadius;
                    var offset = center - pos;
                    var distance = offset.magnitude;
                    var overlap = distance - availableRadius;
                    if (overlap <= 0f) continue;
                    if (distance > 0.000001f)
                        center = pos + offset * (availableRadius / distance);
                    else
                        center = pos;
                    largestOverlap = Mathf.Max(largestOverlap, overlap);
                }
                if (largestOverlap <= 0.0001f) return true;
            }
            return false;
        }

        void Awake() {
            if (hasAwaken) return;
            hasAwaken = true;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
#endif
            TryEstimate(true);
        }

        void OnValidate() => Awake();

        public void TryEstimate(bool undo = false) {
            TryEstimate(audioSources, out var newNear, out var newFar);
            newNear.center = transform.InverseTransformPoint(newNear.center);
            newFar.center = transform.InverseTransformPoint(newFar.center);
            if (nearBounds.Equals(newNear) && farBounds.Equals(newFar)) return;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (undo) Undo.RecordObject(this, "Estimate Audio Source Near Bounds");
#endif
            nearBounds = newNear;
            farBounds = newFar;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (undo && PrefabUtility.IsPartOfPrefabInstance(this))
                PrefabUtility.RecordPrefabInstancePropertyModifications(this);
#endif
        }

        public void TryFit(bool undo = false) {
            var worldNear = transform.TransformPoint(nearBounds.center);
            var worldFar = transform.TransformPoint(farBounds.center);
            foreach (var audioSource in audioSources) {
                if (audioSource == null) continue;
                var pos = audioSource.transform.position;
                var near = Vector3.Distance(pos, worldNear) + nearBounds.radius;
                var far = Mathf.Max(near, farBounds.radius - Vector3.Distance(pos, worldFar));
                if (audioSource.TryGetComponent(out VRC_SpatialAudioSource spatialAudioSource)) {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
                    if (undo) Undo.RecordObject(spatialAudioSource, "Fit Audio Source");
#endif
                    spatialAudioSource.Near = near;
                    spatialAudioSource.Far = far;
                    spatialAudioSource.VolumetricRadius = Mathf.Min(spatialAudioSource.VolumetricRadius, near);
#if UNITY_EDITOR && !COMPILER_UDONSHARP
                    if (undo && PrefabUtility.IsPartOfPrefabInstance(spatialAudioSource))
                        PrefabUtility.RecordPrefabInstancePropertyModifications(spatialAudioSource);
#endif
                }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
                if (undo) Undo.RecordObject(audioSource, "Fit Audio Source");
#endif
                audioSource.minDistance = near;
                audioSource.maxDistance = far;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
                if (undo && PrefabUtility.IsPartOfPrefabInstance(audioSource))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(audioSource);
#endif
            }
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (undo) Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
#endif
        }

        [Serializable]
        public struct SphereBounds : IEquatable<SphereBounds> {
            [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.SphereBounds.center")] public Vector3 center;
            [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.SphereBounds.radius")] public float radius;

            public SphereBounds(Vector3 center, float radius) {
                this.center = center;
                this.radius = radius;
            }

            public readonly bool Equals(SphereBounds other) => center == other.center && Mathf.Approximately(radius, other.radius);

            public override readonly bool Equals(object obj) => obj is SphereBounds other && Equals(other);

            public override readonly int GetHashCode() => HashCode.Combine(center, radius);
        }
    }
}
