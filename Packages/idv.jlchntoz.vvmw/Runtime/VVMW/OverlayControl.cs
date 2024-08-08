using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Overlay Control")]
    [DefaultExecutionOrder(2)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-add-an-overlay-control")]
    public class OverlayControl : VizVidBehaviour {
        Quaternion leftHandRotation = Quaternion.Euler(-90, -45, 0);
        Quaternion rightHandRotation = Quaternion.Euler(90, -45, 180);
        Vector3 offsetDirection = new Vector3(0, 1, -1);
        [LocalizedHeader("HEADER:Main_Reference")]
        [SerializeField, Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        ), BindUdonSharpEvent, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] Core core;
        [LocalizedHeader("HEADER:Non_VizVid_References")]
        [SerializeField, LocalizedLabel] AudioSource[] audioSources;
        [SerializeField, LocalizedLabel] GameObject[] resyncTargets;
        [SerializeField, LocalizedLabel, Range(0, 1)] float volume = 1;
        [LocalizedHeader("HEADER:Options")]
        [SerializeField, LocalizedLabel] KeyCode reloadKey = KeyCode.F9;
        [SerializeField, LocalizedLabel] KeyCode volumeUpKey = KeyCode.F11;
        [SerializeField, LocalizedLabel] KeyCode volumeDownKey = KeyCode.F10;
        [SerializeField, LocalizedLabel] bool disableHandControls;
        [LocalizedHeader("HEADER:UI_References")]
        [SerializeField, LocalizedLabel] GameObject desktopModeCanvas;
        [SerializeField, LocalizedLabel] GameObject vrModeCanvas;
        Transform vrModeCanvasTransform;
        [SerializeField, LocalizedLabel] GameObject vrModeOptionsCanvas, desktopModeOptionsCanvas;
        [BindEvent(nameof(Button.onClick), nameof(_OnReload))]
        [SerializeField, LocalizedLabel] Button reloadButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_OnHandToggle))]
        [SerializeField, LocalizedLabel] Toggle leftHandToggle, rightHandToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnOffsetChange))]
        [SerializeField, LocalizedLabel] Slider offsetSliderVR;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSliderChanged))]
        [SerializeField, LocalizedLabel] Slider volumeSliderVR;
        [SerializeField, LocalizedLabel] RectTransform volumeSliderDesktop;
        [TMProMigratable(nameof(desktopHintsReloadButtonKeyTMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsReloadButtonKey;
        [TMProMigratable(nameof(desktopHintsVolumeUpKeyTMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsVolumeUpKey;
        [TMProMigratable(nameof(desktopHintsVolumeDownKeyTMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsVolumeDownKey;
        [TMProMigratable(nameof(desktopHintsReloadButtonKey2TMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsReloadButtonKey2;
        [TMProMigratable(nameof(desktopHintsVolumeUpKey2TMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsVolumeUpKey2;
        [TMProMigratable(nameof(desktopHintsVolumeDownKey2TMPro))]
        [SerializeField, LocalizedLabel] Text desktopHintsVolumeDownKey2;
        [SerializeField, LocalizedLabel] TextMeshProUGUI desktopHintsReloadButtonKeyTMPro, desktopHintsVolumeUpKeyTMPro, desktopHintsVolumeDownKeyTMPro;
        [SerializeField, LocalizedLabel] TextMeshProUGUI desktopHintsReloadButtonKey2TMPro, desktopHintsVolumeUpKey2TMPro, desktopHintsVolumeDownKey2TMPro;
        Animator desktopModeAnim;
        bool vrMode, afterFirstRun;
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

        void OnEnable() {
            afterFirstRun = true;
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
            if (desktopHintsReloadButtonKeyTMPro != null)
                desktopHintsReloadButtonKeyTMPro.text = reloadKey.ToString();
            if (desktopHintsVolumeUpKey != null)
                desktopHintsVolumeUpKey.text = volumeUpKey.ToString();
            if (desktopHintsVolumeUpKeyTMPro != null)
                desktopHintsVolumeUpKeyTMPro.text = volumeUpKey.ToString();
            if (desktopHintsVolumeDownKey != null)
                desktopHintsVolumeDownKey.text = volumeDownKey.ToString();
            if (desktopHintsVolumeDownKeyTMPro != null)
                desktopHintsVolumeDownKeyTMPro.text = volumeDownKey.ToString();
            if (desktopHintsReloadButtonKey2 != null)
                desktopHintsReloadButtonKey2.text = reloadKey.ToString();
            if (desktopHintsReloadButtonKey2TMPro != null)
                desktopHintsReloadButtonKey2TMPro.text = reloadKey.ToString();
            if (desktopHintsVolumeUpKey2 != null)
                desktopHintsVolumeUpKey2.text = volumeUpKey.ToString();
            if (desktopHintsVolumeUpKey2TMPro != null)
                desktopHintsVolumeUpKey2TMPro.text = volumeUpKey.ToString();
            if (desktopHintsVolumeDownKey2 != null)
                desktopHintsVolumeDownKey2.text = volumeDownKey.ToString();
            if (desktopHintsVolumeDownKey2TMPro != null)
                desktopHintsVolumeDownKey2TMPro.text = volumeDownKey.ToString();
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
            if (!afterFirstRun) return;
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