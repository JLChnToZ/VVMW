using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;
using VRC.Udon.Common.Enums;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(BaseVRCVideoPlayer))]
    [RequireComponent(typeof(Renderer))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Video Player Handler")]
    [DefaultExecutionOrder(0)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#builtin-module--avpro-module")]
    public class VideoPlayerHandler : UdonSharpBehaviour {
        string[] rtspVaildProtocols = new string[] { "rtsp", "rtmp", "rtspt", "rtspu", "rtmps", "rtsps" };
        [NonSerialized] public Core core;
        [Tooltip("The name of current video player. Can be the key mapped in language pack JSON.")]
        public string playerName = "";
        [SerializeField] string texturePropertyName = "_MainTex";
        [SerializeField] bool useSharedMaterial = true;
        [SerializeField] AudioSource primaryAudioSource;
        [Tooltip("This option is only for AVPro video player.\nIt will use a workaround with a little performance cost to attempt to fix the screen flickering issue.")]
        [SerializeField] bool useFlickerWorkaround = true;
        [SerializeField] bool isAvPro;
        [Tooltip("This material will be used to blit the screen to a temporary render texture for the flickering workaround. Don't change it unless needed.")]
        [SerializeField] Material blitMaterial;
        RenderTexture bufferedTexture;
        bool isActive, isReady, isPaused;
        bool isWaitingForTexture, isFlickerWorkaroundTextureRunning;
        BaseVRCVideoPlayer videoPlayer;
        new Renderer renderer;
        Texture texture;
        MaterialPropertyBlock propertyBlock;
        int texturePropertyID;
        VRCUrl lastUrl;
        bool isRTSP;

        public bool IsActive {
            get => isActive;
            set {
                isActive = value;
                if (!isActive && videoPlayer.IsPlaying) videoPlayer.Stop();
            }
        }

        public float Time {
            get => videoPlayer.GetTime();
            set => videoPlayer.SetTime(value);
        }

        public bool IsAvPro => isAvPro && bufferedTexture == null;

        public float Duration => isRTSP ? float.PositiveInfinity : videoPlayer.GetDuration();

        public Texture Texture => bufferedTexture != null ? bufferedTexture : texture;

        public AudioSource PrimaryAudioSource => primaryAudioSource;

        public bool Loop {
            get => videoPlayer.Loop;
            set => videoPlayer.Loop = value;
        }

        public bool IsReady => isReady && videoPlayer.IsReady;

        public bool IsPlaying => videoPlayer.IsPlaying;

        public bool IsPaused => isPaused;

        void Start() {
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
                if (isAvPro && useFlickerWorkaround && !isFlickerWorkaroundTextureRunning && blitMaterial != null) {
                    isFlickerWorkaroundTextureRunning = true;
                    SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
                } else
                    core._OnTextureChanged();
            } else
                SendCustomEventDelayedSeconds(nameof(_GetTexture), 0.2F);
        }

        // Experimental workaround for AVPro screen flickering issue.
        public void _BlitBufferScreen() {
            if (!isActive || !videoPlayer.IsPlaying || texture == null || !enabled) {
                isFlickerWorkaroundTextureRunning = false;
                return;
            }
            SendCustomEventDelayedFrames(nameof(_BlitBufferScreen), 0, EventTiming.LateUpdate);
            int width = texture.width, height = texture.height;
            if (bufferedTexture != null && (bufferedTexture.width != width || bufferedTexture.height != height)) {
                VRCRenderTexture.ReleaseTemporary(bufferedTexture);
                bufferedTexture = null;
            }
            if (bufferedTexture == null) {
                Debug.Log($"[VVMW] Created temporary render texture for {playerName}: {width}x{height}");
                bufferedTexture = VRCRenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB64, RenderTextureReadWrite.sRGB, 1);
                bufferedTexture.filterMode = FilterMode.Bilinear;
                bufferedTexture.wrapMode = TextureWrapMode.Clamp;
                core._OnTextureChanged();
            }
            VRCGraphics.Blit(texture, bufferedTexture, blitMaterial);
        }

        public void LoadUrl(VRCUrl url, bool reload) {
            if (videoPlayer.IsPlaying) videoPlayer.Stop();
            if (!isActive) return;
            if (!reload && Utilities.IsValid(lastUrl) && lastUrl.Equals(url) && IsReady && videoPlayer.GetDuration() != 0 && !float.IsInfinity(videoPlayer.GetDuration())) {
                videoPlayer.SetTime(0);
                SendCustomEventDelayedFrames("_onVideoReady", 0);
            } else {
                isReady = false;
                videoPlayer.LoadURL(url);
            }
            lastUrl = url;
            isRTSP = IsRTSP(url);
            isPaused = false;
        }

        public void Play() {
            if (!isActive) return;
            videoPlayer.Play();
            OnVideoPlay();
        }

        public void Pause() {
            if (!isActive) return;
            videoPlayer.Pause();
            OnVideoPause();
        }

        public void Stop() {
            if (!isActive) return;
            videoPlayer.Stop();
            if (isRTSP) {
                isRTSP = false;
                lastUrl = VRCUrl.Empty;
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
            core.OnVideoPlay();
        }

        public void _onVideoPause() => OnVideoPause();

        public override void OnVideoPause() {
            isPaused = true;
            if (!isActive) return;
            core.OnVideoPause();
        }

        public override void OnVideoEnd() {
            if (isRTSP) return; // Don't do anything if it's RTSP
            // For AVPro players, even it fires OnVideoEnd event,
            // its IsPlaying flag doesn't change to false,
            // so we have to stop it manually.
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

        bool IsRTSP(VRCUrl url) {
            if (!Utilities.IsValid(url)) return false;
            var urlStr = url.Get();
            if (string.IsNullOrEmpty(urlStr) || urlStr.Length < 7) return false;
            int index = urlStr.IndexOf("://");
            if (index < 0 || index > 5) return false;
            var protocol = urlStr.Substring(0, index).ToLower();
            return Array.IndexOf(rtspVaildProtocols, protocol) >= 0;
        }

        void ClearTexture() {
            texture = null;
            if (bufferedTexture != null) {
                Debug.Log($"[VVMW] Released temporary render texture for {playerName}.");
                VRCRenderTexture.ReleaseTemporary(bufferedTexture);
                bufferedTexture = null;
            }
        }
    }
}