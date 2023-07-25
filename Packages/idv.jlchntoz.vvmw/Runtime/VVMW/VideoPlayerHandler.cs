using System;
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Components.Video;
using VRC.SDK3.Video.Components.Base;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(BaseVRCVideoPlayer))]
    [RequireComponent(typeof(Renderer))]
    public class VideoPlayerHandler : UdonSharpBehaviour {
        string[] rtspVaildProtocols = new string[] { "rtsp", "rtmp", "rtspt", "rtspu", "rtmps", "rtsps" };
        [NonSerialized] public Core core;
        [Tooltip("The name of current video player. Can be the key mapped in language pack JSON.")]
        public string playerName = "";
        [SerializeField] string texturePropertyName = "_MainTex";
        [SerializeField] bool useSharedMaterial = true;
        [SerializeField] AudioSource primaryAudioSource;
        public bool isAvPro;
        bool isActive, isReady, isPaused;
        bool isWaitingForTexture;
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

        public float Duration => isRTSP ? float.PositiveInfinity : videoPlayer.GetDuration();

        public Texture Texture => texture;

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
        }

        public void _GetTexture() {
            if (!isActive || !isWaitingForTexture || !videoPlayer.IsPlaying) {
                texture = null;
                isWaitingForTexture = false;
                return;
            }
            if (useSharedMaterial)
                texture = renderer.material.GetTexture(texturePropertyID);
            else {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                renderer.GetPropertyBlock(propertyBlock);
                texture = propertyBlock.GetTexture(texturePropertyID);
            }
            if (texture != null) {
                isWaitingForTexture = false;
                core._OnTextureChanged();
            } else
                SendCustomEventDelayedSeconds(nameof(_GetTexture), 0.2F);
        }

        public void LoadUrl(VRCUrl url, bool reload) {
            if (videoPlayer.IsPlaying) videoPlayer.Stop();
            if (!isActive) return;
            if (!reload && Utilities.IsValid(lastUrl) && lastUrl.Equals(url) && !isRTSP && IsReady)
                SendCustomEventDelayedFrames("_onVideoReady", 0);
            else {
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
            isPaused = false;
            if (!isActive) return;
            texture = null;
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
    }
}