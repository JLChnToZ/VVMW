using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
#if COMPILER_UDONSHARP && UDON_NETWORKING_UPDATED
using VRC.SDK3.UdonNetworkCalling;
#endif
using VRC.Udon.Common.Interfaces;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        [SerializeField, LocalizedLabel, Range(0, 5)] float timeDriftDetectThreshold = 0.9F;
        [UdonSynced] long ownerServerTime;
        // When playing, it is the time when the video started playing;
        // When paused, it is the progress of the video in ticks.
        [UdonSynced] long time;
        [UdonSynced] float rangeLoopStart = -1, rangeLoopEnd = -1;
        [UdonSynced] float syncedSpeed = 1, syncedActualSpeed = 1;
        [FieldChangeCallback(nameof(PerformerId))]
        [UdonSynced] ushort performerId;
        [FieldChangeCallback(nameof(SyncOffset))]
        float syncOffset = 0;
        [FieldChangeCallback(nameof(Speed))]
        float speed = 1;
        float actualSpeed = 1;
        float localRangeLoopStart = -1, localRangeLoopEnd = -1;
        float rangeLoopDuration;
        float syncLatency;
        float lastSyncRawTime;
        bool isBuffering;
        bool isResyncTime;
        bool isCheckingRangeLoop;
        DateTime lastSyncTime;
        VRCPlayerApi performer;

        /// <summary>
        /// The current time of the video in seconds.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be zero.
        /// </remarks>
        // Find this code useful to your project? You are welcome to adopt it.
        // If you are respectful, please kindly leave a credit that you got inspired here;
        // or you can be an asshole who just rip it off, refactor it and then claim you made it.
        public float Time {
            get => Utilities.IsValid(activeHandler) ? activeHandler.Time : 0;
            private set {
                if (IsRangeLooping) value = CalculateLoopTime(value);
                activeHandler.Time = value;
                lastSyncRawTime = value;
                isBuffering = false; // Reset buffering state
            }
        }

        /// <summary>
        /// The duration of the video in seconds.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be infinity.
        /// </remarks>
        public float Duration => Utilities.IsValid(activeHandler) ? activeHandler.Duration : 0;

        /// <summary>
        /// The start time of the range loop in seconds. If it is negative, the range loop is disabled.
        /// </summary>
        public float RangeLoopStart => localRangeLoopStart;

        /// <summary>
        /// The end time of the range loop in seconds. If it is negative, the range loop is disabled.
        /// </summary>
        public float RangeLoopEnd => localRangeLoopEnd;

        /// <summary>
        /// The offset of the video time to other players, in seconds.
        /// </summary>
        public float SyncOffset {
            get => syncOffset;
            set {
                if (syncOffset == value) return;
                syncOffset = value;
                SendEvent("_OnSyncOffsetChange");
                if (synced && Utilities.IsValid(activeHandler) && activeHandler.IsPlaying) {
                    var duration = activeHandler.Duration;
                    if (duration <= 0 || float.IsInfinity(duration)) return;
                    Time = CalcVideoTime();
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        /// <summary>
        /// The playback progress of the video, from 0 to 1.
        /// </summary>
        /// <remarks>
        /// If it is a live stream, this value will be zero, and setting this value will have no effect.
        /// </remarks>
        public float Progress {
            get {
                if (!Utilities.IsValid(activeHandler)) return 0;
                var duration = activeHandler.Duration;
                if (duration <= 0 || float.IsInfinity(duration)) return 0;
                return activeHandler.Time / duration;
            }
            set {
                if (!Utilities.IsValid(activeHandler)) return;
                var duration = activeHandler.Duration;
                if (duration <= 0 || float.IsInfinity(duration)) return;
                Time = duration * value;
                RequestSync();
            }
        }

        /// <summary>
        /// Is current video player backend support speed adjustment.
        /// </summary>
        public bool SupportSpeedAdjustment => Utilities.IsValid(activeHandler) && activeHandler.SupportSpeedAdjustment;

        /// <summary>
        /// The playback speed of the video.
        /// </summary>
        /// <remarks>
        /// The value will be clamped between 0.1 and 2.
        /// </remarks>
        public float Speed {
            get => speed;
            set {
                if (speed == value) return;
                speed = Mathf.Clamp(value, 0.1F, 2);
                SyncSpeed();
                RequestSync();
            }
        }

        /// <summary>
        /// Whether the video player is currently looping in a specific range.
        /// </summary>
        public bool IsRangeLooping => localRangeLoopStart >= 0 && localRangeLoopEnd > localRangeLoopStart;

        /// <summary>
        /// Gets the performer of the video player playback.
        /// </summary>
        public VRCPlayerApi Performer => performer;

        ushort PerformerId {
            set {
                performerId = value;
                if (performerId == 0)
                    performer = null;
                else
                    performer = VRCPlayerApi.GetPlayerById(performerId);
                SendEvent("_OnPerformerChange");
            }
        }

        float PerformerLatency => Utilities.IsValid(performer) && !performer.isLocal ? UnityEngine.Time.realtimeSinceStartup - Networking.SimulationTime(performer) : 0;

        /// <summary>
        /// Event entry point on the ownership of the video player is transferred.
        /// Internal use only. Do not call this method.
        /// </summary>
        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            if (!player.isLocal) isLocalReloading = false;
            syncLatency = 0;
        }

        void StartSyncTime() {
            StartCheckRangeLoop();
            if (!synced) return;
            SyncTime(true);
            if (!isResyncTime) {
                isResyncTime = true;
                SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
            }
        }

        void StartCheckRangeLoop() {
            if (isCheckingRangeLoop) return;
            isCheckingRangeLoop = true;
            SendCustomEventDelayedFrames(nameof(_CheckRangeLoop), 0);
        }

        float CalculateLoopTime(float value) => localRangeLoopStart + Mathf.Repeat(value - localRangeLoopStart, rangeLoopDuration);

#if COMPILER_UDONSHARP
        public
#endif
        void _AutoSyncTime() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || !Utilities.IsValid(activeHandler) || !activeHandler.IsReady) {
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
            if (!Utilities.IsValid(activeHandler)) {
                actualSpeed = 1;
                return 0;
            }
            actualSpeed = activeHandler.Speed;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            var videoTime = Mathf.Repeat(activeHandler.Time, duration);
            var syncTime = (long)((videoTime / actualSpeed - syncOffset + PerformerLatency) * TimeSpan.TicksPerSecond);
            if (activeHandler.IsPlaying) syncTime = Networking.GetNetworkDateTime().Ticks - syncTime;
            if (synced) syncedActualSpeed = actualSpeed;
            return syncTime;
        }

        float CalcVideoTime() {
            if (!Utilities.IsValid(activeHandler)) return 0;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            float videoTime;
            int intState = state;
            switch (intState) {
                case PLAYING: videoTime = ((float)(Networking.GetNetworkDateTime().Ticks - time) / TimeSpan.TicksPerSecond + syncOffset + syncLatency - PerformerLatency) * actualSpeed; break;
                case PAUSED: videoTime = (float)time / TimeSpan.TicksPerSecond; break;
                default: return 0;
            }
            return loop ? Mathf.Repeat(videoTime, duration) : Mathf.Clamp(videoTime, 0, duration);
        }

        void SyncTime(bool forced) {
            if (Networking.IsOwner(gameObject)) {
                if (!forced && Utilities.IsValid(activeHandler) && IsRangeLooping) {
                    var halfRange = rangeLoopDuration / 2;
                    var diff = CalculateLoopTime(CalcVideoTime()) - activeHandler.Time;
                    if (diff > halfRange) diff -= rangeLoopDuration;
                    else if (diff < -halfRange) diff += rangeLoopDuration;
                    if (Mathf.Abs(diff) / activeHandler.Speed < timeDriftDetectThreshold) return;
                    forced = true;
                }
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
                var t2 = activeHandler.Time;
                // Is playing + time (progress) not changed since last check = video is buffering or paused
                // Do not align the time this case as it may cause current non-ready buffer to be discard and rebuffered
                // causing the video to be stuck in buffering state.
                bool isPlaying = activeHandler.IsPlaying;
                if (!isPlaying || t2 != lastSyncRawTime) {
                    // If it was buffering, we shift the time a bit further, estimates user loading time for catching up
                    if (isBuffering) {
                        if (isPlaying) {
                            float tCatchup = t2 - lastSyncRawTime;
                            if (tCatchup > timeDriftDetectThreshold) t += tCatchup;
                        }
                        isBuffering = false;
                    }
                    lastSyncRawTime = t2;
                    if (!forced) {
                        if (loop) t2 = Mathf.Repeat(t2, duration);
                        forced = Mathf.Abs(t2 - t) >= timeDriftDetectThreshold;
                    }
                } else
                    isBuffering = isPlaying;
                if (forced) {
                    Time = t;
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        /// <summary>
        /// Set the video player to loop in a specific range.
        /// </summary>
        /// <param name="start">The start time of the range loop in seconds. It must be non-negative and less than the end time.</param>
        /// <param name="end">The end time of the range loop in seconds. It must be greater than the start time.</param>
        public void SetRangeLoop(float start, float end) {
            var duration = Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                _ClearRangeLoop();
                return;
            }
            SetRangeInternal(Mathf.Clamp(start, 0, end), Mathf.Clamp(end, start, duration));
            RequestSync();
        }

        void SetRangeInternal(float start, float end) {
            if (Mathf.Approximately(localRangeLoopStart, start) && Mathf.Approximately(localRangeLoopEnd, end)) return;
            bool wasRangeLoop = IsRangeLooping;
            localRangeLoopStart = start;
            localRangeLoopEnd = end;
            rangeLoopDuration = end - start;
            var isRangeLoop = IsRangeLooping;
            if (wasRangeLoop != isRangeLoop) SendEvent("_OnRangeLoopToggled");
            if (isRangeLoop) {
                SendEvent("_OnRangeLoopChange");
                StartCheckRangeLoop();
            }
        }

        /// <summary>
        /// Clear the range loop and disable looping in a specific range.
        /// </summary>
        public void _ClearRangeLoop() {
            if (!IsRangeLooping) return;
            localRangeLoopStart = -1;
            localRangeLoopEnd = -1;
            rangeLoopDuration = 0;
            SendEvent("_OnRangeLoopToggled");
            RequestSync();
        }

        // Find this code useful to your project? You are welcome to adopt it.
        // If you are respectful, please kindly leave a credit that you got inspired here;
        // or you can be an asshole who just rip it off, refactor it and then claim you made it.
#if COMPILER_UDONSHARP
        public
#endif
        void _CheckRangeLoop() {
            if (!Utilities.IsValid(activeHandler) || !activeHandler.IsPlaying || localRangeLoopStart < 0 || localRangeLoopEnd <= localRangeLoopStart) {
                isCheckingRangeLoop = false;
                return;
            }
            SendCustomEventDelayedFrames(nameof(_CheckRangeLoop), 0);
            var time = activeHandler.Time;
            if (time < localRangeLoopStart) {
                Time = localRangeLoopStart;
                SendEvent("_OnTimeDrift");
                return;
            }
            if (time > localRangeLoopEnd) {
                Time = time; // The time will be wrapped in Time's setter
                SendEvent("_OnTimeDrift");
                return;
            }
        }

        /// <summary>
        /// Request the owner to synchronize the video player state.
        /// </summary>
        /// <remarks>
        /// If synchronization is disabled, this method will have no effect.
        /// </remarks>
        public void _RequestOwnerSync() {
            isOwnerSyncRequested = false;
            if (!synced) return;
            SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerSync));
        }

        /// <summary>
        /// Let current user be the performer of current video player playback. (Experimental)
        /// </summary>
        /// <param name="enable">Enable or disable the performance mode.</param>
        /// <remarks>
        /// Video playback time will be auto adjusted to cancel-out the latency between the performer and audience (other users).
        /// </remarks>
        public void SetOwnPerformer(bool enable) {
            if (!synced) return;
            var player = Networking.LocalPlayer;
            if (enable) {
                var newHostId = player.playerId;
                if (newHostId == performerId) return;
                PerformerId = (ushort)newHostId;
            } else if (performerId == player.playerId) {
                PerformerId = 0;
            } else return;
            if (!Networking.IsOwner(gameObject))
                Networking.SetOwner(player, gameObject);
            RequestSerialization();
        }

#if COMPILER_UDONSHARP
#if UDON_NETWORKING_UPDATED
        [NetworkCallable]
#endif
        public
#endif
        void OwnerSync() {
            if (!Networking.IsOwner(gameObject) || !synced) return;
            if ((Networking.GetNetworkDateTime() - lastSyncTime).Ticks < OWNER_SYNC_COOLDOWN_TICKS) return;
            RequestSerialization();
        }

        void SyncSpeed() {
            if (Utilities.IsValid(activeHandler) && activeHandler.SupportSpeedAdjustment)
                activeHandler.Speed = speed;
            SetAudioPitch();
            SendEvent("_OnSpeedChange");
        }
    }
}
