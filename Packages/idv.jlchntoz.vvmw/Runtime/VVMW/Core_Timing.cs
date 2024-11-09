using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon.Common.Interfaces;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel, Range(0, 5)] float timeDriftDetectThreshold = 0.9F;
        [UdonSynced] long ownerServerTime;
        [UdonSynced] long time;
        [UdonSynced] float syncedSpeed = 1, syncedActualSpeed = 1;
        [FieldChangeCallback(nameof(SyncOffset))]
        float syncOffset = 0;
        [FieldChangeCallback(nameof(Speed))]
        float speed = 1, actualSpeed = 1;
        float syncLatency;
        bool isResyncTime;
        DateTime lastSyncTime;

        public float Time => activeHandler != null ? activeHandler.Time : 0;

        public float Duration => activeHandler != null ? activeHandler.Duration : 0;

        public float SyncOffset {
            get => syncOffset;
            set {
                if (syncOffset == value) return;
                syncOffset = value;
                SendEvent("_OnSyncOffsetChange");
                if (synced && activeHandler != null && activeHandler.IsPlaying) {
                    var duration = activeHandler.Duration;
                    if (duration <= 0 || float.IsInfinity(duration)) return;
                    activeHandler.Time = CalcVideoTime();
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        public float Progress {
            get {
                if (activeHandler == null) return 0;
                var duration = activeHandler.Duration;
                if (activeHandler.Duration <= 0 || float.IsInfinity(duration)) return 0;
                return activeHandler.Time / activeHandler.Duration;
            }
            set {
                if (activeHandler == null) return;
                var duration = activeHandler.Duration;
                if (activeHandler.Duration <= 0 || float.IsInfinity(duration)) return;
                activeHandler.Time = duration * value;
                RequestSync();
            }
        }

        public bool SupportSpeedAdjustment => activeHandler != null && activeHandler.SupportSpeedAdjustment;

        public float Speed {
            get => speed;
            set {
                if (speed == value) return;
                speed = Mathf.Clamp(value, 0.1F, 2);
                SyncSpeed();
                RequestSync();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            if (!player.isLocal) isLocalReloading = false;
            syncLatency = 0;
        }

        void StartSyncTime() {
            if (!synced) return;
            SyncTime(true);
            if (!isResyncTime) {
                isResyncTime = true;
                SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
            }
        }

        public void _AutoSyncTime() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || activeHandler == null || !activeHandler.IsReady) {
                isResyncTime = false;
                return;
            }
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                isResyncTime = false;
                RequestSerialization();
                return;
            }
            SyncTime(false);
            SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
        }

        long CalcSyncTime(out float actualSpeed) {
            if (activeHandler == null) {
                actualSpeed = 1;
                return 0;
            }
            actualSpeed = activeHandler.Speed;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            var videoTime = Mathf.Repeat(activeHandler.Time, duration);
            var syncTime = (long)((videoTime / actualSpeed - syncOffset) * TimeSpan.TicksPerSecond);
            if (activeHandler.IsPlaying) syncTime = Networking.GetNetworkDateTime().Ticks - syncTime;
            if (synced) syncedActualSpeed = actualSpeed;
            return syncTime;
        }

        float CalcVideoTime() {
            if (activeHandler == null) return 0;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            float videoTime;
            int intState = state;
            switch (intState) {
                case PLAYING: videoTime = ((float)(Networking.GetNetworkDateTime().Ticks - time) / TimeSpan.TicksPerSecond + syncOffset + syncLatency) * actualSpeed; break;
                case PAUSED: videoTime = (float)time / TimeSpan.TicksPerSecond; break;
                default: return 0;
            }
            if (loop) videoTime = Mathf.Repeat(videoTime, duration);
            return videoTime;
        }

        void SyncTime(bool forced) {
            if (Networking.IsOwner(gameObject)) {
                var newTime = CalcSyncTime(out float speed);
                if (forced || Mathf.Abs((float)(newTime - time) / TimeSpan.TicksPerSecond) >= timeDriftDetectThreshold) {
                    time = newTime;
                    actualSpeed = speed;
                    RequestSerialization();
                }
            } else {
                var duration = activeHandler.Duration;
                if (duration <= 0 || float.IsInfinity(duration)) return;
                float t = CalcVideoTime();
                if (!forced) {
                    var t2 = activeHandler.Time;
                    if (loop) t2 = Mathf.Repeat(t2, duration);
                    forced = Mathf.Abs(t2 - t) >= timeDriftDetectThreshold;
                }
                if (forced) {
                    activeHandler.Time = t;
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        public void _RequestOwnerSync() {
            isOwnerSyncRequested = false;
            if (!synced) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerSync));
        }

        public void OwnerSync() {
            if (!Networking.IsOwner(gameObject) || !synced) return;
            if ((Networking.GetNetworkDateTime() - lastSyncTime).Ticks < OWNER_SYNC_COOLDOWN_TICKS) return;
            RequestSerialization();
        }

        void SyncSpeed() {
            if (activeHandler != null && activeHandler.SupportSpeedAdjustment)
                activeHandler.Speed = speed;
            SetAudioPitch();
            SendEvent("_OnSpeedChange");
        }
    }
}
