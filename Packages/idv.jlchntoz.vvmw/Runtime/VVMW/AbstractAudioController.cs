using UnityEngine;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    [RequireComponent(typeof(AudioSource))]
    public abstract class AbstractAudioController : UdonSharpBehaviour {
        [Resolve("."), HideInInspector] protected AudioSource audioSource;
        [SerializeField, HideInInspector] internal protected Core core;
        [SerializeField, Range(0, 1), FieldChangeCallback(nameof(Volume))] protected float volume = 1;
        float internalVolume = 1;

        public float Volume {
            get => volume;
            set {
                volume = Mathf.Clamp01(value);
                UpdateVolume();
            }
        }

        protected virtual void OnEnable() {
            internalVolume = core.Volume;
            internalVolume *= internalVolume;
            UpdateVolume();
        }

#if COMPILER_UDONSHARP
        public
#else
        internal
#endif
        void InternalSetVolume(float value) {
            internalVolume = value;
            UpdateVolume();
        }

#if COMPILER_UDONSHARP
        public
#else
        internal
#endif
        void InternalSetPitch(float value) => audioSource.pitch = value;

        void UpdateVolume() => audioSource.volume = internalVolume * volume;
    }
}