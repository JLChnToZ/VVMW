using System;
using UnityEngine;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// This module is for reducing rate limit errors by debouncing (on holds and wait for certain time) video switching requests across VizVid instances.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("/VizVid/Components/Rate Limit Resolver")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#rate-limit-resolver")]
    public partial class RateLimitResolver : UdonSharpBehaviour {
        const long TICKS_RATELIMIT = 5050 * TimeSpan.TicksPerMillisecond; // 5 seconds + 50ms (1 / 20fps) buffer.
        long lastUrlLoadTime;

        /// <summary>
        /// Request a safe delay time for loading a new video URL in seconds.
        /// </summary>
        /// <returns>The delay time in seconds.</returns>
        /// <example><code><![CDATA[
        /// // Assume the actual video loading logic lives in `_LoadUrl` method.
        /// float delay = rateLimitResolver._GetSafeLoadUrlDelay();
        /// if (delay > 0) {
        ///    SendCustomEventDelayedSeconds(nameof(_LoadUrl), delay);
        ///    return;
        /// }
        /// _LoadUrl();
        /// ]]>
        /// </code></example>
        public float _GetSafeLoadUrlDelay() {
            var now = DateTime.UtcNow.Ticks;
            var nextSafeTime = lastUrlLoadTime + TICKS_RATELIMIT;
            if (nextSafeTime > now) {
                lastUrlLoadTime = nextSafeTime;
                return (float)(nextSafeTime - now) / TimeSpan.TicksPerSecond;
            }
            lastUrlLoadTime = now;
            return 0;
        }
    }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
    public partial class RateLimitResolver : ISingleton<RateLimitResolver> {
        void ISingleton<RateLimitResolver>.Merge(RateLimitResolver[] others) {} // Do Nothing.
    }
#endif
}