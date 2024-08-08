using System;
using UnityEngine;
using UdonSharp;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field)]
    public class BindUdonSharpEventAttribute : Attribute {}

    public abstract class UdonSharpEventSender : UdonSharpBehaviour {
        [SerializeField, LocalizedLabel] protected UdonSharpBehaviour[] targets;

        public void _AddListener(UdonSharpBehaviour callback) {
            if (callback == null) return;
            if (targets == null) {
                targets = new UdonSharpBehaviour[] { callback };
                return;
            }
            if (Array.IndexOf(targets, callback) >= 0) return;
            var temp = new UdonSharpBehaviour[targets.Length + 1];
            Array.Copy(targets, temp, targets.Length);
            temp[targets.Length] = callback;
            targets = temp;
        }

        protected void SendEvent(string name) {
            if (targets == null) return;
            Debug.Log($"[{GetUdonTypeName()}] Send Event {name}");
            foreach (var ub in targets) if (ub != null) ub.SendCustomEvent(name);
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        protected static void MergeTargets(UdonSharpEventSender[] singletons) {
            UdonSharpEventSender first = null;
            int count = 0;
            foreach (var singleton in singletons) {
                if (singleton == null) continue;
                if (singleton.targets == null) continue;
                if (first == null) first = singleton;
                count += singleton.targets.Length;
            }
            if (count < 1) return;
            var targets = new UdonSharpBehaviour[count];
            count = 0;
            foreach (var singleton in singletons) {
                if (singleton == null) continue;
                if (singleton.targets == null) continue;
                Array.Copy(singleton.targets, 0, targets, count, singleton.targets.Length);
                count += singleton.targets.Length;
            }
            first.targets = targets;
        }
#endif
    }
}