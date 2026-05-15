using System;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Automatically play the video when a user goes nearby.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [AddComponentMenu("VizVid/Components/Auto Play On Near")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-setup-auto-plays-when-a-user-goes-nearby")]
    public class AutoPlayOnNearV2 : VizVidBehaviour {
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        [Resolve(nameof(handler) + "." + nameof(FrontendHandler.core), HideInInspectorIfResolvable = true)]
        [Locatable] Core core;
        [SerializeField, Locatable, LocalizedLabel(Key = "VVMW.Handler")] FrontendHandler handler;
        [SerializeField, HideInInspector, Resolve(nameof(core))] GameObject coreGO;
        [SerializeField, HideInInspector, Resolve(nameof(handler))] GameObject handlerGO;
        [SerializeField, HideInInspector, BindUdonSharpEvent] ActiveRegionManager activeRegionManager;
        [SerializeField] bool interruptOnEnter = true;
        [SerializeField] bool stopOnLeave = true;
        [UdonSynced] ushort[] enteredPlayerIds;
        ushort localPlayerId;
        bool isSynced;

        void OnEnable() {
            if (Utilities.IsValid(core)) isSynced = core.IsSynced;
            localPlayerId = (ushort)Networking.LocalPlayer.playerId;
        }

        public void _OnMatchingCoresChanged() {
            var isActive = core.isCurrentCoreActive;
            if (isActive) {
                if (interruptOnEnter && !core.IsPlaying && AddPlayerToEntered()) {
                    if (Utilities.IsValid(handler)) {
                        if (isSynced && !Networking.IsOwner(handlerGO))
                            Networking.SetOwner(Networking.LocalPlayer, handlerGO);
                        handler._AutoPlay();
                    } else {
                        if (isSynced && !Networking.IsOwner(coreGO))
                            Networking.SetOwner(Networking.LocalPlayer, coreGO);
                        core._PlayDefaultUrl();
                    }
                }
            } else {
                if (stopOnLeave && core.IsPlaying && RemovePlayerFromEntered()) {
                    if (Utilities.IsValid(handler))
                        handler._Stop();
                    else
                        core.Stop();
                }
            }
        }

        bool AddPlayerToEntered() {
            if (!isSynced) return true;
            if (!Utilities.IsValid(enteredPlayerIds)) {
                enteredPlayerIds = new ushort[] { localPlayerId };
                RequestSync();
                return true;
            }
            var offset = enteredPlayerIds.Length;
            if (Array.IndexOf(enteredPlayerIds, localPlayerId) < 0) {
                var temp = new ushort[offset + 1];
                Array.Copy(enteredPlayerIds, temp, offset);
                temp[offset] = localPlayerId;
                enteredPlayerIds = temp;
                RequestSync();
            }
            return offset > 0;
        }

        bool RemovePlayerFromEntered() {
            if (!isSynced || !Utilities.IsValid(enteredPlayerIds)) return true;
            var length = enteredPlayerIds.Length;
            var index = Array.IndexOf(enteredPlayerIds, localPlayerId);
            if (index < 0) return length > 0;
            var offset = length - 1;
            if (offset == 0) {
                enteredPlayerIds = new ushort[0];
                RequestSync();
                return true;
            }
            var temp = new ushort[offset];
            Array.Copy(enteredPlayerIds, 0, temp, 0, index);
            Array.Copy(enteredPlayerIds, index + 1, temp, index, offset - index);
            enteredPlayerIds = temp;
            RequestSync();
            return offset > 0;
        }

        void RequestSync() {
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
        }
    }
}