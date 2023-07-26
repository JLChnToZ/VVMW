using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    public class OverlayControl : UdonSharpBehaviour {
        [Header("References")]
        [SerializeField] Core core;
        [SerializeField] LanguageManager languageManager;
        [Header("References (For use with non-VMMV players)")]
        [SerializeField] AudioSource[] audioSources;
        [SerializeField] GameObject[] resyncTargets;
        [SerializeField, Range(0, 1)] float volume = 1;
        [Header("Options")]
        [SerializeField] KeyCode reloadKey = KeyCode.F9;
        [SerializeField] KeyCode volumeUpKey = KeyCode.F11;
        [SerializeField] KeyCode volumeDownKey = KeyCode.F10;
        [Header("UI References")]
        [SerializeField] GameObject desktopModeCanvas;
        [SerializeField] GameObject vrModeCanvas;
        [SerializeField] Transform vrModeCanvasContent;
        [SerializeField] GameObject vrModeOptionsCanvas;
        [BindEvent(nameof(Button.onClick), nameof(_OnReload))]
        [SerializeField] Button reloadButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_OnHandToggle))]
        [SerializeField] Toggle leftHandToggle, rightHandToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnOffsetChange))]
        [SerializeField] Slider offsetSliderVR;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSliderChanged))]
        [SerializeField] Slider volumeSliderVR;
        [SerializeField] Image volumeSliderDesktop;
        [SerializeField] Text hintsDesktop;
        Animator desktopModeAnim;
        bool vrMode;
        bool disableHandControls;
        VRCPlayerApi localPlayer;
        [System.NonSerialized] public bool isLeftHanded;

        public float Volume {
            get => volume;
            set {
                volume = value;
                if (Utilities.IsValid(core))
                    core.Volume = value;
                else
                    _OnVolumeChange();
            }
        }

        void Start() {
            localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)) {
                enabled = false;
                return;
            }
            vrMode = localPlayer.IsUserInVR();
            vrModeCanvas.SetActive(vrMode);
            vrModeOptionsCanvas.SetActive(vrMode);
            desktopModeCanvas.SetActive(!vrMode);
            desktopModeAnim = desktopModeCanvas.GetComponentInChildren<Animator>();
            if (Utilities.IsValid(core)) core._AddListener(this);
            _OnVolumeChange();
            offsetSliderVR.SetValueWithoutNotify(Mathf.Log(vrModeCanvasContent.localPosition.magnitude, 1.5F));
            if (languageManager != null) {
                languageManager._AddListener(this);
                _OnLanguageChange();
            }
        }

        void Update() {
            if (!Utilities.IsValid(localPlayer)) return;
            if (vrMode) {
                var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                var hand = localPlayer.GetTrackingData(
                    isLeftHanded ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand
                );
                vrModeCanvas.SetActive(!disableHandControls && Quaternion.Angle(head.rotation, Quaternion.LookRotation(hand.position - head.position)) < 30);
                vrModeCanvas.transform.SetPositionAndRotation(hand.position, hand.rotation * Quaternion.AngleAxis(isLeftHanded ? 90 : -90, Vector3.right));
            } else if (Input.anyKey) {
                if (Input.GetKey(reloadKey))
                    _OnReload();
                if (Input.GetKey(volumeDownKey))
                    Volume -= 0.3F * Time.deltaTime;
                if (Input.GetKey(volumeUpKey))
                    Volume += 0.3F * Time.deltaTime;
            }
        }

        public void _OnReload() {
            bool hasCore = Utilities.IsValid(core);
            if (hasCore && core.IsPlaying)
                core.LocalSync();
            else if (!(hasCore && core.IsLoading) && resyncTargets != null)
                foreach (var target in resyncTargets)
                    if (target.gameObject.activeInHierarchy) {
                        foreach (var ub in target.GetComponents(typeof(UdonBehaviour)))
                            ((UdonBehaviour)ub).SendCustomEvent("Resync");
                        break;
                    }
            if (!vrMode) desktopModeAnim.SetTrigger("Reload");
        }

        public void _OnVolumeSliderChanged() {
            Volume = volumeSliderVR.value;
        }

        public void _OnVolumeChange() {
            if (Utilities.IsValid(core)) volume = core.Volume;
            if (vrMode) {
                volumeSliderVR.SetValueWithoutNotify(volume);
            } else {
                volumeSliderDesktop.fillAmount = volume;
                desktopModeAnim.SetTrigger("VolumeChange");
            }
            if (audioSources != null)
                foreach (var audioSource in audioSources)
                    audioSource.volume = volume;
        }

        public void _OnHandToggle() {
            if (leftHandToggle.isOn) {
                disableHandControls = false;
                isLeftHanded = false;
            } else if (rightHandToggle.isOn) {
                disableHandControls = false;
                isLeftHanded = true;
            } else {
                disableHandControls = true;
            }
        }

        public void _OnOffsetChange() {
            vrModeCanvasContent.localPosition = Vector3.back * Mathf.Pow(1.5F, offsetSliderVR.value);
        }

        public void _OnLanguageChange() {
            hintsDesktop.text = string.Format(languageManager.GetLocale("DesktopOverlay"), reloadKey, volumeUpKey, volumeDownKey);
        }
    }
}