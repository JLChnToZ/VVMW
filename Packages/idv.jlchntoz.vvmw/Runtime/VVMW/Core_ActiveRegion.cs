using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, HideInInspector] ActiveRegionManager activeRegionManager;
        [SerializeField, HideInInspector] internal bool hasRegion;
#if COMPILER_UDONSHARP
        public
#else
        internal
#endif
        bool isCurrentCoreActive = true;

        bool IsActiveInternal => !hasRegion || !Utilities.IsValid(activeRegionManager) ||
            activeRegionManager.core == this ||
            activeRegionManager.isMatchingAll;

#if COMPILER_UDONSHARP
        public
#else
        internal
#endif
        void SetActive(bool active) {
            if (isCurrentCoreActive == active) return;
            isCurrentCoreActive = active;
            if (!isActiveAndEnabled || !afterFirstRun) return;
            if (active) RestoreActiveState();
            if (muteOnOutOfRange) UpdateVolume();
            SendEvent("_OnActiveStateChanged");
        }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
        void DrawRegionGizmos() {
            int i = 0;
            foreach (var boundData in ActiveRegionConfig.GetRegionConfigs(this)) {
                if (boundData == null) continue;
                Gizmos.color = Color.HSVToRGB(i++ * 0.35F % 1F, 1F, 1F);
                Gizmos.matrix = boundData.staticRegion ? Matrix4x4.identity : boundData.transform.localToWorldMatrix;
                var size = boundData.bounds.size;
                if (Mathf.Approximately(size.sqrMagnitude, 0))
                    Gizmos.DrawSphere(boundData.bounds.center, 0.1F);
                else
                    Gizmos.DrawWireCube(boundData.bounds.center, size);
            }
        }
#endif
    }
}