using System;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    [AddComponentMenu("VizVid/Audio Source Configurator")]
    public class AudioSourceConfigurator : MonoBehaviour, ISelfPreProcess {
        [LocalizedLabel] public AudioSource[] audioSources;
        [LocalizedLabel] public SphereBounds nearBounds;
        [LocalizedLabel] public SphereBounds farBounds;
        [NonSerialized] bool hasAwaken;

        int IPrioritizedPreProcessor.Priority => 0;

        void ISelfPreProcess.PreProcess() => TryFit();

        void Awake() {
            if (hasAwaken) return;
            hasAwaken = true;
            if (!Application.isPlaying) TryEstimate();
        }

        void OnValidate() => Awake();

        public void TryEstimate() {
            nearBounds.center = Vector3.zero;
            nearBounds.radius = 0F;
            farBounds.center = Vector3.zero;
            farBounds.radius = 0f;
            int count = 0;
            foreach (var audioSource in audioSources) {
                if (audioSource == null) continue;
                var position = audioSource.transform.position;
                var near = audioSource.TryGetComponent(out VRC_SpatialAudioSource spatialAudioSource) ? spatialAudioSource.Near : audioSource.minDistance;
                if (count == 0) {
                    nearBounds.center = position;
                    nearBounds.radius = near;
                } else {
                    var distance = Vector3.Distance(position, nearBounds.center);
                    if (distance + near < nearBounds.radius) {
                        nearBounds.radius = distance + near;
                    } else if (distance - near > nearBounds.radius) {
                        nearBounds.center = (nearBounds.center * nearBounds.radius + position * near) / (nearBounds.radius + near);
                        nearBounds.radius += distance - near;
                    } else {
                        var newRadius = (nearBounds.radius + distance + near) * 0.5F;
                        nearBounds.center += (position - nearBounds.center).normalized * (newRadius - nearBounds.radius);
                        nearBounds.radius = newRadius;
                    }
                }
                farBounds.center += position;
                count++;
            }
            if (count == 0) return;
            nearBounds.center = transform.InverseTransformPoint(nearBounds.center);
            farBounds.center /= count;
            foreach (var audioSource in audioSources) {
                if (audioSource == null) continue;
                var far = audioSource.TryGetComponent(out VRC_SpatialAudioSource spatialAudioSource) ? spatialAudioSource.Far : audioSource.maxDistance;
                farBounds.radius = Mathf.Max(farBounds.radius, Vector3.Distance(audioSource.transform.position, farBounds.center) + far);
            }
            farBounds.center = transform.InverseTransformPoint(farBounds.center);
        }

        public void TryFit() {
            var worldNearBounds = new SphereBounds(transform.TransformPoint(nearBounds.center), nearBounds.radius);
            var worldFarBounds = new SphereBounds(transform.TransformPoint(farBounds.center), farBounds.radius);
            foreach (var audioSource in audioSources) {
                if (audioSource == null) continue;
                var pos = audioSource.transform.position;
                var near = Mathf.Max(0f, worldNearBounds.radius - Vector3.Distance(pos, worldNearBounds.center));
                var far = Mathf.Max(near, worldFarBounds.radius - Vector3.Distance(pos, worldFarBounds.center));
                if (audioSource.TryGetComponent(out VRC_SpatialAudioSource spatialAudioSource)) {
                    spatialAudioSource.Near = near;
                    spatialAudioSource.Far = far;
                    spatialAudioSource.VolumetricRadius = Mathf.Min(spatialAudioSource.VolumetricRadius, near);
                }
                audioSource.minDistance = near;
                audioSource.maxDistance = far;
            }
        }
        
        [Serializable]
        public struct SphereBounds {
            [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.SphereBounds.center")] public Vector3 center;
            [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.SphereBounds.radius")] public float radius;

            public SphereBounds(Vector3 center, float radius) {
                this.center = center;
                this.radius = radius;
            }
        }
    }
}
