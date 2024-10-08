﻿using System;
using UnityEngine;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("/VizVid/Components/Rate Limit Resolver")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#rate-limit-resolver")]
    public partial class RateLimitResolver : UdonSharpBehaviour {
        const long TICKS_RATELIMIT = 5050 * TimeSpan.TicksPerMillisecond; // 5 seconds + 50ms (1 / 20fps) buffer.
        long lastUrlLoadTime;

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