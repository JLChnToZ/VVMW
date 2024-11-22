using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel, Range(0, 10)] float realtimeGIUpdateInterval = 0;
        bool isRealtimeGIUpdaterRunning;

        void UpdateRealtimeGI() {
            if (isRealtimeGIUpdaterRunning || realtimeGIUpdateInterval <= 0) return;
            isRealtimeGIUpdaterRunning = true;
            SendCustomEventDelayedFrames(nameof(_UpdateRealtimeGI), 0);
        }

        public void _UpdateRealtimeGI() {
            for (int i = 0, length = screenTargets.Length; i < length; i++)
                switch (screenTargetModes[i] & 0x7) {
                    case 1: case 2: case 3:
                        var target = (Renderer)screenTargets[i];
                        if (Utilities.IsValid(target)) RendererExtensions.UpdateGIMaterials(target);
                        break;
                }
            if (!enabled || !gameObject.activeInHierarchy || !Utilities.IsValid(activeHandler) || !activeHandler.IsReady) {
                isRealtimeGIUpdaterRunning = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_UpdateRealtimeGI), realtimeGIUpdateInterval);
        }
    }
}
