using System;
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
    /// <summary>
    /// A component displays a overlay UI (PC), or wrist UI (VR) for controlling the VizVid video player.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Overlay Control")]
    [DefaultExecutionOrder(2)]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-add-an-overlay-control")]
    public partial class OverlayControl : VizVidBehaviour {

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
        ), BindUdonSharpEvent, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        Core core;
        [SerializeField, HideInInspector, BindUdonSharpEvent] ActiveRegionManager activeRegionManager;
        [LocalizedHeader("HEADER:Non_VizVid_References")]
        [SerializeField, LocalizedLabel] AudioSource[] audioSources;
        [SerializeField, LocalizedLabel] GameObject[] resyncTargets;
        [SerializeField, LocalizedLabel, Range(0, 1)] float volume = 1;
        [LocalizedHeader("HEADER:Options")]
        [SerializeField, LocalizedLabel] KeyCode fullscreenScreenKey = KeyCode.F8;
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
        [SerializeField, LocalizedLabel]
        GameObject
            desktopHintsReloadButtonKeyGO, desktopHintsVolumeUpKeyGO, desktopHintsVolumeDownKeyGO, desktopHintsFullscreenKeyGO,
            desktopHintsReloadButtonKey2GO, desktopHintsVolumeUpKey2GO, desktopHintsVolumeDownKey2GO, desktopHintsFullscreenKey2GO,
            escapeFullscreenHintGO;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsReloadButtonKeyGO), NullOnly = false)] Text desktopHintsReloadButtonKey;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeUpKeyGO), NullOnly = false)] Text desktopHintsVolumeUpKey;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeDownKeyGO), NullOnly = false)] Text desktopHintsVolumeDownKey;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsFullscreenKeyGO), NullOnly = false)] Text desktopHintsFullscreenKey;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsReloadButtonKey2GO), NullOnly = false)] Text desktopHintsReloadButtonKey2;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeUpKey2GO), NullOnly = false)] Text desktopHintsVolumeUpKey2;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeDownKey2GO), NullOnly = false)] Text desktopHintsVolumeDownKey2;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsFullscreenKey2GO), NullOnly = false)] Text desktopHintsFullscreenKey2;
        [SerializeField, HideInInspector, Resolve(nameof(escapeFullscreenHintGO), NullOnly = false)] Text escapeFullscreenHint;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsReloadButtonKeyGO), NullOnly = false)] TextMeshProUGUI desktopHintsReloadButtonKeyTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeUpKeyGO), NullOnly = false)] TextMeshProUGUI desktopHintsVolumeUpKeyTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeDownKeyGO), NullOnly = false)] TextMeshProUGUI desktopHintsVolumeDownKeyTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsFullscreenKeyGO), NullOnly = false)] TextMeshProUGUI desktopHintsFullscreenKeyTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsReloadButtonKey2GO), NullOnly = false)] TextMeshProUGUI desktopHintsReloadButtonKey2TMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeUpKey2GO), NullOnly = false)] TextMeshProUGUI desktopHintsVolumeUpKey2TMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsVolumeDownKey2GO), NullOnly = false)] TextMeshProUGUI desktopHintsVolumeDownKey2TMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopHintsFullscreenKey2GO), NullOnly = false)] TextMeshProUGUI desktopHintsFullscreenKey2TMPro;
        [SerializeField, HideInInspector, Resolve(nameof(escapeFullscreenHintGO), NullOnly = false)] TextMeshProUGUI escapeFullscreenHintTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(desktopModeCanvas))] Animator desktopModeAnim;
        [SerializeField, LocalizedLabel] RawImage desktopModeFullscreenOverlay;
        [SerializeField, HideInInspector, Resolve(nameof(desktopModeFullscreenOverlay))] AspectRatioFitter desktopModeFullscreenOverlayAspectFitter;
        [SerializeField, HideInInspector, Resolve(nameof(desktopModeFullscreenOverlay))] GameObject desktopModeFullscreenOverlayGO;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;
        Rect normalRect = new Rect(0, 0, 1, 1), flippedRect = new Rect(0, 1, 1, -1);
        bool vrMode, afterFirstRun, volumeChanging;
        VRCPlayerApi localPlayer;
        [NonSerialized] public bool isLeftHanded;
        float offset = 0.05F;
        int reloadAnimKey, volumeChangeAnimKey, fullscreenAnimKey;
        Core[] matchingCores;
        int matchingCoreCount;
        bool fullscreen;

        public float Volume {
            get => volume;
            set {
                volume = Mathf.Clamp01(value);
                volumeChanging = true;
                for (int i = 0; i < matchingCoreCount; i++) {
                    var core = matchingCores[i];
                    if (Utilities.IsValid(core)) core.Volume = volume;
                }
                if (Utilities.IsValid(audioSources) && audioSources.Length > 0) {
                    var normalizedVolume = volume * volume; // Volume is not linear
                    foreach (var audioSource in audioSources)
                        audioSource.volume = normalizedVolume;
                }
                volumeChanging = false;
                UpdateVolume();
            }
        }

        void OnEnable() {
            afterFirstRun = true;
            localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)) {
                enabled = false;
                return;
            }
            reloadAnimKey = Animator.StringToHash("Reload");
            volumeChangeAnimKey = Animator.StringToHash("VolumeChange");
            fullscreenAnimKey = Animator.StringToHash("Fullscreen");
            vrMode = localPlayer.IsUserInVR();
            vrModeCanvas.SetActive(vrMode);
            vrModeOptionsCanvas.SetActive(vrMode);
            desktopModeCanvas.SetActive(!vrMode);
            desktopModeOptionsCanvas.SetActive(!vrMode);
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
            if (fullscreenScreenKey == KeyCode.None) {
                if (Utilities.IsValid(desktopHintsFullscreenKeyGO))
                    desktopHintsFullscreenKeyGO.SetActive(false);
                if (Utilities.IsValid(desktopHintsFullscreenKey2GO))
                    desktopHintsFullscreenKey2GO.SetActive(false);
            } else {
                if (Utilities.IsValid(desktopHintsFullscreenKey))
                    desktopHintsFullscreenKey.text = fullscreenScreenKey.ToString();
                if (Utilities.IsValid(desktopHintsFullscreenKeyTMPro))
                    desktopHintsFullscreenKeyTMPro.text = fullscreenScreenKey.ToString();
                if (Utilities.IsValid(desktopHintsFullscreenKey2))
                    desktopHintsFullscreenKey2.text = fullscreenScreenKey.ToString();
                if (Utilities.IsValid(desktopHintsFullscreenKey2TMPro))
                    desktopHintsFullscreenKey2TMPro.text = fullscreenScreenKey.ToString();
                UpdateHintText();
            }
            _OnVolumeChange();
        }

        void UpdateHintText() {
            var escapeHintText = string.Format(languageManager.GetLocale("OverlayEscapeHint"), fullscreenScreenKey);
            if (Utilities.IsValid(escapeFullscreenHint))
                escapeFullscreenHint.text = escapeHintText;
            if (Utilities.IsValid(escapeFullscreenHintTMPro))
                escapeFullscreenHintTMPro.text = escapeHintText;
        }

#if VRC_ENABLE_PLAYER_PERSISTENCE
        public override void OnPlayerRestored(VRCPlayerApi player) {
            if (!player.isLocal) return;
            if (PlayerData.HasKey(player, PlayerPersistenceHandKey)) {
                int hand = PlayerData.GetByte(player, PlayerPersistenceHandKey);
                disableHandControls = hand == 0;
                isLeftHanded = hand == 2;
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
                var headPos = head.position;
                if (!Utilities.IsValid(core)) {
                    vrModeCanvas.SetActive(false);
                    return;
                }
                var hand = localPlayer.GetTrackingData(
                    isLeftHanded ? VRCPlayerApi.TrackingDataType.RightHand : VRCPlayerApi.TrackingDataType.LeftHand
                );
                var canvasRotation = hand.rotation * (isLeftHanded ? rightHandRotation : leftHandRotation);
                var canvasPosition = hand.position + canvasRotation * (offsetDirection * offset);
                vrModeCanvasTransform.SetPositionAndRotation(canvasPosition, canvasRotation);
                vrModeCanvas.SetActive(Vector3.Angle(head.rotation * Vector3.forward, (canvasPosition - headPos).normalized) < 30);
                return;
            }
            if (Input.anyKey) {
                if (Input.GetKeyDown(reloadKey) && !AnyModifierKeyDown())
                    _OnReload();
                else if (Input.GetKey(volumeDownKey) && !AnyModifierKeyDown())
                    Volume -= 0.3F * Time.deltaTime;
                else if (Input.GetKey(volumeUpKey) && !AnyModifierKeyDown())
                    Volume += 0.3F * Time.deltaTime;
                else if (fullscreenScreenKey != KeyCode.None && Input.GetKeyDown(fullscreenScreenKey) && !AnyModifierKeyDown()) {
                    fullscreen = !fullscreen;
                    _OnTextureChanged();
                } else if (Input.GetKeyDown(KeyCode.Escape) && fullscreen) {
                    fullscreen = false;
                    _OnTextureChanged();
                }
            }
        }

        bool AnyModifierKeyDown() =>
            Input.GetKey(KeyCode.LeftControl) ||
            Input.GetKey(KeyCode.RightControl) ||
            Input.GetKey(KeyCode.LeftCommand) ||
            Input.GetKey(KeyCode.RightCommand) ||
            Input.GetKey(KeyCode.LeftAlt) ||
            Input.GetKey(KeyCode.RightAlt) ||
            Input.GetKey(KeyCode.LeftShift) ||
            Input.GetKey(KeyCode.RightShift);

#if COMPILER_UDONSHARP
        public
#endif
        void _OnActiveCoreChanged() {
            core = activeRegionManager.core;
            _OnTextureChanged();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnMatchingCoresChanged() {
            matchingCores = activeRegionManager.matchingCores;
            matchingCoreCount = activeRegionManager.matchingCoreCount;
        }

        public void _OnReload() {
            bool hasCoreMatch = false;
            for (int i = 0; i < matchingCoreCount; i++)
                hasCoreMatch |= ReloadCore(matchingCores[i]);
            if (!hasCoreMatch && Utilities.IsValid(resyncTargets))
                foreach (var target in resyncTargets)
                    if (target.activeInHierarchy) {
                        foreach (var ub in target.GetComponents(typeof(UdonBehaviour)))
                            ((UdonBehaviour)ub).SendCustomEvent("Resync");
                        break;
                    }
            if (!vrMode) desktopModeAnim.SetTrigger(reloadAnimKey);
        }

        bool ReloadCore(Core coreToReload) {
            bool hasCore = Utilities.IsValid(coreToReload);
            if (hasCore && coreToReload.IsPlaying) {
                coreToReload.LocalSync();
                return true;
            }
            return false;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnLanguageChanged() {
            if (fullscreenScreenKey != KeyCode.None)
                UpdateHintText();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnVolumeSliderChanged() =>
            Volume = volumeSliderVR.value;

#if COMPILER_UDONSHARP
        public
#endif
        void _OnVolumeChange() {
            if (!afterFirstRun || volumeChanging || !Utilities.IsValid(core)) return;
            volume = core.Volume;
            UpdateVolume();
        }

        void UpdateVolume() {
            if (vrMode) {
                volumeSliderVR.SetValueWithoutNotify(volume);
            } else {
                volumeSliderDesktop.anchorMax = new Vector2(volume, 1);
                desktopModeAnim.SetTrigger(volumeChangeAnimKey);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnTextureChanged() {
            if (!Utilities.IsValid(desktopModeFullscreenOverlay)) return;
            bool wasVisible = desktopModeFullscreenOverlayGO.activeSelf;
            if (!fullscreen || !Utilities.IsValid(core)) {
                if (wasVisible) desktopModeFullscreenOverlayGO.SetActive(false);
                return;
            }
            var texture = core.VideoTexture;
            if (!Utilities.IsValid(texture)) {
                if (wasVisible) desktopModeFullscreenOverlayGO.SetActive(false);
                return;
            }
            desktopModeFullscreenOverlay.texture = texture;
            if (Utilities.IsValid(desktopModeFullscreenOverlayAspectFitter))
                desktopModeFullscreenOverlayAspectFitter.aspectRatio = (float)texture.width / texture.height;
#if UNITY_STANDALONE_WIN
            desktopModeFullscreenOverlay.uvRect = core.IsAVPro ? flippedRect : normalRect;
#endif
            desktopModeFullscreenOverlayGO.SetActive(true);
            if (!wasVisible) desktopModeAnim.SetTrigger(fullscreenAnimKey);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnHandToggle() {
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

#if COMPILER_UDONSHARP
        public
#endif
        void _OnOffsetChange() {
            offset = Mathf.Pow(1.5F, offsetSliderVR.value);
#if VRC_ENABLE_PLAYER_PERSISTENCE
            PlayerData.SetFloat(PlayerPersistenceDistanceKey, offset);
#endif
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    public partial class OverlayControl : ISelfPreProcess {
        int IPrioritizedPreProcessor.Priority => 0;

        void ISelfPreProcess.PreProcess() {
            foreach (var core in gameObject.scene.IterateAllComponents<Core>())
                core._AddListener(this);
        }
    }
#endif
}
