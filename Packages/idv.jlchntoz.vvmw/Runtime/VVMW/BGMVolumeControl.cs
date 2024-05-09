using UdonSharp;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/BGM Volume Control")]
    [RequireComponent(typeof(AudioSource))]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-make-background-music-fade-out-when-video-is-playing")]
    public class BGMVolumeControl : VizVidBehaviour {
        AudioSource audioSource;
        [SerializeField, Locatable, BindUdonSharpEvent] Core core;
        [Range(0, 1)] public float volume = 1;
        public bool isMuted;
        [SerializeField, Range(0, 10)] float fadeTime = 1;
        bool isVideoPlaying;

        void Start() {
            audioSource = GetComponent<AudioSource>();
        }

        void OnEnable() {
            isVideoPlaying = core.enabled && core.gameObject.activeSelf && core.IsPlaying && !core.IsStatic;
        }

        void Update() {
            float targetVolune = isMuted || isVideoPlaying ? 0 : volume;
            audioSource.volume = fadeTime > 0 ? Mathf.MoveTowards(audioSource.volume, targetVolune, Time.deltaTime / fadeTime) : targetVolune;
            if (isVideoPlaying && (!core.enabled || !core.gameObject.activeSelf)) isVideoPlaying = false;
        }

        public void Mute() => isMuted = true;

        public void Unmute() => isMuted = false;
        
        public override void OnVideoStart() => isVideoPlaying = !core.IsStatic;

        public override void OnVideoPlay() => isVideoPlaying = !core.IsStatic;

        public override void OnVideoPause() => isVideoPlaying = false;

        public override void OnVideoEnd() => isVideoPlaying = false;
    }
}