using UdonSharp;
using UnityEngine;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW.Pickups {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Collider))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Pickup Reset")]
    public class PickupReset : UdonSharpBehaviour {
        [SerializeField] PickupPanel pickupPanel;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;
        [SerializeField] string interactKey = "Reset";

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