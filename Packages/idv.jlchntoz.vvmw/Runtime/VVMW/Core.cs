using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.Udon.Common.Interfaces;
using VVMW.ThirdParties.Yttl;

#if AUDIOLINK_V1
using AudioLink;
#endif

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Core")]
    public class Core : UdonSharpEventSender {
        const byte IDLE = 0, LOADING = 1, PLAYING = 2, PAUSED = 3;
        [HideInInspector, SerializeField] string[] trustedUrlDomains = new string[0]; // This list will be fetched on build, via VRChat SDK
        Vector4 normalST = new Vector4(1, 1, 0, 0), flippedST = new Vector4(1, -1, 0, 1);
        Rect normalRect = new Rect(0, 0, 1, 1), flippedRect = new Rect(0, 1, 1, -1);
        [SerializeField] VideoPlayerHandler[] playerHandlers;
        [Tooltip("Audio sources to link to video player, will be set to the primary audio source of the video player, and volumes can be controlled by the video player.")]
        [SerializeField] AudioSource[] audioSources;
        [SerializeField] VRCUrl defaultUrl;
        [Tooltip("The default url to use when playing on Quest. Leave empty to use the same url as PC.")]
        [SerializeField] VRCUrl defaultQuestUrl;
        [SerializeField, Range(0, 255)] int autoPlayPlayerType = 1;
        [SerializeField] bool synced = true;
        [SerializeField] int totalRetryCount = 3;
        [SerializeField, Range(5, 20)] float retryDelay = 5.5F;
        [SerializeField, Range(0, 1), FieldChangeCallback(nameof(Volume))]
        float defaultVolume = 1;
        [SerializeField, FieldChangeCallback(nameof(Muted))]
        bool defaultMuted = false;
        [Tooltip("The default texture to use when video is not ready or error occurred. It is required to have a texture when use with property block mode.")]
        [SerializeField] Texture defaultTexture;
        [SerializeField] UnityEngine.Object[] screenTargets;
        [SerializeField] int[] screenTargetModes;
        [SerializeField] int[] screenTargetIndeces;
        [SerializeField] string[] screenTargetPropertyNames, avProPropertyNames;
        [SerializeField] Texture[] screenTargetDefaultTextures;
        int[] screenTargetPropertyIds, avProPropertyIds;
        [FieldChangeCallback(nameof(SyncOffset))]
        float syncOffset = 0;
        [UdonSynced] VRCUrl pcUrl, questUrl;
        VRCUrl localUrl, loadingUrl, lastUrl, altUrl;
        // When playing, it is the time when the video started playing;
        // When paused, it is the progress of the video in ticks.
        [UdonSynced] long time;
        [UdonSynced] byte activePlayer;
        byte localActivePlayer, lastActivePlayer;
        // 0: Idle, 1: Loading, 2: Playing, 3: Paused
        [UdonSynced] byte state;
        [SerializeField, UdonSynced, FieldChangeCallback(nameof(Loop))]
        bool loop;
        [Locatable(
            "AudioLink.AudioLink, AudioLink", "VRCAudioLink.AudioLink, AudioLink",
            InstaniatePrefabPath = "Packages/com.llealloo.audiolink/Runtime/AudioLink.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        )]
        [SerializeField] UdonSharpBehaviour audioLink;
        [Tooltip("YTTL (Video title viewer) integration")]
        [SerializeField, Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/Third-Parties/YTTL/YTTL Manager.prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.First
        )] YttlManager yttl;
        VideoPlayerHandler activeHandler;
        int retryCount = 0;
        bool isLoading, isLocalReloading, isResyncTime, isError, isSyncAudioLink;
        VideoError lastError = VideoError.Unknown;
        string[] playerNames;
        bool trustUpdated, isTrusted;
        MaterialPropertyBlock screenTargetPropertyBlock;
        AudioSource assignedAudioSource;

        // Yttl Receivers
        [NonSerialized] public VRCUrl url;
        [NonSerialized, FieldChangeCallback(nameof(Author))]
        public string author = "";
        [NonSerialized, FieldChangeCallback(nameof(Title))]
        public string title = "";
        [NonSerialized, FieldChangeCallback(nameof(ViewCount))]
        public string viewCount = "";
        [NonSerialized, FieldChangeCallback(nameof(Description))]
        public string description = "";

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
                    } else
                        handler.IsActive = false;
                }
                if (value == 0 && wasPlaying && !isLocalReloading && !IsUrlValid(loadingUrl)) {
                    SendEvent("_onVideoEnd");
                    _OnTextureChanged();
                }
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
                return (AudioLink.AudioLink)audioLink;
                #else
                return audioLink;
                #endif
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
            if (IsAudioLinked()) ((AudioLink.AudioLink)audioLink).SetMediaVolume(defaultVolume);
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
                if (IsAudioLinked()) ((AudioLink.AudioLink)audioLink).SetMediaLoop(value ? MediaLoop.LoopOne : MediaLoop.None);
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

        public bool IsAVPro => activeHandler != null && activeHandler.isAvPro;

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

        public bool IsTrusted {
            get {
                if (!trustUpdated) {
                    isTrusted = ValidateTrusted();
                    trustUpdated = true;
                }
                return isTrusted;
            }
        }

        string Title {
            get => title;
            set {
                if (url == null || !url.Equals(localUrl)) return;
                title = value;
            }
        }

        string Author {
            get => author;
            set {
                if (url == null || !url.Equals(localUrl)) return;
                author = value;
            }
        }

        string ViewCount {
            get => viewCount;
            set {
                if (url == null || !url.Equals(localUrl)) return;
                viewCount = value;
            }
        }

        string Description {
            get => description;
            set {
                if (url == null || !url.Equals(localUrl)) return;
                description = value;
            }
        }

        bool ValidateTrusted() {
            if (!Utilities.IsValid(localUrl)) return false;
            var url = localUrl.Get();
            if (string.IsNullOrEmpty(url)) return false;
            int domainStartIndex = url.IndexOf("://");
            if (domainStartIndex < 0) return false;
            domainStartIndex += 3;
            int endIndex = url.IndexOf('/', domainStartIndex);
            if (endIndex < 0) return false;
            int startIndex = url.LastIndexOf('.', domainStartIndex, endIndex - domainStartIndex);
            if (startIndex < 0) return false;
            startIndex = url.LastIndexOf('.', domainStartIndex, startIndex - domainStartIndex);
            if (startIndex < 0) startIndex = domainStartIndex;
            return Array.IndexOf(trustedUrlDomains, url.Substring(startIndex + 1, endIndex - startIndex - 1)) >= 0;
        }

        void Start() {
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
            screenTargetPropertyBlock = new MaterialPropertyBlock();
            Volume = defaultVolume;
            if (!synced || Networking.IsOwner(gameObject)) SendCustomEventDelayedFrames(nameof(_PlayDefaultUrl), 0);
            // Print trusted URLs for debug
            foreach (var domain in trustedUrlDomains) Debug.Log(domain);
        }

        public void _PlayDefaultUrl() {
            if (IsUrlValid(defaultUrl)) PlayUrl(null, 0);
        }

        public void PlayUrl(VRCUrl url, byte playerType) => PlayUrlMP(url, null, playerType);

        public void PlayUrlMP(VRCUrl pcUrl, VRCUrl questUrl, byte playerType) {
            isError = false;
            VRCUrl url;
            #if UNITY_ANDROID
            url = questUrl;
            if (!IsUrlValid(url))
            #endif
                url = pcUrl;
            if (!IsUrlValid(url)) {
                if (IsUrlValid(defaultUrl)) {
                    pcUrl = defaultUrl;
                    questUrl = defaultQuestUrl;
                } else return;
                #if UNITY_ANDROID
                url = questUrl;
                if (!IsUrlValid(url))
                #endif
                    url = pcUrl;
                playerType = (byte)autoPlayPlayerType;
            }
            #if UNITY_ANDROID
            localUrl = questUrl;
            altUrl = pcUrl;
            #else
            localUrl = pcUrl;
            altUrl = questUrl;
            #endif
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
            if (yttl != null) {
                if (!url.Equals(this.url)) {
                    author = "";
                    title = "";
                    viewCount = "";
                    description = "";
                }
                yttl.LoadData(url, this);
            }
        }

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
            if (!IsUrlValid(loadingUrl) ||
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
                #if UNITY_ANDROID
                if (IsUrlValid(questUrl))
                    localUrl = questUrl;
                else
                #endif
                    localUrl = pcUrl;
            }
            else if (!IsUrlValid(localUrl)) localUrl = defaultUrl;
            loadingUrl = localUrl;
            trustUpdated = false;
            if (!IsUrlValid(loadingUrl)) return;
            retryCount = 0;
            _ReloadUrl();
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
            assignedAudioSource = activeHandler.PrimaryAudioSource;
            if (audioLink != null) {
                if (assignedAudioSource != null)
                    audioLink.SetProgramVariable("audioSource", assignedAudioSource);
                #if AUDIOLINK_V1
                float duration = activeHandler.Duration;
                SetAudioLinkPlayBackState(duration <= 0 || float.IsInfinity(duration) ? MediaPlaying.Streaming : MediaPlaying.Playing);
                UpdateAudioLinkVolume();
                #endif
            }
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
            if (IsUrlValid(loadingUrl) || isLocalReloading) return;
            lastActivePlayer = activePlayer;
            lastUrl = localUrl;
            ActivePlayer = 0;
            localUrl = synced ? null : defaultUrl;
            trustUpdated = false;
            SendEvent("_onVideoEnd");
            #if AUDIOLINK_V1
            if (audioLink != null) ((AudioLink.AudioLink)audioLink).SetMediaPlaying(MediaPlaying.Stopped);
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
            var isAvPro = IsAVPro;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                Texture texture = null;
                if (videoTexture != null)
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
                        if (index < 0) renderer.GetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        if (screenTargetPropertyIds[i] != 0)
                            screenTargetPropertyBlock.SetTexture(screenTargetPropertyIds[i], texture);
                        if (avProPropertyIds[i] != 0) {
                            if ((screenTargetModes[i] & 0x8) != 0)
                                screenTargetPropertyBlock.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
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
                        rawImage.uvRect = isAvPro ? flippedRect : normalRect;
                        break;
                    }
                }
            }
            SendEvent("_OnTextureChanged");
        }

        void SetTextureToMaterial(Texture texture, Material material, int i, bool isAvPro) {
            if (screenTargetPropertyIds[i] != 0)
                material.SetTexture(screenTargetPropertyIds[i], texture);
            if (avProPropertyIds[i] != 0) {
                if ((screenTargetModes[i] & 0x8) != 0)
                    material.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
                else
                    material.SetInt(avProPropertyIds[i], isAvPro ? 1 : 0);
            }
        }

        public override void OnPreSerialization() {
            if (!synced || isLocalReloading) return;
            if (activeHandler == null) {
                activePlayer = 0;
                state = IDLE;
                time = 0;
                pcUrl = null;
                questUrl = null;
            } else {
                activePlayer = localActivePlayer;
                #if UNITY_ANDROID
                if (IsUrlValid(altUrl)) {
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
                    time = CalcSyncTime();
                } else {
                    state = IsUrlValid(localUrl) ? LOADING : IDLE;
                    time = 0;
                }
            }
        }

        public override void OnDeserialization() {
            if (!synced) return;
            ActivePlayer = activePlayer;
            VRCUrl url = null;
            #if UNITY_ANDROID
            if (IsUrlValid(questUrl)) {
                url = questUrl;
                altUrl = pcUrl;
            } else
            #endif
            {
                url = pcUrl;
                altUrl = questUrl;
            }
            if (state != IDLE && localUrl != url && (!IsUrlValid(localUrl) || !IsUrlValid(url) || localUrl.Get() != url.Get())) {
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
                if (yttl != null) {
                    author = "";
                    title = "";
                    viewCount = "";
                    description = "";
                    yttl.LoadData(url, this);
                }
            }
            localUrl = url;
            if (activeHandler != null && activeHandler.IsReady) {
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
                            var duration = activeHandler.Duration;
                            if (duration > 0 && !float.IsInfinity(duration))
                                activeHandler.Time += UnityEngine.Time.realtimeSinceStartup - Networking.SimulationTime(Networking.GetOwner(gameObject));
                        }
                        break;
                }
                if (!isLocalReloading && !isLoading) SyncTime();
            }
        }

        public override void OnOwnershipTransferred(VRCPlayerApi player) {
            if (!player.isLocal) isLocalReloading = false;
        }

        void StartSyncTime() {
            #if AUDIOLINK_V1
            if (!isSyncAudioLink) {
                isSyncAudioLink = true;
                _SyncAudioLink();
            }
            #endif
            if (!synced) return;
            SyncTime();
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
                return;
            }
            SyncTime();
            SendCustomEventDelayedSeconds(nameof(_AutoSyncTime), 0.5F);
        }

        long CalcSyncTime() {
            if (activeHandler == null) return 0;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            var videoTime = Mathf.Repeat(activeHandler.Time, duration);
            var syncTime = (long)((videoTime - syncOffset) * TimeSpan.TicksPerSecond);
            if (activeHandler.IsPlaying) syncTime = Networking.GetNetworkDateTime().Ticks - syncTime;
            return syncTime;
        }

        float CalcVideoTime() {
            if (activeHandler == null) return 0;
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) return 0;
            float videoTime;
            int intState = state;
            switch (intState) {
                case PLAYING: videoTime = (float)(Networking.GetNetworkDateTime().Ticks - time) / TimeSpan.TicksPerSecond + syncOffset; break;
                case PAUSED: videoTime = (float)time / TimeSpan.TicksPerSecond; break;
                default: return 0;
            }
            if (loop) videoTime = Mathf.Repeat(videoTime, duration);
            return videoTime;
        }

        void SyncTime() {
            if (Networking.IsOwner(gameObject)) {
                var newTime = CalcSyncTime();
                if (Mathf.Abs((float)(newTime - time) / TimeSpan.TicksPerSecond) >= 0.1F) {
                    time = newTime;
                    RequestSerialization();
                }
            } else {
                var duration = activeHandler.Duration;
                if (duration <= 0 || float.IsInfinity(duration)) return;
                float t = CalcVideoTime();
                var t2 = activeHandler.Time;
                if (loop) t2 = Mathf.Repeat(t2, duration);
                if (Mathf.Abs(t2 - t) >= 0.1F) {
                    activeHandler.Time = t;
                    SendEvent("_OnTimeDrift");
                }
            }
        }

        bool IsUrlValid(VRCUrl url) => Utilities.IsValid(url) && !url.Equals(VRCUrl.Empty);

        public void _RequestOwnerSync() => SendCustomNetworkEvent(NetworkEventTarget.Owner, nameof(OwnerSync));

        public void OwnerSync() {
            if (!Networking.IsOwner(gameObject) || !synced) return;
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
            var settedAudioSource = ((AudioLink.AudioLink)audioLink).audioSource;
            return settedAudioSource == null || settedAudioSource == assignedAudioSource;
        }

        void SetAudioLinkPlayBackState(MediaPlaying state) {
            if (IsAudioLinked()) {
                ((AudioLink.AudioLink)audioLink).autoSetMediaState = false;
                ((AudioLink.AudioLink)audioLink).SetMediaPlaying(state);
            }
        }

        public void _SyncAudioLink() {
            if (!gameObject.activeInHierarchy || !enabled || isLoading || isLocalReloading || activeHandler == null || !activeHandler.IsReady || !IsAudioLinked()) {
                isSyncAudioLink = false;
                return;
            }
            var duration = activeHandler.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                ((AudioLink.AudioLink)audioLink).SetMediaTime(0);
                isSyncAudioLink = false;
                return;
            }
            ((AudioLink.AudioLink)audioLink).SetMediaTime(activeHandler.Time / duration);
            SendCustomEventDelayedFrames(nameof(_SyncAudioLink), 0);
        }
        #endif

        public void Yttl_OnDataLoaded() => SendEvent("_OnTitleData");

        public void SetTitle(string title, string author) {
            url = VRCUrl.Empty;
            this.title = title;
            this.author = author;
            description = "";
            viewCount = "";
            SendEvent("_OnTitleData");
        }
    }
}
