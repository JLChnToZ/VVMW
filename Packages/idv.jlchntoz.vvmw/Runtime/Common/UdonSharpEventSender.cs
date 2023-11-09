using System;
using UnityEngine;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    [AttributeUsage(AttributeTargets.Field)]
    public class BindUdonSharpEventAttribute : Attribute {}

    public abstract class UdonSharpEventSender : UdonSharpBehaviour {
        [Tooltip("UdonSharpBehavior that will receive the event from this object.")]
        [SerializeField] protected UdonSharpBehaviour[] targets;

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
    }
}