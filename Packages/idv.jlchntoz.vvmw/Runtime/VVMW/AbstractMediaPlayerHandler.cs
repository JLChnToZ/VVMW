using System;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public abstract partial class AbstractMediaPlayerHandler : VizVidBehaviour {
        [NonSerialized] public Core core;
        [LocalizedLabel] public string playerName = "";
        protected bool isActive, isReady, isPaused;
        protected Texture texture;
        protected VRCUrl currentUrl;
        [HideInInspector, SerializeField] protected string[] trustedUrlDomains = new string[0]; // This list will be fetched on build, via VRChat SDK

        public virtual bool IsActive { get => isActive; set => isActive = value; }

        public virtual bool IsReady => isReady;

        public virtual bool IsPlaying => false;

        public virtual bool IsPaused => isPaused;

        public virtual bool SupportSpeedAdjustment => false;

        public virtual AudioSource PrimaryAudioSource => null;

        public virtual float Time { get => 0; set {} }

        public virtual float Speed { get => 1; set {} }

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
            if (VRCUrl.IsNullOrEmpty(currentUrl)) return false;
            var url = currentUrl.Get();
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

        public virtual int IsSupported(string urlStr) => 0;

        protected virtual bool TryGetUrl(VRCUrl url, out string urlStr) {
            if (VRCUrl.IsNullOrEmpty(url)) {
                urlStr = null;
                return false;
            }
            urlStr = url.Get();
            return true;
        }
    }

    #if UNITY_EDITOR && !COMPILER_UDONSHARP
    public abstract partial class AbstractMediaPlayerHandler : ISelfPreProcess {
        internal static ApplyTurstedUrl applyTurstedUrl; // Actual method is in TrustedUrlUtls

        int IPrioritizedPreProcessor.Priority => -10;

        void ISelfPreProcess.PreProcess() => PreProcess();

        protected virtual void PreProcess() {}
    }

    internal delegate void ApplyTurstedUrl(TrustedUrlTypes type, ref string[] trustedUrlDomains);
    #endif
}