using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        bool ownerHealthCheckPending, ownerHealthCheckAwaiting;
        float lastOwnerSimulationTime;
        int ownerSimulationTurnCount;

#if COMPILER_UDONSHARP
        public
#endif
        void _DoOwnerHealthCheck() => DoHealthCheck(nameof(Ping));

        void DoHealthCheckNoSync() => DoHealthCheck(nameof(PingNoSync));

        void DoHealthCheck(string remoteCallback) {
            if (!synced || ownerHealthCheckPending || !isActiveAndEnabled) return;
            var owner = Networking.GetOwner(gameObject);
            if (owner.isLocal) return;
            ownerHealthCheckPending = true;
            if (!ownerHealthCheckAwaiting) {
                ownerHealthCheckAwaiting = true;
                lastOwnerSimulationTime = Networking.SimulationTime(owner);
                ownerSimulationTurnCount = 0;
                SendCustomEventDelayedSeconds(nameof(_HealthCheck), 0.1F);
            }
            SendCustomNetworkEvent(NetworkEventTarget.Owner, remoteCallback);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void Ping() {
            if (!synced || !Networking.IsOwner(gameObject) || !isActiveAndEnabled) return;
            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(Pong));
            if ((Networking.GetNetworkDateTime() - lastSyncTime).Ticks < OWNER_SYNC_COOLDOWN_TICKS) return;
            RequestSerialization();
        }
        

#if COMPILER_UDONSHARP
        public
#endif
        void PingNoSync() {
            if (!synced || !Networking.IsOwner(gameObject) || !isActiveAndEnabled) return;
            SendCustomNetworkEvent(NetworkEventTarget.Others, nameof(Pong));
        }

#if COMPILER_UDONSHARP
        public
#endif
        void Pong() {
            ownerHealthCheckPending = false;
            healthCheckCounter = 0;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _HealthCheck() {
            if (ownerHealthCheckAwaiting && ownerHealthCheckPending && isActiveAndEnabled && synced) {
                var owner = Networking.GetOwner(gameObject);
                if (!owner.isLocal) {
                    var simulationTime = Networking.SimulationTime(owner);
                    if (simulationTime != lastOwnerSimulationTime) {
                        lastOwnerSimulationTime = simulationTime;
                        if (++ownerSimulationTurnCount >= 3) {
                            Networking.SetOwner(Networking.LocalPlayer, gameObject);
                            ownerHealthCheckAwaiting = false;
                            ownerHealthCheckPending = false;
                            return;
                        }
                    }
                    SendCustomEventDelayedSeconds(nameof(_HealthCheck), 0.1F);
                    return;
                }
            }
            ownerHealthCheckAwaiting = false;
            ownerHealthCheckPending = false;
        }
    }
}
