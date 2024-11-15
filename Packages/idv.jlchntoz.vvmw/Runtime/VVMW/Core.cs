using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if AUDIOLINK_V1
using AudioLink;
#endif

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Core")]
    [DefaultExecutionOrder(0)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#vvmw-game-object")]
    public partial class Core : UdonSharpEventSender {
        const long OWNER_SYNC_COOLDOWN_TICKS = 3 * TimeSpan.TicksPerSecond;
        const long DOUBLE_CLICK_THRESHOLD_TICKS = 500 * TimeSpan.TicksPerMillisecond;
        const byte IDLE = 0, LOADING = 1, PLAYING = 2, PAUSED = 3;
        [SerializeField, LocalizedLabel] internal AbstractMediaPlayerHandler[] playerHandlers;
        [SerializeField, LocalizedLabel] VRCUrl defaultUrl;
        [SerializeField, LocalizedLabel] VRCUrl defaultQuestUrl;
        [SerializeField, LocalizedLabel, Range(0, 255)] int autoPlayPlayerType = 1;
        [SerializeField, LocalizedLabel] bool synced = true;
        [SerializeField, LocalizedLabel] int totalRetryCount = 3;
        [SerializeField, LocalizedLabel, Range(5, 20)] float retryDelay = 5.5F;
        [SerializeField, LocalizedLabel] float autoPlayDelay = 0;
        [UdonSynced] VRCUrl pcUrl, questUrl;
        VRCUrl localUrl, loadingUrl, lastUrl, altUrl;
        // When playing, it is the time when the video started playing;
        // When paused, it is the progress of the video in ticks.
        [UdonSynced] byte activePlayer;
        byte localActivePlayer, lastActivePlayer;
        // 0: Idle, 1: Loading, 2: Playing, 3: Paused
        [UdonSynced] byte state;

        [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))]
        bool loop;
        AbstractMediaPlayerHandler activeHandler;
        int retryCount = 0;
        bool isLoading, isLocalReloading, isError;
        VideoError lastError = VideoError.Unknown;
        string[] playerNames;
        bool trustUpdated, isTrusted;
        internal bool afterFirstRun;
        bool isOwnerSyncRequested, isReloadRequested;
        DateTime lastClickResyncTime;

        public string[] PlayerNames {
            get {
                if (playerNames == null || playerNames.Length != playerHandlers.Length) {
                    playerNames = new string[playerHandlers.Length];
                    for (int i = 0; i < playerNames.Length; i++)
                        playerNames[i] = playerHandlers[i].playerName;
                }
                return playerNames;
            }
        }

        public byte ActivePlayer {
            get => localActivePlayer;
            private set {
                if (value == localActivePlayer && activeHandler == (value == 0 ? null : playerHandlers[value - 1]))
                    return;
                localActivePlayer = value;
                var lastActiveHandler = activeHandler;
                bool wasPlaying = lastActiveHandler != null && lastActiveHandler.IsPlaying;
                activeHandler = null;
                for (int i = 0; i < playerHandlers.Length; i++) {
                    var handler = playerHandlers[i];
                    if (i + 1 == value) {
                        handler.IsActive = true;
                        activeHandler = handler;
                        SyncSpeed();
                    } else
                        handler.IsActive = false;
                }
                if (value == 0 && wasPlaying && !isLocalReloading && VRCUrl.IsNullOrEmpty(loadingUrl))
                    SendEvent("_onVideoEnd");
                _OnTextureChanged();
            }
        }

        public byte LastActivePlayer => lastActivePlayer;

        public bool Loop {
            get => loop;
            set {
                bool wasLoop = loop;
                loop = value;
                if (activeHandler != null) activeHandler.Loop = value;
                if (synced && wasLoop != value && Networking.IsOwner(gameObject))
                    RequestSerialization();
                #if AUDIOLINK_V1
                if (IsAudioLinked()) audioLink.SetMediaLoop(value ? MediaLoop.LoopOne : MediaLoop.None);
                #endif
            }
        }

        public bool IsAVPro => activeHandler != null && activeHandler.IsAvPro;

        public VRCUrl Url => localUrl;

        public VRCUrl LastUrl => lastUrl;

        public bool IsSynced => synced;

        public bool IsLoading => isLoading;

        public bool IsReady => activeHandler != null && activeHandler.IsReady && !isLoading;

        public bool IsPlaying => activeHandler != null && activeHandler.IsPlaying;

        public bool IsPaused => activeHandler != null && activeHandler.IsPaused;

        public byte State {
            get {
                if (activeHandler == null) return 0;
                if (activeHandler.IsPaused) return 5;
                if (activeHandler.IsPlaying) return 4;
                if (isError) return 2;
                if (isLoading) return 1;
                if (activeHandler.IsReady) return 3;
                return 0;
            }
        }

        public VideoError LastError => lastError;

        public bool IsStatic => activeHandler != null && activeHandler.IsStatic;

        public bool IsTrusted {
            get {
                if (!trustUpdated) {
                    isTrusted = activeHandler != null && activeHandler.IsCurrentUrlTrusted();
                    trustUpdated = true;
                }
                return isTrusted;
            }
        }

        VRCUrl URL {
            get => url;
            set => url = value ?? VRCUrl.Empty;
        }

        void OnEnable() {
            StartBroadcastScreenTexture();
            #if AUDIOLINK_V1
            SendCustomEventDelayedFrames(nameof(_RestoreAudioLinkState), 0);
            #endif
            if (afterFirstRun) return;
            url = VRCUrl.Empty;
            foreach (var handler in playerHandlers)
                handler.core = this;
            InitScreenProperties();
            UpdateVolume();
            if (!synced || Networking.IsOwner(gameObject)) SendCustomEventDelayedSeconds(nameof(_PlayDefaultUrl), autoPlayDelay);
            else if (synced) SendCustomEventDelayedSeconds(nameof(_RequestOwnerSync), autoPlayDelay + 3);
            afterFirstRun = true;
#if VRC_ENABLE_PLAYER_PERSISTENCE
            // Uncomment the following line when player persistence go live, currently it will breaks non-beta version clients.
            // RestoreFromPersistence(Networking.LocalPlayer);
#endif
        }

        void OnDisable() {
            StopBroadcastScreenTexture();
        }

        public void _PlayDefaultUrl() {
            if (!VRCUrl.IsNullOrEmpty(defaultUrl)) PlayUrl(null, 0);
        }

        public byte GetSuitablePlayerType(VRCUrl url) {
            if (VRCUrl.IsNullOrEmpty(url)) return 0;
            string urlStr = url.Get();
            int largestSupport = int.MinValue, largestSupportIndex = -1;
            for (int i = 0; i < playerHandlers.Length; i++) {
                var handler = playerHandlers[i];
                if (handler == null) continue;
                var support = handler.IsSupported(urlStr);
                if (support > largestSupport) {
                    largestSupport = support;
                    largestSupportIndex = i;
                }
            }
            return (byte)(largestSupport <= 0 ? 0 : largestSupportIndex + 1);
        }

        public void PlayUrl(VRCUrl url, byte playerType) => PlayUrl(url, null, playerType);

        public void PlayUrl(VRCUrl pcUrl, VRCUrl questUrl, byte playerType) {
            isError = false;
            VRCUrl url;
            #if UNITY_ANDROID || UNITY_IOS
            url = questUrl;
            if (VRCUrl.IsNullOrEmpty(url))
            #endif
                url = pcUrl;
            if (VRCUrl.IsNullOrEmpty(url)) {
                if (!VRCUrl.IsNullOrEmpty(defaultUrl)) {
                    pcUrl = defaultUrl;
                    questUrl = defaultQuestUrl;
                } else return;
                #if UNITY_ANDROID || UNITY_IOS
                url = questUrl;
                if (VRCUrl.IsNullOrEmpty(url))
                #endif
                    url = pcUrl;
                playerType = (byte)autoPlayPlayerType;
            }
            #if UNITY_ANDROID || UNITY_IOS
            if (!VRCUrl.IsNullOrEmpty(questUrl)) {
                localUrl = questUrl;
                altUrl = pcUrl;
            } else
            #endif
            {
                localUrl = pcUrl;
                altUrl = questUrl;
            }
            time = 0;
            ActivePlayer = playerType;
            loadingUrl = null;
            retryCount = 0;
            lastError = VideoError.Unknown;
            trustUpdated = false;
            isLoading = true;
            isLocalReloading = false;
            SendEvent("_OnVideoBeginLoad");
            #if AUDIOLINK_V1
            SetAudioLinkPlayBackState(MediaPlaying.Loading);
            #endif
            activeHandler.LoadUrl(url, false);
            if (RequestSync()) state = LOADING;
            LoadYTTL();
        }

        [Obsolete("Use PlayUrl(VRCUrl, VRCUrl, byte) instead.")]
        public void PlayUrlMP(VRCUrl pcUrl, VRCUrl questUrl, byte playerType) => PlayUrl(pcUrl, questUrl, playerType);

        public override void OnVideoError(VideoError videoError) {
            isError = true;
            lastError = videoError;
            SendCustomEventDelayedFrames(nameof(_DeferSendErrorEvent), 0);
            #if AUDIOLINK_V1
            SetAudioLinkPlayBackState(MediaPlaying.Error);
            #endif
            if (retryCount < totalRetryCount) {
                retryCount++;
                loadingUrl = localUrl;
                switch (videoError) {
                    case VideoError.InvalidURL:
                        retryCount = 0;
                        break;
                    default:
                        SendCustomEventDelayedSeconds(nameof(_ReloadUrl), retryDelay);
                        return;
                }
            }
            isLoading = false;
        }

        public void _DeferSendErrorEvent() => SendEvent("_OnVideoError");

        public void _ReloadUrl() {
            isError = false;
            if (VRCUrl.IsNullOrEmpty(loadingUrl) ||
                (synced && state == IDLE) ||
                !loadingUrl.Equals(localUrl)) {
                return;
            }
            if (!synced && localUrl.Equals(defaultUrl)) {
                PlayUrl(null, 0);
                return;
            }
            isLocalReloading = true;
            activeHandler.LoadUrl(loadingUrl, true);
            isLoading = true;
            SendEvent("_OnVideoBeginLoad");
        }

        public void GlobalSync() {
            if (!synced) {
                LocalSync();
                return;
            }
            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(LocalSync));
        }

        public void LocalSync() {
            if (synced) {
                var currentTime = Networking.GetNetworkDateTime();
                if (lastClickResyncTime < currentTime) {
                    if ((currentTime - lastClickResyncTime).Ticks < DOUBLE_CLICK_THRESHOLD_TICKS) {
                        lastClickResyncTime = currentTime + TimeSpan.FromTicks(OWNER_SYNC_COOLDOWN_TICKS);
                        if (!isOwnerSyncRequested) {
                            isOwnerSyncRequested = true;
                            SendCustomEventDelayedFrames(nameof(_RequestOwnerSync), 0);
                        }
                        return;
                    }
                    lastClickResyncTime = currentTime;
                }
            }
            ReloadUrlCore();
        }

        void ReloadUrlCore() {
            if (synced) {
                #if UNITY_ANDROID || UNITY_IOS
                if (!VRCUrl.IsNullOrEmpty(questUrl))
                    localUrl = questUrl;
                else
                #endif
                    localUrl = pcUrl;
            } else if (VRCUrl.IsNullOrEmpty(localUrl))
                localUrl = defaultUrl;
            loadingUrl = localUrl;
            trustUpdated = false;
            if (VRCUrl.IsNullOrEmpty(loadingUrl)) return;
            retryCount = 0;
            _ReloadUrl();
        }

        public void _RequestReloadUrl() {
            if (isReloadRequested) return;
            isReloadRequested = true;
            SendCustomEventDelayedSeconds(nameof(_ReloadUrlCore), 1F);
        }

        public void _ReloadUrlCore() {
            isReloadRequested = false;
            if (enabled && gameObject.activeInHierarchy) ReloadUrlCore();
        }

        public void Play() {
            if (activeHandler == null) return;
            activeHandler.Play();
            RequestSync();
        }

        public void Pause() {
            if (activeHandler == null) return;
            activeHandler.Pause();
            RequestSync();
        }

        public void Stop() {
            var handler = activeHandler;
            if (handler == null) return;
            handler.Stop();
            if (!handler.IsReady) { // Cancel loading if it is still loading
                ActivePlayer = 0;
                loadingUrl = null;
                lastError = VideoError.Unknown;
                isLoading = false;
                isLocalReloading = false;
                isError = false;
            }
            RequestSync();
        }

        public override void OnVideoReady() {
            loadingUrl = null;
            lastError = VideoError.Unknown;
            retryCount = 0;
            isLoading = false;
            isError = false;
            float videoTime = 0;
            if (isLocalReloading) videoTime = CalcVideoTime();
            isLocalReloading = false;
            activeHandler.Loop = loop;
            SendEvent("_onVideoReady");
            if (!synced) {
                activeHandler.Play();
                return;
            }
            int intState = state;
            switch (intState) {
                case IDLE:
                case LOADING:
                    if (Networking.IsOwner(gameObject))
                        activeHandler.Play();
                    else if (synced) // Try to ad-hoc sync
                        SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerSync));
                    break;
                case PLAYING: activeHandler.Play(); break;
                case PAUSED: activeHandler.Pause(); break;
                default: return;
            }
            if (videoTime > 0) activeHandler.Time = videoTime;
        }

        public override void OnVideoStart() {
            SendEvent("_onVideoStart");
            StartSyncTime();
        }

        public override void OnVideoPlay() {
            SendEvent("OnVideoPlay");
            SetAudioPitch();
            AssignAudioLinkSource();
            if (!synced || !Networking.IsOwner(gameObject) || isLocalReloading) return;
            state = PLAYING;
            StartSyncTime();
        }

        public override void OnVideoPause() {
            SendEvent("OnVideoPause");
            #if AUDIOLINK_V1
            SetAudioLinkPlayBackState(MediaPlaying.Paused);
            #endif
            if (!synced || !Networking.IsOwner(gameObject) || isLocalReloading) return;
            state = PAUSED;
            StartSyncTime();
        }

        public override void OnVideoEnd() {
            if (!VRCUrl.IsNullOrEmpty(loadingUrl) || isLocalReloading) return;
            lastActivePlayer = activePlayer;
            lastUrl = localUrl;
            ActivePlayer = 0;
            localUrl = synced ? null : defaultUrl;
            trustUpdated = false;
            SendEvent("_onVideoEnd");
            #if AUDIOLINK_V1
            if (audioLink != null) audioLink.SetMediaPlaying(MediaPlaying.Stopped);
            #endif
            _OnTextureChanged();
            if (!synced || !Networking.IsOwner(gameObject)) return;
            state = IDLE;
            RequestSerialization();
        }

        public override void OnVideoLoop() {
            SendEvent("_onVideoLoop");
            activeHandler.Time = 0;
            if (!synced || !Networking.IsOwner(gameObject)) return;
            state = PLAYING;
            RequestSerialization();
        }

        public override void OnPreSerialization() {
            if (!synced || isLocalReloading) return;
            lastSyncTime = Networking.GetNetworkDateTime();
            ownerServerTime = lastSyncTime.Ticks;
            syncedSpeed = speed;
            if (activeHandler == null) {
                activePlayer = 0;
                state = IDLE;
                time = 0;
                pcUrl = null;
                questUrl = null;
            } else {
                activePlayer = localActivePlayer;
                #if UNITY_ANDROID || UNITY_IOS
                if (!VRCUrl.IsNullOrEmpty(altUrl)) {
                    pcUrl = altUrl;
                    questUrl = localUrl;
                } else
                #endif
                {
                    pcUrl = localUrl;
                    questUrl = altUrl;
                }
                if (activeHandler.IsReady) {
                    state = activeHandler.IsPlaying ? PLAYING : PAUSED;
                    time = CalcSyncTime(out actualSpeed);
                } else {
                    state = VRCUrl.IsNullOrEmpty(localUrl) ? IDLE : LOADING;
                    time = 0;
                    actualSpeed = 1;
                }
            }
            syncedActualSpeed = actualSpeed;
        }

        public override void OnDeserialization(DeserializationResult result) {
            if (!synced) return;
            ActivePlayer = activePlayer;
            float sendTime = result.sendTime;
            syncLatency = sendTime > 0 ? // if send time is negative, which means it was sent before join thus this is not valid.
                (float)(ownerServerTime - Networking.GetNetworkDateTime().Ticks) / TimeSpan.TicksPerSecond +
                UnityEngine.Time.realtimeSinceStartup - sendTime : 0;
            actualSpeed = syncedActualSpeed;
            if (speed != syncedSpeed) {
                speed = syncedSpeed;
                SyncSpeed();
            }
            VRCUrl url = null;
            #if UNITY_ANDROID || UNITY_IOS
            if (!VRCUrl.IsNullOrEmpty(questUrl)) {
                url = questUrl;
                altUrl = pcUrl;
            } else
            #endif
            {
                url = pcUrl;
                altUrl = questUrl;
            }
            bool shouldReload = state != IDLE && localUrl != url && (VRCUrl.IsNullOrEmpty(localUrl) || VRCUrl.IsNullOrEmpty(url) || !localUrl.Equals(url));
            if (shouldReload) {
                if (activeHandler == null) {
                    Debug.LogWarning($"[VVMW] Owner serialization incomplete, will queue a sync request.");
                    SendCustomEventDelayedSeconds(nameof(_RequestOwnerSync), 1);
                    return;
                }
                loadingUrl = null;
                retryCount = 0;
                lastError = VideoError.Unknown;
                isLoading = true;
                isError = false;
                trustUpdated = false;
                SendEvent("_OnVideoBeginLoad");
                activeHandler.LoadUrl(url, false);
            }
            localUrl = url;
            if (shouldReload) LoadYTTL();
            if (activeHandler != null && activeHandler.IsReady) {
                bool forceSyncTime = false;
                int intState = state;
                switch (intState) {
                    case IDLE:
                        if (activeHandler.IsPlaying) activeHandler.Stop();
                        break;
                    case PAUSED:
                    case LOADING:
                        if (!activeHandler.IsPaused) activeHandler.Pause();
                        break;
                    case PLAYING:
                        if (activeHandler.IsPaused || !activeHandler.IsPlaying) {
                            activeHandler.Play();
                            forceSyncTime = true;
                        }
                        break;
                }
                if (!isLocalReloading && !isLoading) SyncTime(forceSyncTime);
            }
        }

        bool RequestSync() {
            if (!synced) return false;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
            return true;
        }
    }
}
