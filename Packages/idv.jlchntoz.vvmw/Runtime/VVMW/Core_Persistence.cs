#if VRC_ENABLE_PLAYER_PERSISTENCE
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Persistence;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel] internal bool enablePersistence = true;
        [SerializeField] internal string volumePersistenceKey;
        [SerializeField] internal string mutedPersistenceKey;
        bool persistenceSupported = false;

        public override void OnPlayerRestored(VRCPlayerApi player) {
            persistenceSupported = true;
            RestoreFromPersistence(player);
        }

        void RestoreFromPersistence(VRCPlayerApi player) {
            if (!player.isLocal || !enablePersistence || !persistenceSupported) return;
            bool volumeUpdated = false;
            if (!string.IsNullOrEmpty(volumePersistenceKey) && PlayerData.TryGetFloat(player, volumePersistenceKey, out float volume)) {
                defaultVolume = volume;
                volumeUpdated = true;
            }
            if (!string.IsNullOrEmpty(mutedPersistenceKey) && PlayerData.TryGetBool(player, mutedPersistenceKey, out bool muted)) {
                defaultMuted = muted;
                volumeUpdated = true;
            }
            if (volumeUpdated) UpdateVolume();
        }

        void SaveVolumeToPersistence() {
            if (!persistenceSupported || !enablePersistence) return;
            if (!string.IsNullOrEmpty(volumePersistenceKey))
                PlayerData.SetFloat(volumePersistenceKey, defaultVolume);
            if (!string.IsNullOrEmpty(mutedPersistenceKey))
                PlayerData.SetBool(mutedPersistenceKey, defaultMuted);
        }
    }
}
#else
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        void RestoreFromPersistence(VRCPlayerApi player) {}

        void SaveVolumeToPersistence() {}
    }
}
#endif