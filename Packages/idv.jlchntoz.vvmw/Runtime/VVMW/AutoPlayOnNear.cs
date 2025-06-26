using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
using VRC.SDK3.Data;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Automatically play the video when a user goes nearby.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/Auto Play On Near")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-setup-auto-plays-when-a-user-goes-nearby")]
    public class AutoPlayOnNear : VizVidBehaviour {
        [SerializeField, Locatable, LocalizedLabel(Key = "VVMW.Handler")] FrontendHandler handler;
        [SerializeField, LocalizedLabel] float distance = 0;
        [SerializeField] bool interruptOnEnter = true;
        [SerializeField] bool stopOnLeave = true;
        [SerializeField] bool unmuteOnEnter = false;
        [SerializeField] bool muteOnLeave = false;
        [SerializeField, Range(0, 1)] float unmuteVolume = 0.5F;
        DataDictionary enteredPlayers;
        VRCPlayerApi localPlayer;
        bool isSynced;
        bool wasPlaying;

        void OnEnable() {
            if (Utilities.IsValid(handler)) isSynced = handler.core.IsSynced;
            localPlayer = Networking.LocalPlayer;
            if (isSynced) {
                if (Utilities.IsValid(enteredPlayers))
                    enteredPlayers.Clear();
                else
                    enteredPlayers = new DataDictionary();
            }
            if (distance > 0) SendCustomEventDelayedFrames(nameof(_SlowUpdate), 0);
            Stop();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _SlowUpdate() {
            if (!enabled || !gameObject.activeInHierarchy) return;
            SendCustomEventDelayedSeconds(nameof(_SlowUpdate), 0.5F);
            if (!Utilities.IsValid(localPlayer)) return;
            if (Vector3.Distance(localPlayer.GetPosition(), transform.position) <= distance) {
                if (!wasPlaying) {
                    Play();
                    if (unmuteOnEnter) SetVolumes(unmuteVolume);
                    wasPlaying = true;
                }
            } else {
                if (wasPlaying) {
                    Stop();
                    if (muteOnLeave) SetVolumes(0);
                    wasPlaying = false;
                }
            }
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
            if (distance > 0) return;
            if (isSynced) {
                if (enteredPlayers.Count == 0) Play();
                enteredPlayers[player.playerId] = true;
            } else if (player.isLocal)
                Play();
            if (unmuteOnEnter && player.isLocal) SetVolumes(unmuteVolume);
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player) {
            if (distance > 0) return;
            if (isSynced) {
                if (enteredPlayers.Remove(player.playerId) && enteredPlayers.Count == 0)
                    Stop();
            } else if (player.isLocal)
                Stop();
            if (muteOnLeave && player.isLocal)
                SetVolumes(0);
        }

        void Play() {
            if (Utilities.IsValid(handler) && (!isSynced || Networking.IsOwner(Networking.LocalPlayer, handler.gameObject)) && (interruptOnEnter || !handler.core.IsPlaying))
                handler._AutoPlay();
        }

        void Stop() {
            if (Utilities.IsValid(handler) && (!isSynced || Networking.IsOwner(Networking.LocalPlayer, handler.gameObject)) && stopOnLeave)
                handler._Stop();
        }

        void SetVolumes(float volume) {
            if (!Utilities.IsValid(handler)) return;
            var audioControllers = handler.core.audioControllers;
            if (!Utilities.IsValid(audioControllers)) return;
            foreach (var audioController in audioControllers)
                if (Utilities.IsValid(audioController))
                    audioController.Volume = volume;
        }
    }
}