using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Image;
using VRC.Udon.Common.Interfaces;
using VRC.SDK3.Components.Video;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Image Viewer Handler")]
    [DefaultExecutionOrder(0)]
    public class ImageViewerHandler : AbstractMediaPlayerHandler {
        VRCImageDownloader loader;
        VRCUrl currentUrl;
        bool loop;

        public override bool IsActive {
            get => isActive;
            set {
                isActive = value;
                texture = null;
                currentUrl = null;
            }
        }

        public override bool Loop { 
            get => loop; 
            set => loop = value;
        }

        public override bool IsPlaying => texture;

        public override void LoadUrl(VRCUrl url, bool reload) {
            if (loader == null) loader = new VRCImageDownloader();
            if (url.Equals(currentUrl) && !reload && texture) {
                isReady = true;
                if (isActive) core.OnVideoReady();
                return;
            }
            loader.DownloadImage(url, null, (IUdonEventReceiver)this);
            currentUrl = url;
            texture = null;
            isReady = false;
        }

        public override void OnImageLoadSuccess(IVRCImageDownload image) {
            if (!image.Url.Equals(currentUrl)) return;
            texture = image.Result;
            isReady = true;
            if (isActive) core.OnVideoReady();
        }

        public override void OnImageLoadError(IVRCImageDownload image) {
            texture = null;
            currentUrl = null;
            isReady = true;
            if (isActive) {
                var error = VideoError.Unknown;
                switch (image.Error) {
                    case VRCImageDownloadError.DownloadError:
                    case VRCImageDownloadError.InvalidImage:
                        error = VideoError.PlayerError;
                        break;
                    case VRCImageDownloadError.InvalidURL:
                        error = VideoError.InvalidURL;
                        break;
                    case VRCImageDownloadError.AccessDenied:
                        error = VideoError.AccessDenied;
                        break;
                    case VRCImageDownloadError.TooManyRequests:
                        error = VideoError.RateLimited;
                        break;
                }
                core.OnVideoError(error);
            }
        }

        public override void Play() {
            isPaused = false;
            if (isActive) {
                core.OnVideoPlay();
                if (texture) core._OnTextureChanged();
            }
        }

        public override void Pause() {
            isPaused = true;
            if (isActive) core.OnVideoPause();
        }

        public override void Stop() {
            texture = null;
            currentUrl = null;
            isReady = false;
            if (isActive) core.OnVideoEnd();
        }
    }
}