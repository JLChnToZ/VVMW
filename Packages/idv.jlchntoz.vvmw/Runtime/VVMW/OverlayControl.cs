using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    public class OverlayControl : UdonSharpBehaviour {
        Quaternion leftHandRotation = Quaternion.Euler(-90, -45, 0);
        Quaternion rightHandRotation = Quaternion.Euler(90, -45, 180);
        Vector3 offsetDirection = new Vector3(0, 1, -1);
        [Header("References")]
        [SerializeField, Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] Core core;
        [Header("References (For use with non-VizVid players)")]
        [SerializeField] AudioSource[] audioSources;
        [SerializeField] GameObject[] resyncTargets;
        [SerializeField, Range(0, 1)] float volume = 1;
        [Header("Options")]
        [SerializeField] KeyCode reloadKey = KeyCode.F9;
        [SerializeField] KeyCode volumeUpKey = KeyCode.F11;
        [SerializeField] KeyCode volumeDownKey = KeyCode.F10;
        [Tooltip("Disable hand controls in VR mode at start")]
        [SerializeField] bool disableHandControls;
        [Header("UI References")]
        [SerializeField] GameObject desktopModeCanvas;
        [SerializeField] GameObject vrModeCanvas;
        Transform vrModeCanvasTransform;
        [SerializeField] GameObject vrModeOptionsCanvas, desktopModeOptionsCanvas;
        [BindEvent(nameof(Button.onClick), nameof(_OnReload))]
        [SerializeField] Button reloadButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_OnHandToggle))]
        [SerializeField] Toggle leftHandToggle, rightHandToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnOffsetChange))]
        [SerializeField] Slider offsetSliderVR;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSliderChanged))]
        [SerializeField] Slider volumeSliderVR;
        [SerializeField] RectTransform volumeSliderDesktop;
        [SerializeField] Text desktopHintsReloadButtonKey, desktopHintsVolumeUpKey, desktopHintsVolumeDownKey;
        Animator desktopModeAnim;
        bool vrMode;
        VRCPlayerApi localPlayer;
        [System.NonSerialized] public bool isLeftHanded;
        float offset = 0.05F;

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
            desktopModeOptionsCanvas.SetActive(!vrMode);
            desktopModeAnim = desktopModeCanvas.GetComponentInChildren<Animator>();
            if (Utilities.IsValid(core)) core._AddListener(this);
            _OnVolumeChange();
            offsetSliderVR.SetValueWithoutNotify(Mathf.Log(offset, 1.5F));
            vrModeCanvasTransform = vrModeCanvas.transform;
            if (disableHandControls) {
                leftHandToggle.SetIsOnWithoutNotify(false);
                rightHandToggle.SetIsOnWithoutNotify(false);
            } else {
                leftHandToggle.SetIsOnWithoutNotify(!isLeftHanded);
                rightHandToggle.SetIsOnWithoutNotify(isLeftHanded);
            }
            if (desktopHintsReloadButtonKey != null)
                desktopHintsReloadButtonKey.text = reloadKey.ToString();
            if (desktopHintsVolumeUpKey != null)
                desktopHintsVolumeUpKey.text = volumeUpKey.ToString();
            if (desktopHintsVolumeDownKey != null)
                desktopHintsVolumeDownKey.text = volumeDownKey.ToString();
        }

        void Update() {
            if (!Utilities.IsValid(localPlayer)) return;
            if (vrMode) {
                if (disableHandControls) {
                    vrModeCanvas.SetActive(false);
                    return;
                }
                var head = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head);
                var hand = localPlayer.GetTrackingData(
                    isLeftHanded ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand
                );
                var canvasRotation = hand.rotation * (isLeftHanded ? rightHandRotation : leftHandRotation);
                var canvasPosition = hand.position + canvasRotation * (offsetDirection * offset);
                vrModeCanvasTransform.SetPositionAndRotation(canvasPosition, canvasRotation);
                vrModeCanvas.SetActive(Vector3.Angle(head.rotation * Vector3.forward, (canvasPosition - head.position).normalized) < 30);
            } else if (Input.anyKey) {
                if (Input.GetKeyDown(reloadKey))
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
                volumeSliderDesktop.anchorMax = new Vector2(volume, 1);
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
            offset = Mathf.Pow(1.5F, offsetSliderVR.value);
        }
    }
}