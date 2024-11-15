using System;
using UdonSharp;
using UnityEngine;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel] AudioSource[] audioSources;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(Volume))]
        float defaultVolume = 1;
        [SerializeField, LocalizedLabel, FieldChangeCallback(nameof(Muted))]
        bool defaultMuted = false;
        AudioSource assignedAudioSource;

        public float Volume {
            get => defaultMuted ? 0 : defaultVolume;
            set {
                defaultVolume = Mathf.Clamp01(value);
                if (value > 0) defaultMuted = false;
                UpdateVolume();
                SaveVolumeToPersistence();
            }
        }

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
            if (audioSources != null)
                for (int i = 0; i < audioSources.Length; i++) {
                    var audioSource = audioSources[i];
                    if (audioSource == null) continue;
                    audioSource.volume = volume;
                }
            SendEvent("_OnVolumeChange");
            UpdateAudioLinkVolume();
        }

        void SetAudioPitch() {
            if (audioSources == null) return;
            var speed = activeHandler.Speed;
            for (int i = 0; i < audioSources.Length; i++) {
                var audioSource = audioSources[i];
                if (audioSource == null) continue;
                audioSource.pitch = speed;
            }
        }
    }
}
