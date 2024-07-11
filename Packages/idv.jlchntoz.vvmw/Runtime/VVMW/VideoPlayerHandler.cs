using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.Udon.Common.Enums;
using VRC.SDK3.Video.Components.AVPro;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(BaseVRCVideoPlayer))]
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Video Player Handler")]
    [DefaultExecutionOrder(0)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#builtin-module--avpro-module")]
    public class VideoPlayerHandler : AbstractMediaPlayerHandler {
        string[] realTimeProtocols = new string[] { "rtsp", "rtmp", "rtspt", "rtspu", "rtmps", "rtsps" };
        [SerializeField] string texturePropertyName = "_MainTex";
        [SerializeField] bool useSharedMaterial = true;
        [SerializeField] AudioSource primaryAudioSource;
        [Tooltip("This option is only for AVPro video player.\nIt will use a workaround with a little performance cost to attempt to fix the screen flickering issue.")]
        [SerializeField] bool useFlickerWorkaround = true;
        [SerializeField] bool isAvPro;
        [Tooltip("This material will be used to blit the screen to a temporary render texture for the flickering workaround. Don't change it unless needed.")]
        [SerializeField] Material blitMaterial;
        [SerializeField, HideInInspector] bool isLowLatency;
        RenderTexture bufferedTexture;
        bool isWaitingForTexture, isFlickerWorkaroundTextureRunning;
        BaseVRCVideoPlayer videoPlayer;
        new Renderer renderer;
        MaterialPropertyBlock propertyBlock;
        int texturePropertyID;
        bool isRealTimeProtocol;
        bool afterFirstRun;

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

        public override bool IsAvPro => isAvPro && bufferedTexture == null;

        public override float Duration => isRealTimeProtocol ? float.PositiveInfinity : videoPlayer.GetDuration();

        public override Texture Texture => texture != null && bufferedTexture != null ? bufferedTexture : texture;

        public override AudioSource PrimaryAudioSource => primaryAudioSource;

        public override bool Loop {
            get => videoPlayer.Loop;
            set => videoPlayer.Loop = value;
        }

        public override bool IsReady => isReady && videoPlayer.IsReady;

        public override bool IsPlaying => videoPlayer.IsPlaying;

        public override bool IsPaused => isPaused;

        void OnEnable() {
            if (afterFirstRun) return;
            afterFirstRun = true;
            videoPlayer = (BaseVRCVideoPlayer)GetComponent(typeof(BaseVRCVideoPlayer));
            renderer = (Renderer)GetComponent(typeof(Renderer));
            texturePropertyID = VRCShader.PropertyToID(texturePropertyName);
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
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                texture = propertyBlock.GetTexture(texturePropertyID);
            }
            if (texture != null) {
                isWaitingForTexture = false;
                BlitBufferScreen();
                core._OnTextureChanged();
            } else
                SendCustomEventDelayedSeconds(nameof(_GetTexture), 0.2F);
        }

        void BlitBufferScreen() {
            if (!isAvPro || !useFlickerWorkaround || isFlickerWorkaroundTextureRunning ||
                blitMaterial == null || texture == null || !videoPlayer.IsPlaying)
                return;
            isFlickerWorkaroundTextureRunning = true;
            SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
            if (bufferedTexture != null && texture.width == bufferedTexture.width && texture.height == bufferedTexture.height)
                return;
            Debug.Log($"[VVMW] Released temporary render texture for {playerName}.");
            VRCRenderTexture.ReleaseTemporary(bufferedTexture);
            bufferedTexture = null;
        }

        public void _BlitBufferScreen() {
            if (!isActive || !videoPlayer.IsPlaying || texture == null) {
                isFlickerWorkaroundTextureRunning = false;
                return;
            }
            if (isPaused) // Special case: we render 1 more frame when paused.
                isFlickerWorkaroundTextureRunning = false;
            else
                SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
            if (bufferedTexture == null) {
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
            if (!reload && Utilities.IsValid(currentUrl) && currentUrl.Equals(url) && IsReady && videoPlayer.GetDuration() != 0 && !float.IsInfinity(videoPlayer.GetDuration())) {
                videoPlayer.SetTime(0);
                SendCustomEventDelayedFrames("_onVideoReady", 0);
            } else {
                isReady = false;
                videoPlayer.LoadURL(url);
                ClearTexture();
            }
            currentUrl = url;
            isRealTimeProtocol = IsRealTimeProtocol(url);
            isPaused = false;
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
                currentUrl = VRCUrl.Empty;
            }
            OnVideoEnd();
        }

        public override void OnVideoError(VideoError videoError) {
            if (!isActive) return;
            isReady = false;
            core.OnVideoError(videoError);
        }

        public override void OnVideoReady() {
            isPaused = false;
            isReady = true;
            if (isActive) core.OnVideoReady();
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
            if (isRealTimeProtocol) return;
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

        void ClearTexture() {
            texture = null;
            if (bufferedTexture != null) {
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
                #if !UNITY_ANDROID
                case "mov":
                case "avi":
                case "wav":
                case "wma":
                case "wmv":
                #else
                case "ogg":
                #endif
                    return 1;
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
    }
}