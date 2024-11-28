using System;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel] AudioSource[] audioSources;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(Volume))]
        float defaultVolume = 1;
        [SerializeField, LocalizedLabel, FieldChangeCallback(nameof(Muted))]
        bool defaultMuted = false;
        AudioSource assignedAudioSource;

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
            var volume = defaultMuted ? 0 : defaultVolume * defaultVolume; // Volume is not linear
            if (Utilities.IsValid(audioSources))
                for (int i = 0; i < audioSources.Length; i++) {
                    var audioSource = audioSources[i];
                    if (!Utilities.IsValid(audioSource)) continue;
                    audioSource.volume = volume;
                }
            SendEvent("_OnVolumeChange");
            UpdateAudioLinkVolume();
        }

        void SetAudioPitch() {
            if (!Utilities.IsValid(audioSources)) return;
            var speed = activeHandler.Speed;
            for (int i = 0; i < audioSources.Length; i++) {
                var audioSource = audioSources[i];
                if (!Utilities.IsValid(audioSource)) continue;
                audioSource.pitch = speed;
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
