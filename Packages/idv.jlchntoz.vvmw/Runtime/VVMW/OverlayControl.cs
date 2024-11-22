using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
#if VRC_ENABLE_PLAYER_PERSISTENCE
using VRC.SDK3.Persistence;
#endif

namespace JLChnToZ.VRC.VVMW {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Overlay Control")]
    [DefaultExecutionOrder(2)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-add-an-overlay-control")]
    public class OverlayControl : VizVidBehaviour {

#if VRC_ENABLE_PLAYER_PERSISTENCE
        const string PlayerPersistenceHandKey = "VVMW:OverlayControl:Hand";
        const string PlayerPersistenceDistanceKey = "VVMW:OverlayControl:Distances";
#endif
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
            if (Utilities.IsValid(desktopHintsReloadButtonKey))
                desktopHintsReloadButtonKey.text = reloadKey.ToString();
            if (Utilities.IsValid(desktopHintsReloadButtonKeyTMPro))
                desktopHintsReloadButtonKeyTMPro.text = reloadKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeUpKey))
                desktopHintsVolumeUpKey.text = volumeUpKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeUpKeyTMPro))
                desktopHintsVolumeUpKeyTMPro.text = volumeUpKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeDownKey))
                desktopHintsVolumeDownKey.text = volumeDownKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeDownKeyTMPro))
                desktopHintsVolumeDownKeyTMPro.text = volumeDownKey.ToString();
            if (Utilities.IsValid(desktopHintsReloadButtonKey2))
                desktopHintsReloadButtonKey2.text = reloadKey.ToString();
            if (Utilities.IsValid(desktopHintsReloadButtonKey2TMPro))
                desktopHintsReloadButtonKey2TMPro.text = reloadKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeUpKey2))
                desktopHintsVolumeUpKey2.text = volumeUpKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeUpKey2TMPro))
                desktopHintsVolumeUpKey2TMPro.text = volumeUpKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeDownKey2))
                desktopHintsVolumeDownKey2.text = volumeDownKey.ToString();
            if (Utilities.IsValid(desktopHintsVolumeDownKey2TMPro))
                desktopHintsVolumeDownKey2TMPro.text = volumeDownKey.ToString();
        }

#if VRC_ENABLE_PLAYER_PERSISTENCE
        public override void OnPlayerRestored(VRCPlayerApi player) {
            if (!player.isLocal) return;
            if (PlayerData.HasKey(player, PlayerPersistenceHandKey)) {
                int hand = PlayerData.GetByte(player, PlayerPersistenceHandKey);
                disableHandControls = hand == 0;
                isLeftHanded = hand == 1;
                leftHandToggle.SetIsOnWithoutNotify(hand == 1);
                rightHandToggle.SetIsOnWithoutNotify(hand == 2);
            }
            if (PlayerData.HasKey(player, PlayerPersistenceDistanceKey)) {
                offset = PlayerData.GetFloat(player, PlayerPersistenceDistanceKey);
                offsetSliderVR.SetValueWithoutNotify(Mathf.Log(offset, 1.5F));
            }
        }
#endif

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
            else if (!(hasCore && core.IsLoading) && Utilities.IsValid(resyncTargets))
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
            if (Utilities.IsValid(audioSources))
                foreach (var audioSource in audioSources)
                    audioSource.volume = volume;
        }

        public void _OnHandToggle() {
            if (leftHandToggle.isOn) {
                disableHandControls = false;
                isLeftHanded = false;
#if VRC_ENABLE_PLAYER_PERSISTENCE
                PlayerData.SetByte(PlayerPersistenceHandKey, 1);
#endif
            } else if (rightHandToggle.isOn) {
                disableHandControls = false;
                isLeftHanded = true;
#if VRC_ENABLE_PLAYER_PERSISTENCE
                PlayerData.SetByte(PlayerPersistenceHandKey, 2);
#endif
            } else {
                disableHandControls = true;
#if VRC_ENABLE_PLAYER_PERSISTENCE
                PlayerData.SetByte(PlayerPersistenceHandKey, 0);
#endif
            }
        }

        public void _OnOffsetChange() {
            offset = Mathf.Pow(1.5F, offsetSliderVR.value);
#if VRC_ENABLE_PLAYER_PERSISTENCE
            PlayerData.SetFloat(PlayerPersistenceDistanceKey, offset);
#endif
        }
    }
}