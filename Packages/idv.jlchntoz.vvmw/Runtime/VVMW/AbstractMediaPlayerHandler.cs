using System;
using UnityEngine;
using VRC.SDKBase;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    public abstract class AbstractMediaPlayerHandler : VizVidBehaviour {
        [NonSerialized] public Core core;
        [Tooltip("The name of current video player. Can be the key mapped in language pack JSON.")]
        public string playerName = "";
        protected bool isActive, isReady, isPaused;
        protected Texture texture;
        protected VRCUrl currentUrl;
        [HideInInspector, SerializeField] protected string[] trustedUrlDomains = new string[0]; // This list will be fetched on build, via VRChat SDK

        public virtual bool IsActive { get => isActive; set => isActive = value; }

        public virtual bool IsReady => isReady;

        public virtual bool IsPlaying => false;

        public virtual bool IsPaused => isPaused;

        public virtual AudioSource PrimaryAudioSource => null;

        public virtual float Time { get => 0; set {} }

        public virtual bool Loop { get => false; set {} }

        public virtual bool IsAvPro => false;

        public virtual float Duration => float.PositiveInfinity;

        public virtual Texture Texture => texture;

        public virtual bool IsStatic => false;

        public abstract void LoadUrl(VRCUrl url, bool reload);

        public virtual void Play() {}

        public virtual void Pause() {}

        public virtual void Stop() {}

        public bool IsCurrentUrlTrusted() {
            if (!Utilities.IsValid(currentUrl)) return false;
            var url = currentUrl.Get();
            if (string.IsNullOrEmpty(url)) return false;
            int domainStartIndex = url.IndexOf("://");
            if (domainStartIndex < 0) return false;
            domainStartIndex += 3;
            int endIndex = url.IndexOf('/', domainStartIndex);
            if (endIndex < 0) return false;
            int startIndex = url.LastIndexOf('.', endIndex - 1, endIndex - domainStartIndex - 1);
            if (startIndex < 0) return false;
            startIndex = url.LastIndexOf('.', startIndex - 1, startIndex - domainStartIndex - 1);
            if (startIndex < 0) startIndex = domainStartIndex;
            return Array.IndexOf(trustedUrlDomains, url.Substring(startIndex + 1, endIndex - startIndex - 1)) >= 0;
        }

        public virtual int IsSupported(string urlStr) {
            return 0;
        }

        protected virtual bool TryGetUrl(VRCUrl url, out string urlStr) {
            urlStr = url.Get();
            return !string.IsNullOrEmpty(urlStr);
        }
    }
}