using UnityEngine;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW.Pickups {
    /// <summary>
    /// The reset button for the pickup panel.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Pickup Reset")]
    public class PickupReset : UdonSharpBehaviour {
        [SerializeField, LocalizedLabel, BindUdonSharpEvent] PickupPanel pickupPanel;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;
        [SerializeField, LocalizedLabel] string interactKey = "Reset";
        bool hasPickup;

        void Start() {
            if (Utilities.IsValid(languageManager)) _OnLanguageChanged();
            gameObject.SetActive(hasPickup);
        }

        public override void Interact() => pickupPanel._Reset();

        public void _OnLanguageChanged() {
            InteractionText = languageManager.GetLocale(interactKey);
        }

        public void _OnScreenPickup() {
            hasPickup = true;
            gameObject.SetActive(hasPickup);
        }

        public void _OnReset() {
            hasPickup = false;
            gameObject.SetActive(hasPickup);
        }
    }
}