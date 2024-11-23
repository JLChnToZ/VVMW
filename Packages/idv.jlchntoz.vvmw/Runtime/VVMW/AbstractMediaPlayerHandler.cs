using System;
using UnityEngine;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// The base class for video player backends.
    /// </summary>
    public abstract partial class AbstractMediaPlayerHandler : VizVidBehaviour {
#if COMPILER_UDONSHARP
        [NonSerialized] public
#else
        internal protected
#endif
        Core core;
#if COMPILER_UDONSHARP
        public
#else
        [LocalizedLabel, SerializeField] internal protected
#endif
        string playerName = "";
        protected bool isActive, isReady, isPaused;
        protected Texture texture;
        protected VRCUrl currentUrl;
        [HideInInspector, SerializeField] protected string[] trustedUrlDomains = new string[0]; // This list will be fetched on build, via VRChat SDK

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsActive { get => isActive; set => isActive = value; }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsReady => isReady;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsPlaying => false;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsPaused => isPaused;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool SupportSpeedAdjustment => false;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual AudioSource PrimaryAudioSource => null;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual float Time { get => 0; set { } }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual float Speed { get => 1; set { } }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool Loop { get => false; set { } }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsAvPro => false;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual float Duration => float.PositiveInfinity;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual Texture Texture => texture;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual bool IsStatic => false;

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        abstract void LoadUrl(VRCUrl url, bool reload);

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual void Play() { }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual void Pause() { }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual void Stop() { }

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        bool IsCurrentUrlTrusted() {
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

#if COMPILER_UDONSHARP
        public
#else
        internal protected
#endif
        virtual int IsSupported(string urlStr) => 0;

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

        protected virtual void PreProcess() { }
    }

    internal delegate void ApplyTurstedUrl(TrustedUrlTypes type, ref string[] trustedUrlDomains);
#endif
}