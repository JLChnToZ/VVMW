﻿using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Data;
using VRC.Udon.Common;
using VRC.Udon.Common.Interfaces;
using VVMW.ThirdParties.Yttl;
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
    public class Core : UdonSharpEventSender {
        const long OWNER_SYNC_COOLDOWN_TICKS = 3 * TimeSpan.TicksPerSecond;
        const long DOUBLE_CLICK_THRESHOLD_TICKS = 500 * TimeSpan.TicksPerMillisecond;
        const byte IDLE = 0, LOADING = 1, PLAYING = 2, PAUSED = 3;
        Vector4 normalST = new Vector4(1, 1, 0, 0), flippedST = new Vector4(1, -1, 0, 1);
        Rect normalRect = new Rect(0, 0, 1, 1), flippedRect = new Rect(0, 1, 1, -1);
        [SerializeField, LocalizedLabel] internal AbstractMediaPlayerHandler[] playerHandlers;
        [SerializeField, LocalizedLabel] AudioSource[] audioSources;
        [SerializeField, LocalizedLabel] VRCUrl defaultUrl;
        [SerializeField, LocalizedLabel] VRCUrl defaultQuestUrl;
        [SerializeField, LocalizedLabel, Range(0, 255)] int autoPlayPlayerType = 1;
        [SerializeField, LocalizedLabel] bool synced = true;
        [SerializeField, LocalizedLabel] int totalRetryCount = 3;
        [SerializeField, LocalizedLabel, Range(5, 20)] float retryDelay = 5.5F;
        [SerializeField, LocalizedLabel] float autoPlayDelay = 0;
        [SerializeField, LocalizedLabel, Range(0, 1), FieldChangeCallback(nameof(Volume))]
        float defaultVolume = 1;
        [SerializeField, LocalizedLabel, FieldChangeCallback(nameof(Muted))]
        bool defaultMuted = false;
        [SerializeField, LocalizedLabel] Texture defaultTexture;
        [SerializeField] UnityEngine.Object[] screenTargets;
        [SerializeField] int[] screenTargetModes;
        [SerializeField] int[] screenTargetIndeces;
        [SerializeField] string[] screenTargetPropertyNames, avProPropertyNames;
        [SerializeField] Texture[] screenTargetDefaultTextures;
        [SerializeField, LocalizedLabel] bool broadcastScreenTexture;
        [SerializeField, LocalizedLabel] string broadcastScreenTextureName = "_Udon_VideoTex";
        [SerializeField, LocalizedLabel, Range(0, 10)] float realtimeGIUpdateInterval = 0;
        [SerializeField, LocalizedLabel, Range(0, 5)] float timeDriftDetectThreshold = 0.9F;
        int[] screenTargetPropertyIds, avProPropertyIds;
        [FieldChangeCallback(nameof(SyncOffset))]
        float syncOffset = 0;
        [FieldChangeCallback(nameof(Speed))]
        float speed = 1;
        float actualSpeed = 1;
        [UdonSynced] VRCUrl pcUrl, questUrl;
        VRCUrl localUrl, loadingUrl, lastUrl, altUrl;
        // When playing, it is the time when the video started playing;
        // When paused, it is the progress of the video in ticks.
        [UdonSynced] long time;
        [UdonSynced] byte activePlayer;
        byte localActivePlayer, lastActivePlayer;
        // 0: Idle, 1: Loading, 2: Playing, 3: Paused
        [UdonSynced] byte state;
        [UdonSynced] long ownerServerTime;
        [UdonSynced] float syncedSpeed = 1, syncedActualSpeed = 1;
        [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))]
        bool loop;
        [Locatable(
            "AudioLink.AudioLink, AudioLink", "VRCAudioLink.AudioLink, AudioLink",
            InstaniatePrefabPath = "Packages/com.llealloo.audiolink/Runtime/AudioLink.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        ), SerializeField, LocalizedLabel]
        #if AUDIOLINK_V1
        AudioLink.AudioLink audioLink;
        #else
        UdonSharpBehaviour audioLink;
        #endif
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/Third-Parties/YTTL/YTTL Manager.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        ), SerializeField, LocalizedLabel] YttlManager yttl;
        AbstractMediaPlayerHandler activeHandler;
        int retryCount = 0;
        bool isLoading, isLocalReloading, isResyncTime, isError, isSyncAudioLink;
        VideoError lastError = VideoError.Unknown;
        string[] playerNames;
        bool trustUpdated, isTrusted;
        MaterialPropertyBlock screenTargetPropertyBlock;
        AudioSource assignedAudioSource;
        bool isRealtimeGIUpdaterRunning;
        internal bool afterFirstRun;
        bool isOwnerSyncRequested, isReloadRequested;
        DateTime lastSyncTime, lastClickResyncTime;
        float syncLatency;

        // Yttl Receivers
        [NonSerialized, FieldChangeCallback(nameof(URL))]
        public VRCUrl url = VRCUrl.Empty;
        [NonSerialized, FieldChangeCallback(nameof(Author))]
        public string author = "";
        [NonSerialized, FieldChangeCallback(nameof(Title))]
        public string title = "";
        [NonSerialized, FieldChangeCallback(nameof(ViewCount))]
        public string viewCount = "";
        [NonSerialized, FieldChangeCallback(nameof(Description))]
        public string description = "";
        bool hasCustomTitle;
        int broadcastTextureId;
        DataDictionary screenSharedProperties;

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

        public float Volume {
            get => defaultMuted ? 0 : defaultVolume;
            set {
                defaultVolume = Mathf.Clamp01(value);
                if (value > 0) defaultMuted = false;
                UpdateVolume();
            }
        }

        public bool Muted {
            get => defaultMuted;
            set {
                defaultMuted = value;
                UpdateVolume();
            }
        }

        #if AUDIOLINK_V1
        public AudioLink.AudioLink AudioLink
        #else
        public UdonSharpBehaviour AudioLink
        #endif
        {
            get {
                #if AUDIOLINK_V1
                if (!IsAudioLinked()) return null;
                #endif
                return audioLink;
            }
        }

        void UpdateVolume() {
            var volume = defaultMuted ? 0 : defaultVolume * defaultVolume; // Volume is not linear
            if (audioSources != null)
                for (int i = 0; i < audioSources.Length; i++) {
                    var audioSource = audioSources[i];
                    if (audioSource == null) continue;
                    audioSource.volume = volume;
                }
            SendEvent("_OnVolumeChange");
            UpdateAudioLinkVolume();
        }

        void UpdateAudioLinkVolume() {
            #if AUDIOLINK_V1
            if (IsAudioLinked()) audioLink.SetMediaVolume(defaultVolume);
            #endif
        }

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

        public float Time => activeHandler != null ? activeHandler.Time : 0;

        public float Duration => activeHandler != null ? activeHandler.Duration : 0;

        public Texture VideoTexture => activeHandler != null ? activeHandler.Texture : null;

        public VideoError LastError => lastError;

        public bool IsStatic => activeHandler != null && activeHandler.IsStatic;

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

        string Title {
            get => title;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                title = value;
            }
        }

        string Author {
            get => author;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                author = value;
            }
        }

        string ViewCount {
            get => viewCount;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                viewCount = value;
            }
        }

        string Description {
            get => description;
            set {
                if (hasCustomTitle || !url.Equals(localUrl)) return;
                description = value;
            }
        }

        void OnEnable() {
            if (broadcastScreenTexture) {
                if (broadcastTextureId == 0) broadcastTextureId = VRCShader.PropertyToID(broadcastScreenTextureName);
                var videoTexture = VideoTexture;
                if (videoTexture != null) VRCShader.SetGlobalTexture(broadcastTextureId, videoTexture);
            }
            #if AUDIOLINK_V1
            SendCustomEventDelayedFrames(nameof(_RestoreAudioLinkState), 0);
            #endif
            if (afterFirstRun) return;
            url = VRCUrl.Empty;
            foreach (var handler in playerHandlers)
                handler.core = this;
            if (screenTargetPropertyNames != null) {
                screenTargetPropertyIds = new int[screenTargetPropertyNames.Length];
                for (int i = 0; i < screenTargetPropertyNames.Length; i++) {
                    var propertyName = screenTargetPropertyNames[i];
                    if (!string.IsNullOrEmpty(propertyName) && propertyName != "_")
                        screenTargetPropertyIds[i] = VRCShader.PropertyToID(propertyName);
                }
            }
            if (avProPropertyNames != null) {
                avProPropertyIds = new int[avProPropertyNames.Length];
                for (int i = 0; i < avProPropertyNames.Length; i++) {
                    if ((screenTargetModes[i] & 0x8) != 0)
                        avProPropertyIds[i] = VRCShader.PropertyToID(screenTargetPropertyNames[i] + "_ST");
                    else {
                        var propertyName = avProPropertyNames[i];
                        if (!string.IsNullOrEmpty(propertyName) && propertyName != "_")
                            avProPropertyIds[i] = VRCShader.PropertyToID(propertyName);
                    }
                }
            }
            UpdateVolume();
            if (!synced || Networking.IsOwner(gameObject)) SendCustomEventDelayedSeconds(nameof(_PlayDefaultUrl), autoPlayDelay);
            else if (synced) SendCustomEventDelayedSeconds(nameof(_RequestOwnerSync), autoPlayDelay + 3);
            afterFirstRun = true;
        }

        void OnDisable() {
            if (broadcastScreenTexture) VRCShader.SetGlobalTexture(broadcastTextureId, null);
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

        void AssignAudioLinkSource() {
            assignedAudioSource = activeHandler.PrimaryAudioSource;
            if (audioLink != null) {
                if (assignedAudioSource != null)
                #if AUDIOLINK_V1
                    audioLink.audioSource = assignedAudioSource;
                float duration = activeHandler.Duration;
                SetAudioLinkPlayBackState(duration <= 0 || float.IsInfinity(duration) ? MediaPlaying.Streaming : MediaPlaying.Playing);
                UpdateAudioLinkVolume();
                #else
                    audioLink.SetProgramVariable("audioSource", assignedAudioSource);
                #endif
            }
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

        public void _OnTextureChanged() {
            var videoTexture = VideoTexture;
            var hasVideoTexture = videoTexture != null;
            var isAvPro = hasVideoTexture && IsAVPro;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                Texture texture = null;
                if (hasVideoTexture)
                    texture = videoTexture;
                else if (screenTargetDefaultTextures != null && i < screenTargetDefaultTextures.Length)
                    texture = screenTargetDefaultTextures[i];
                if (texture == null) texture = defaultTexture;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        SetTextureToMaterial(texture, (Material)screenTargets[i], i, isAvPro);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) renderer.GetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        if (screenTargetPropertyIds[i] != 0)
                            screenTargetPropertyBlock.SetTexture(screenTargetPropertyIds[i], texture);
                        if (avProPropertyIds[i] != 0) {
                            if ((screenTargetModes[i] & 0x8) != 0)
                            #if UNITY_STANDALONE_WIN
                                screenTargetPropertyBlock.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
                            #else
                                {} // Do nothing
                            #endif
                            else
                                screenTargetPropertyBlock.SetInt(avProPropertyIds[i], isAvPro ? 1 : 0);
                        }
                        if (index < 0) renderer.SetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.SetPropertyBlock(screenTargetPropertyBlock, index);
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        SetTextureToMaterial(texture, material, i, isAvPro);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        SetTextureToMaterial(texture, material, i, isAvPro);
                        break;
                    }
                    case 4: { // UI RawImage
                        var rawImage = (RawImage)screenTargets[i];
                        rawImage.texture = texture;
                        #if UNITY_STANDALONE_WIN
                        rawImage.uvRect = isAvPro ? flippedRect : normalRect;
                        #endif
                        break;
                    }
                }
            }
            if (broadcastScreenTexture) VRCShader.SetGlobalTexture(broadcastTextureId, videoTexture);
            UpdateRealtimeGI();
            SendEvent("_OnTextureChanged");
        }

        public float GetScreenFloatExtra(int id) {
            if (screenSharedProperties != null && screenSharedProperties.TryGetValue(id, TokenType.Float, out var value))
                return value.Float;
            float v = 0;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        v = ((Material)screenTargets[i]).GetFloat(id);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) {
                            renderer.GetPropertyBlock(screenTargetPropertyBlock);
                            if (screenTargetPropertyBlock.HasFloat(id)) {
                                v = screenTargetPropertyBlock.GetFloat(id);
                                break;
                            }
                            foreach (var m in renderer.sharedMaterials)
                                if (m.HasProperty(id)) {
                                    v = m.GetFloat(id);
                                    break;
                                }
                            break;
                        }
                        renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        if (screenTargetPropertyBlock.HasFloat(id)) {
                            v = screenTargetPropertyBlock.GetFloat(id);
                            break;
                        }
                        var material = renderer.sharedMaterials[index];
                        if (material.HasProperty(id)) {
                            v = material.GetFloat(id);
                            break;
                        }
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        v = material.GetFloat(id);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        v = material.GetFloat(id);
                        break;
                    }
                }
            }
            if (screenSharedProperties == null) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = v;
            return v;
        }

        public void SetScreenFloatExtra(int id, float value) {
            if (screenSharedProperties == null) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = value;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        ((Material)screenTargets[i]).SetFloat(id, value);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) renderer.GetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        screenTargetPropertyBlock.SetFloat(id, value);
                        if (index < 0) renderer.SetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.SetPropertyBlock(screenTargetPropertyBlock, index);
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        material.SetFloat(id, value);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        material.SetFloat(id, value);
                        break;
                    }
                }
            }
            SendEvent("_OnScreenSharedPropertiesChanged");
        }

        void SetTextureToMaterial(Texture texture, Material material, int i, bool isAvPro) {
            if (screenTargetPropertyIds[i] != 0)
                material.SetTexture(screenTargetPropertyIds[i], texture);
            if (avProPropertyIds[i] != 0) {
                if ((screenTargetModes[i] & 0x8) != 0)
                #if UNITY_STANDALONE_WIN
                    material.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
                #else
                    {} // Do nothing
                #endif
                else
                    material.SetInt(avProPropertyIds[i], isAvPro ? 1 : 0);
            }
        }

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
                        if (target != null) RendererExtensions.UpdateGIMaterials(target);
                        break;
                }
            if (!enabled || !gameObject.activeInHierarchy || activeHandler == null || !activeHandler.IsReady) {
                isRealtimeGIUpdaterRunning = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_UpdateRealtimeGI), realtimeGIUpdateInterval);
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

        void SyncSpeed() {
            if (activeHandler != null && activeHandler.SupportSpeedAdjustment)
                activeHandler.Speed = speed;
            SetAudioPitch();
            SendEvent("_OnSpeedChange");
        }

        void SetAudioPitch() {
            if (audioSources == null) return;
            var speed = activeHandler.Speed;
            for (int i = 0; i < audioSources.Length; i++) {
                var audioSource = audioSources[i];
                if (audioSource == null) continue;
                audioSource.pitch = speed;
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

        bool RequestSync() {
            if (!synced) return false;
            if (!Networking.IsOwner(gameObject)) Networking.SetOwner(Networking.LocalPlayer, gameObject);
            RequestSerialization();
            return true;
        }

        #if AUDIOLINK_V1
        bool IsAudioLinked() {
            if (audioLink == null) return false;
            var settedAudioSource = audioLink.audioSource;
            return settedAudioSource == null || settedAudioSource == assignedAudioSource;
        }

        void SetAudioLinkPlayBackState(MediaPlaying state) {
            if (IsAudioLinked()) {
                audioLink.autoSetMediaState = false;
                audioLink.SetMediaPlaying(state);
            }
            if (!isSyncAudioLink) {
                isSyncAudioLink = true;
                _SyncAudioLink();
            }
        }

        public void _SyncAudioLink() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || activeHandler == null || !activeHandler.IsReady || !IsAudioLinked()) {
                isSyncAudioLink = false;
                return;
            }
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                audioLink.SetMediaTime(0);
                isSyncAudioLink = false;
                return;
            }
            audioLink.SetMediaTime(activeHandler.Time / duration);
            SendCustomEventDelayedSeconds(nameof(_SyncAudioLink), 0.25F);
        }

        public void _RestoreAudioLinkState() {
            var state = MediaPlaying.Stopped;
            if (activeHandler != null) {
                if (activeHandler.IsPlaying) {
                    state = MediaPlaying.Playing;
                    AssignAudioLinkSource();
                } else if (activeHandler.IsPaused)
                    state = MediaPlaying.Paused;
            } else if (isLoading)
                state = MediaPlaying.Loading;
            else if (isError)
                state = MediaPlaying.Error;
            SetAudioLinkPlayBackState(state);
        }
        #endif

        public void Yttl_OnDataLoaded() => SendEvent("_OnTitleData");

        public void SetTitle(string title, string author) {
            hasCustomTitle = true;
            url = VRCUrl.Empty;
            this.title = title;
            this.author = author;
            description = "";
            viewCount = "";
            SendEvent("_OnTitleData");
        }

        public void _ResetTitle() {
            if (!hasCustomTitle) return;
            hasCustomTitle = false;
            url = VRCUrl.Empty;
            if (yttl == null) {
                author = "";
                title = "";
                viewCount = "";
                description = "";
            } else
                LoadYTTL();
            SendEvent("_OnTitleData");
        }

        void LoadYTTL() {
            if (yttl == null || hasCustomTitle || url.Equals(localUrl)) return;
            author = "";
            title = "";
            viewCount = "";
            description = "";
            if (localUrl != null)
                yttl.LoadData(localUrl, this);
        }
    }
}
