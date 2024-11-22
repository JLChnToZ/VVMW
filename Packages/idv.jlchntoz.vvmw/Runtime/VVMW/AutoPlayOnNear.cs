using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Components/Auto Play On Near")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-setup-auto-plays-when-a-user-goes-nearby")]
    public class AutoPlayOnNear : VizVidBehaviour {
        [SerializeField, Locatable, LocalizedLabel(Key = "VVMW.Handler")] FrontendHandler handler;
        [SerializeField, LocalizedLabel] float distance = 0;
        bool wasPlaying;

        void OnEnable() {
            if (distance > 0) SendCustomEventDelayedFrames(nameof(_SlowUpdate), 0);
            Stop();
        }

        public void _SlowUpdate() {
            if (!enabled || !gameObject.activeInHierarchy) return;
            SendCustomEventDelayedSeconds(nameof(_SlowUpdate), 0.5F);
            var player = Networking.LocalPlayer;
            if (!Utilities.IsValid(player)) return;
            if (Vector3.Distance(player.GetPosition(), transform.position) <= distance) {
                if (!wasPlaying) Play();
            } else {
                if (wasPlaying) Stop();
            }
        }

        public override void OnPlayerTriggerEnter(VRCPlayerApi player) {
            if (player.isLocal && distance <= 0) Play();
        }

        public override void OnPlayerTriggerExit(VRCPlayerApi player) {
            if (player.isLocal && distance <= 0) Stop();
        }

        void Play() {
            if (Utilities.IsValid(handler)) handler._AutoPlay();
            wasPlaying = true;
        }

        void Stop() {
            if (Utilities.IsValid(handler)) handler._Stop();
            wasPlaying = false;
        }
    }
}