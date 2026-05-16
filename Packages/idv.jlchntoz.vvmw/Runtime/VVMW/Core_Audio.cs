using System;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel] internal AudioSource[] audioSources;
        [SerializeField] internal AbstractAudioController[] audioControllers;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(Volume))]
        float defaultVolume = 1;
        [SerializeField, LocalizedLabel, FieldChangeCallback(nameof(Muted))]
        bool defaultMuted = false;
        [SerializeField, LocalizedLabel, Min(0)] float volumeFadeDuration = 0.5f;
        [SerializeField, LocalizedLabel] internal bool muteOnOutOfRange;
        [SerializeField, LocalizedLabel, Range(0, 1)] internal float outOfRangeVolume = 0f;
        AudioSource assignedAudioSource, assignedAudioSourceR;
        float currentFadeVolume = 0, targetFadeVolume = 0;
        bool isVolumeFading = false;

        /// <summary>
        /// The volume of the audio.
        /// </summary>
        public float Volume {
            get => defaultMuted ? 0 : defaultVolume;
            set {
                defaultVolume = Mathf.Clamp01(value);
                if (value > 0) defaultMuted = false;
                UpdateVolume();
                SaveVolumeToPersistence();
            }
        }

        /// <summary>
        /// Whether the audio is muted.
        /// </summary>
        public bool Muted {
            get => defaultMuted;
            set {
                defaultMuted = value;
                UpdateVolume();
                SaveVolumeToPersistence();
            }
        }

        void UpdateVolume() {
            if (!isVolumeFading) {
                isVolumeFading = true;
                _HandleVolumeFade();
            }
            SendEvent("_OnVolumeChange");
            UpdateAudioLinkVolume();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _HandleVolumeFade() {
            targetFadeVolume = defaultMuted ? 0 : hasRegion && !isCurrentCoreActive ? outOfRangeVolume : 1F;
            if (volumeFadeDuration <= 0 || Mathf.Approximately(currentFadeVolume, targetFadeVolume)) {
                currentFadeVolume = targetFadeVolume;
                isVolumeFading = false;
            } else {
                currentFadeVolume = Mathf.MoveTowards(currentFadeVolume, targetFadeVolume, UnityEngine.Time.deltaTime / volumeFadeDuration);
                SendCustomEventDelayedFrames(nameof(_HandleVolumeFade), 0);
            }
            var volume = currentFadeVolume * defaultVolume;
            volume *= volume; // Volume is not linear
            if (Utilities.IsValid(audioSources))
                for (int i = 0, ic = audioSources.Length; i < ic; i++) {
                    var audioSource = audioSources[i];
                    if (!Utilities.IsValid(audioSource)) continue;
                    audioSource.volume = volume;
                }
            if (Utilities.IsValid(audioControllers))
                for (int i = 0, ic = audioControllers.Length; i < ic; i++) {
                    var controller = audioControllers[i];
                    if (!Utilities.IsValid(controller)) continue;
                    controller.InternalSetVolume(volume);
                }
        }

        void SetAudioPitch() {
            if (!Utilities.IsValid(activeHandler)) return;
            var speed = activeHandler.Speed;
            if (Utilities.IsValid(audioSources))
                for (int i = 0, ic = audioSources.Length; i < ic; i++) {
                    var audioSource = audioSources[i];
                    if (!Utilities.IsValid(audioSource)) continue;
                    audioSource.pitch = speed;
                }
            if (Utilities.IsValid(audioControllers))
                for (int i = 0, ic = audioControllers.Length; i < ic; i++) {
                    var controller = audioControllers[i];
                    if (!Utilities.IsValid(controller)) continue;
                    controller.InternalSetPitch(speed);
                }
        }

#if !COMPILER_UDONSHARP
        void DrawAudioGizmos() {
            foreach (var audioSource in audioSources) {
                if (audioSource == null) continue;
                Gizmos.DrawIcon(audioSource.transform.position, "AudioSource Gizmo", true, Color.green);
            }
        }
#endif
    }
}
