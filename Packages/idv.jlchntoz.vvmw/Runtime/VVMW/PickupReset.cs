using UdonSharp;
using UnityEngine;
using JLChnToZ.VRC.I18N;

namespace JLChnToZ.VRC.VVMW.Pickups {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(Collider))]
    public class PickupReset : UdonSharpBehaviour {
        [SerializeField] PickupPanel pickupPanel;
        [SerializeField] LanguageManager languageManager;
        [SerializeField] string interactKey = "Reset";

        void Start() {
            if (languageManager != null) {
                languageManager._AddListener(this);
                _OnLanguageChanged();
            }
        }

        public override void Interact() => pickupPanel._Reset();

        public void _OnLanguageChanged() {
            InteractionText = languageManager.GetLocale(interactKey);
        }
    }
}