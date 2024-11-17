using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
using UdonSharp;

#if AUDIOLINK_V1
using AudioLink;
#endif

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [Locatable(
            "AudioLink.AudioLink, AudioLink", "VRCAudioLink.AudioLink, AudioLink",
            InstaniatePrefabPath = "Packages/com.llealloo.audiolink/Runtime/AudioLink.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        ), SerializeField, LocalizedLabel]
        #if AUDIOLINK_V1
        AudioLink.AudioLink audioLink;
        #else
        UdonSharpBehaviour audioLink;
        #endif
        bool isSyncAudioLink;

        #if AUDIOLINK_V1
        public AudioLink.AudioLink AudioLink
        #else
        public UdonSharpBehaviour AudioLink
        #endif
        {
            get {
                #if AUDIOLINK_V1
                if (!IsAudioLinked()) return null;
                #endif
                return audioLink;
            }
        }

        void AssignAudioLinkSource() {
            assignedAudioSource = activeHandler.PrimaryAudioSource;
            if (audioLink != null) {
                if (assignedAudioSource != null)
                #if AUDIOLINK_V1
                    audioLink.audioSource = assignedAudioSource;
                float duration = activeHandler.Duration;
                SetAudioLinkPlayBackState(duration <= 0 || float.IsInfinity(duration) ? MediaPlaying.Streaming : MediaPlaying.Playing);
                UpdateAudioLinkVolume();
                #else
                    audioLink.SetProgramVariable("audioSource", assignedAudioSource);
                #endif
            }
        }

        void UpdateAudioLinkVolume() {
            #if AUDIOLINK_V1
            if (IsAudioLinked()) audioLink.SetMediaVolume(defaultVolume);
            #endif
        }

        #if AUDIOLINK_V1
        bool IsAudioLinked() {
            if (audioLink == null) return false;
            var settedAudioSource = audioLink.audioSource;
            return settedAudioSource == null || settedAudioSource == assignedAudioSource;
        }

        void SetAudioLinkPlayBackState(MediaPlaying state) {
            if (IsAudioLinked()) {
                audioLink.autoSetMediaState = false;
                audioLink.SetMediaPlaying(state);
            }
            if (!isSyncAudioLink) {
                isSyncAudioLink = true;
                _SyncAudioLink();
            }
        }

        public void _SyncAudioLink() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || activeHandler == null || !activeHandler.IsReady || !IsAudioLinked()) {
                isSyncAudioLink = false;
                return;
            }
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                audioLink.SetMediaTime(0);
                isSyncAudioLink = false;
                return;
            }
            audioLink.SetMediaTime(activeHandler.Time / duration);
            SendCustomEventDelayedSeconds(nameof(_SyncAudioLink), 0.25F);
        }

        public void _RestoreAudioLinkState() {
            var state = MediaPlaying.Stopped;
            if (activeHandler != null) {
                if (activeHandler.IsPlaying) {
                    state = MediaPlaying.Playing;
                    AssignAudioLinkSource();
                } else if (activeHandler.IsPaused)
                    state = MediaPlaying.Paused;
            } else if (isLoading)
                state = MediaPlaying.Loading;
            else if (isError)
                state = MediaPlaying.Error;
            SetAudioLinkPlayBackState(state);
        }
        #endif
    }
}
