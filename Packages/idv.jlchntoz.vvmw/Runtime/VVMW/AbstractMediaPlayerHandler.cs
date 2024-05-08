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

        public abstract void LoadUrl(VRCUrl url, bool reload);

        public virtual void Play() {}

        public virtual void Pause() {}

        public virtual void Stop() {}
    }
}