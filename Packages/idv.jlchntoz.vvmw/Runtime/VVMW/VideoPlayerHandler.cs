using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.Udon.Common.Enums;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEngine.Rendering;
using UnityEditor;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
#endif

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(BaseVRCVideoPlayer), typeof(Renderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Video Player Handler")]
    [DefaultExecutionOrder(0)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#builtin-module--avpro-module")]
    public class VideoPlayerHandler : AbstractMediaPlayerHandler {
        string[] realTimeProtocols = new string[] { "rtsp", "rtmp", "rtspt", "rtspu", "rtmps", "rtsps" };
        [SerializeField, LocalizedLabel] string texturePropertyName = "_MainTex";
        [SerializeField, LocalizedLabel] string speedParameterName = "Speed";
        [SerializeField, LocalizedLabel] bool useSharedMaterial = true;
        [SerializeField, LocalizedLabel] AudioSource primaryAudioSource;
        [SerializeField, LocalizedLabel] bool useFlickerWorkaround = true;
        [SerializeField] bool isAvPro;
        [SerializeField, LocalizedLabel] Material blitMaterial;
        [SerializeField, LocalizedLabel] bool isLowLatency;
        [SerializeField, HideInInspector] RateLimitResolver rateLimitResolver;
        Animator animator;
        RenderTexture bufferedTexture;
        bool isWaitingForTexture, isFlickerWorkaroundTextureRunning, isLoadUrlRequested;
        BaseVRCVideoPlayer videoPlayer;
        new Renderer renderer;
        MaterialPropertyBlock propertyBlock;
        int texturePropertyID, speedParameterID;
        bool isRealTimeProtocol;
        bool afterFirstRun;
        float playbackSpeed = 1, actualPlaybackSpeed = 1;
        VRCUrl loadedUrl;

        public override bool IsActive {
            get => isActive;
            set {
                isActive = value;
                if (!isActive && videoPlayer.IsPlaying) videoPlayer.Stop();
            }
        }

        public override float Time {
            get => videoPlayer.GetTime();
            set {
                videoPlayer.SetTime(value);
                // If it is paused, we need to blit the screen manually.
                if (isPaused) BlitBufferScreen();
            }
        }

        public override bool IsAvPro => isAvPro && !Utilities.IsValid(bufferedTexture);

        public override float Duration => isRealTimeProtocol ? float.PositiveInfinity : videoPlayer.GetDuration();

        public override Texture Texture => Utilities.IsValid(texture) && Utilities.IsValid(bufferedTexture) ? bufferedTexture : texture;

        public override AudioSource PrimaryAudioSource => primaryAudioSource;

        public override bool Loop {
            get => videoPlayer.Loop;
            set => videoPlayer.Loop = value;
        }

        public override bool IsReady => isReady && videoPlayer.IsReady;

        public override bool IsPlaying => videoPlayer.IsPlaying;

        public override bool IsPaused => isPaused;

        public override bool SupportSpeedAdjustment => animator && !isRealTimeProtocol;

        public override float Speed {
            get => SupportSpeedAdjustment ? actualPlaybackSpeed : 1;
            set {
                if (!animator || Mathf.Approximately(playbackSpeed, value)) return;
                playbackSpeed = value;
                SetPlaybackSpeed();
                if (isActive && isAvPro && !isRealTimeProtocol &&
                    !VRCUrl.IsNullOrEmpty(currentUrl))
                    core._RequestReloadUrl();
            }
        }

        void OnEnable() {
            if (afterFirstRun) return;
            afterFirstRun = true;
            animator = GetComponent<Animator>();
            videoPlayer = (BaseVRCVideoPlayer)GetComponent(typeof(BaseVRCVideoPlayer));
            renderer = (Renderer)GetComponent(typeof(Renderer));
            texturePropertyID = VRCShader.PropertyToID(texturePropertyName);
            if (animator) {
                speedParameterID = Animator.StringToHash(speedParameterName);
                animator.Rebind();
            }
            // This will actually instantiate a material clone,
            // and then the video screen output texture will be assigned to the clone instead of the original material.
            if (useSharedMaterial)
                renderer.sharedMaterial = renderer.material;
        }

        public void _GetTexture() {
            if (!isActive || !isWaitingForTexture || !videoPlayer.IsPlaying) {
                isWaitingForTexture = false;
                ClearTexture();
                return;
            }
            if (useSharedMaterial)
                texture = renderer.sharedMaterial.GetTexture(texturePropertyID);
            else {
                if (!Utilities.IsValid(propertyBlock)) propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                texture = propertyBlock.GetTexture(texturePropertyID);
            }
            if (Utilities.IsValid(texture)) {
                isWaitingForTexture = false;
                BlitBufferScreen();
                core._OnTextureChanged();
            } else
                SendCustomEventDelayedSeconds(nameof(_GetTexture), 0.2F);
        }

        void BlitBufferScreen() {
            if (!isAvPro || !useFlickerWorkaround || isFlickerWorkaroundTextureRunning ||
                !Utilities.IsValid(blitMaterial) || !Utilities.IsValid(texture) || !videoPlayer.IsPlaying)
                return;
            isFlickerWorkaroundTextureRunning = true;
            SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
            if (Utilities.IsValid(bufferedTexture) && texture.width == bufferedTexture.width && texture.height == bufferedTexture.height)
                return;
            Debug.Log($"[VVMW] Released temporary render texture for {playerName}.");
            VRCRenderTexture.ReleaseTemporary(bufferedTexture);
            bufferedTexture = null;
        }

        public void _BlitBufferScreen() {
            if (!isActive || !videoPlayer.IsPlaying || !Utilities.IsValid(texture)) {
                isFlickerWorkaroundTextureRunning = false;
                return;
            }
            if (isPaused) // Special case: we render 1 more frame when paused.
                isFlickerWorkaroundTextureRunning = false;
            else
                SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
            if (!Utilities.IsValid(bufferedTexture)) {
                int width = texture.width, height = texture.height;
                Debug.Log($"[VVMW] Created temporary render texture for {playerName}: {width}x{height}");
                bufferedTexture = VRCRenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.sRGB, 1);
                bufferedTexture.filterMode = FilterMode.Bilinear;
                bufferedTexture.wrapMode = TextureWrapMode.Clamp;
                core._OnTextureChanged();
            }
            VRCGraphics.Blit(texture, bufferedTexture, blitMaterial);
        }

        public override void LoadUrl(VRCUrl url, bool reload) {
            if (videoPlayer.IsPlaying) videoPlayer.Stop();
            if (!isActive) return;
            bool skip = !reload &&
                !VRCUrl.IsNullOrEmpty(currentUrl) &&
                currentUrl.Equals(url) &&
                IsReady &&
                videoPlayer.GetDuration() != 0 &&
                !float.IsInfinity(videoPlayer.GetDuration());
            currentUrl = url;
            isRealTimeProtocol = IsRealTimeProtocol(url);
            isPaused = false;
            if (skip) {
                videoPlayer.SetTime(0);
                SendCustomEventDelayedFrames("_onVideoReady", 0);
            } else {
                ClearTexture();
                LoadUrl();
            }
        }

        void LoadUrl() {
            isReady = false;
            SetPlaybackSpeed();
            if (!isLoadUrlRequested) {
                float delay = Utilities.IsValid(rateLimitResolver) ? rateLimitResolver._GetSafeLoadUrlDelay() : 0;
                if (delay > 0) {
                    isLoadUrlRequested = true;
                    SendCustomEventDelayedSeconds(nameof(_DoLoadUrl), delay);
                } else {
                    loadedUrl = currentUrl;
                    videoPlayer.LoadURL(currentUrl);
                }
            }
        }

        public void _DoLoadUrl() {
            isLoadUrlRequested = false;
            if (!isActive || VRCUrl.IsNullOrEmpty(currentUrl))
                return;
            loadedUrl = currentUrl;
            videoPlayer.LoadURL(currentUrl);
        }

        public override void Play() {
            if (!isActive) return;
            videoPlayer.Play();
            OnVideoPlay();
        }

        public override void Pause() {
            if (!isActive) return;
            videoPlayer.Pause();
            OnVideoPause();
        }

        public override void Stop() {
            if (!isActive) return;
            videoPlayer.Stop();
            if (isRealTimeProtocol) {
                isRealTimeProtocol = false;
                loadedUrl = currentUrl = VRCUrl.Empty;
            }
            OnVideoEnd();
        }

        public override void OnVideoError(VideoError videoError) {
            if (!isActive) return;
            isReady = false;
            core.OnVideoError(videoError);
        }

        public override void OnVideoReady() {
            actualPlaybackSpeed = playbackSpeed;
            if (VRCUrl.IsNullOrEmpty(currentUrl) || !currentUrl.Equals(loadedUrl)) return;
            UpdatePrimaryAudioSourcePitch();
            isPaused = false;
            isReady = true;
            if (!isActive) return;
            if (!isRealTimeProtocol) {
                float duration = videoPlayer.GetDuration();
                if (duration <= 0 || float.IsInfinity(duration)) {
                    isRealTimeProtocol = true;
                    if (isAvPro && playbackSpeed != 1) {
                        LoadUrl();
                        return;
                    }
                }
            }
            core.OnVideoReady();
        }

        public override void OnVideoStart() {
            isPaused = false;
            if (isActive) core.OnVideoStart();
            if (!isWaitingForTexture) {
                isWaitingForTexture = true;
                _GetTexture();
            }
        }

        public void _onVideoPlay() => OnVideoPlay();

        public override void OnVideoPlay() {
            isPaused = false;
            if (!isActive) return;
            BlitBufferScreen();
            core.OnVideoPlay();
        }

        public void _onVideoPause() => OnVideoPause();

        public override void OnVideoPause() {
            isPaused = true;
            if (!isActive) return;
            core.OnVideoPause();
        }

        public override void OnVideoEnd() {
            // Here are quirky behaviors of AVPro video player for firing OnVideoEnd event:
            // 1. If it's real-time protocol,
            //    it doesn't mean the stream is ended,
            //    so we have to ignore this event.
            if (isAvPro && isRealTimeProtocol) return;
            // 2. For video has measurable duration,
            //    its IsPlaying flag doesn't change to false even it reaches the end,
            //    so we have to stop it manually.
            if (videoPlayer.IsPlaying) videoPlayer.Stop();
            isPaused = false;
            if (!isActive) return;
            ClearTexture();
            core.OnVideoEnd();
        }

        public override void OnVideoLoop() {
            isPaused = false;
            if (!isActive) return;
            core.OnVideoLoop();
        }

        bool IsRealTimeProtocol(VRCUrl url) =>
            TryGetUrl(url, out var urlStr) && IsRealTimeProtocolS(urlStr);

        bool IsRealTimeProtocolS(string urlStr) {
            if (string.IsNullOrEmpty(urlStr) || urlStr.Length < 7) return false;
            int index = urlStr.IndexOf("://");
            if (index < 0 || index > 5) return false;
            var protocol = urlStr.Substring(0, index).ToLower();
            return Array.IndexOf(realTimeProtocols, protocol) >= 0;
        }

        void SetPlaybackSpeed() {
            if (!Utilities.IsValid(animator)) return;
            animator.SetFloat(speedParameterID, isRealTimeProtocol ? 1 : playbackSpeed);
            animator.Update(0);
            if (!isAvPro) {
                actualPlaybackSpeed = playbackSpeed;
                UpdatePrimaryAudioSourcePitch();
            }
        }

        void UpdatePrimaryAudioSourcePitch() {
            if (Utilities.IsValid(primaryAudioSource)) primaryAudioSource.pitch = actualPlaybackSpeed;
        }

        void ClearTexture() {
            texture = null;
            if (Utilities.IsValid(bufferedTexture)) {
                Debug.Log($"[VVMW] Released temporary render texture for {playerName}.");
                VRCRenderTexture.ReleaseTemporary(bufferedTexture);
                bufferedTexture = null;
            }
            if (isActive) core._OnTextureChanged();
        }

        public override int IsSupported(string urlStr) {
            if (!urlStr.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                return isAvPro && IsRealTimeProtocolS(urlStr) ? isLowLatency ? 2 : 1 : -1;
            int index = urlStr.IndexOf("://");
            if (index < 0) return -1;
            urlStr = urlStr.Substring(index + 3);
            index = urlStr.IndexOf('/');
            var domain = index < 0 ? urlStr : urlStr.Substring(0, index);
            index = urlStr.IndexOf('#');
            if (index < 0) {
                index = urlStr.IndexOf('?');
                if (index < 0) index = urlStr.Length;
            }
            int domainLength = domain.Length;
            if (domain.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase)) {
                if (urlStr.IndexOf("/live", domainLength, index - domainLength, StringComparison.OrdinalIgnoreCase) >= 0)
                    return isAvPro ? isLowLatency ? 2 : 1 : -1;
                return 1;
            }
            if (domain.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase))
                return 0;
            if (domain.EndsWith("twitch.tv", StringComparison.OrdinalIgnoreCase))
                return isAvPro ? 1 : 0;
            if (domain.EndsWith("soundcloud.com", StringComparison.OrdinalIgnoreCase))
                return isAvPro ? 1 : -1;
            index = urlStr.LastIndexOf('.', index - 1);
            if (index < 0) return 0;
            switch (urlStr.Substring(index + 1).ToLower()) {
                case "mp4":
                case "mkv":
                case "webm":
                case "aac":
                case "opus":
                case "mov":
                case "avi":
                case "wav":
                case "wma":
                case "wmv":
                case "ogg":
                    return 1;
                case "fla":
                case "flac":
                case "ac3":
                case "mp3":
                case "m3u8":
                case "mpd":
                case "ism":
                    if (isAvPro) return 1; break;
            }
            return 0;
        }

        #if UNITY_EDITOR && !COMPILER_UDONSHARP
        protected override void PreProcess() {
            TrustedUrlTypes urlType = default;
            if (!TryGetComponent(out BaseVRCVideoPlayer videoPlayer)) return;
            if (!TryGetComponent(out Renderer renderer)) renderer = gameObject.AddComponent<MeshRenderer>();
            renderer.enabled = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.allowOcclusionWhenDynamic = false;
            using (var videoPlayerSo = new SerializedObject(videoPlayer)) {
                videoPlayerSo.FindProperty("autoPlay").boolValue = false;
                videoPlayerSo.FindProperty("loop").boolValue = false;
                if (videoPlayer is VRCAVProVideoPlayer) {
                    urlType = TrustedUrlTypes.AVProDesktop;
                    isLowLatency = videoPlayerSo.FindProperty("useLowLatency").boolValue;
                    if (!videoPlayer.TryGetComponent(out VRCAVProVideoScreen screen))
                        screen = gameObject.AddComponent<VRCAVProVideoScreen>();
                    using (var screenSo = new SerializedObject(screen)) {
                        screenSo.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
                        screenSo.FindProperty("materialIndex").intValue = 0;
                        screenSo.FindProperty("textureProperty").stringValue = texturePropertyName;
                        screenSo.FindProperty("useSharedMaterial").boolValue = false;
                        screenSo.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (Utilities.IsValid(primaryAudioSource)) {
                        if (!primaryAudioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker))
                            speaker = primaryAudioSource.gameObject.AddComponent<VRCAVProVideoSpeaker>();
                        using (var speakerSo = new SerializedObject(speaker)) {
                            speakerSo.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
                            speakerSo.ApplyModifiedPropertiesWithoutUndo();
                        }
                    }
                } else if (videoPlayer is VRCUnityVideoPlayer) {
                    urlType = TrustedUrlTypes.UnityVideo;
                    videoPlayerSo.FindProperty("renderMode").intValue = 1;
                    videoPlayerSo.FindProperty("targetMaterialRenderer").objectReferenceValue = renderer;
                    videoPlayerSo.FindProperty("targetMaterialProperty").stringValue = texturePropertyName;
                    videoPlayerSo.FindProperty("aspectRatio").intValue = 0;
                    if (Utilities.IsValid(primaryAudioSource)) {
                        var targetAudioSources = videoPlayerSo.FindProperty("targetAudioSources");
                        targetAudioSources.arraySize = 1;
                        targetAudioSources.GetArrayElementAtIndex(0).objectReferenceValue = primaryAudioSource;
                    }
                }
                videoPlayerSo.ApplyModifiedPropertiesWithoutUndo();
            }
            isAvPro = urlType == TrustedUrlTypes.AVProDesktop;
            useSharedMaterial = isAvPro;
            if (Utilities.IsValid(applyTurstedUrl)) applyTurstedUrl(urlType, ref trustedUrlDomains);
        }
        #endif
    }
}