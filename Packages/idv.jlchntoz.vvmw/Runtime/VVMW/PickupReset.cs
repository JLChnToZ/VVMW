﻿using UdonSharp;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW.Pickups {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Pickup Reset")]
    public class PickupReset : UdonSharpBehaviour {
        [SerializeField, LocalizedLabel] PickupPanel pickupPanel;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;
        [SerializeField, LocalizedLabel] string interactKey = "Reset";

        void Start() {
            if (languageManager != null) {
                _OnLanguageChanged();
            }
        }

        public override void Interact() => pickupPanel._Reset();

        public void _OnLanguageChanged() {
            InteractionText = languageManager.GetLocale(interactKey);
        }
    }
}