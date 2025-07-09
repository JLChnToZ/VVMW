using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using TMPro;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// The default UI handler for VizVid.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/UI Handler")]
    [DefaultExecutionOrder(2)]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#default-ui--screen-with-overlay")]
    public partial class UIHandler : VizVidBehaviour {
        [LocalizedHeader("HEADER:Main_Reference")]
        [SerializeField, BindUdonSharpEvent, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        [Resolve(nameof(handler) + "." + nameof(FrontendHandler.core), HideInInspectorIfResolvable = true)]
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        )] Core core;
        [Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        ), LocalizedLabel(Key = "VVMW.Handler"), BindUdonSharpEvent]
        public FrontendHandler handler;
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager languageManager;

        [LocalizedHeader("HEADER:URL_Input")]
        [BindEvent(nameof(VRCUrlInputField.onValueChanged), nameof(_OnURLChanged))]
        [BindEvent(nameof(VRCUrlInputField.onEndEdit), nameof(_OnURLEndEdit))]
        [SerializeField, LocalizedLabel] VRCUrlInputField urlInput;
        [BindEvent(nameof(VRCUrlInputField.onValueChanged), nameof(_OnURLChanged))]
        [BindEvent(nameof(VRCUrlInputField.onEndEdit), nameof(_OnURLEndEdit))]
        [SerializeField, LocalizedLabel] VRCUrlInputField altUrlInput;
        [SerializeField, HideInInspector, Resolve(nameof(altUrlInput), NullOnly = false)] GameObject altUrlInputObject;
        [SerializeField, LocalizedLabel] GameObject videoPlayerSelectButtonTemplate;
        [SerializeField, LocalizedLabel] GameObject videoPlayerSelectRoot, videoPlayerSelectPanel;
        [BindEvent(nameof(Button.onClick), nameof(_VideoPlayerSelect))]
        [SerializeField, LocalizedLabel] Button videoPlayerSelectButton;
        [SerializeField, HideInInspector, Resolve(nameof(videoPlayerSelectButton), NullOnly = false)] GameObject videoPlayerSelectButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_InputCancelClick))]
        [SerializeField, LocalizedLabel] Button cancelButton;
        [SerializeField, HideInInspector, Resolve(nameof(cancelButton), NullOnly = false)] GameObject cancelButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_InputConfirmClick))]
        [SerializeField, LocalizedLabel] Button urlInputConfirmButton;
        [SerializeField, HideInInspector, Resolve(nameof(urlInputConfirmButton), NullOnly = false)] GameObject urlInputConfirmButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_EnforceImmedPlayClick))]
        [SerializeField, LocalizedLabel] Button enforcePlayImmediatelyButton;
        [SerializeField, LocalizedLabel] GameObject selectdPlayerObject;
        [SerializeField, HideInInspector, Resolve(nameof(selectdPlayerObject), NullOnly = false)] Text selectdPlayerText;
        [SerializeField, HideInInspector, Resolve(nameof(selectdPlayerObject), NullOnly = false)] TextMeshProUGUI selectdPlayerTMPro;
        [SerializeField, LocalizedLabel] GameObject queueMode;
        [SerializeField, HideInInspector, Resolve(nameof(queueMode), NullOnly = false)] Text queueModeText;
        [SerializeField, HideInInspector, Resolve(nameof(queueMode), NullOnly = false)] TextMeshProUGUI queueModeTMPro;
        [SerializeField, LocalizedLabel] GameObject otherObjectUnderUrlInput;

        [LocalizedHeader("HEADER:Playback_Controls")]
        [SerializeField, LocalizedLabel] Animator playbackControlsAnimator;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField, LocalizedLabel] Button playButton;
        [SerializeField, HideInInspector, Resolve(nameof(playButton), NullOnly = false)] GameObject playButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_Pause))]
        [SerializeField, LocalizedLabel] Button pauseButton;
        [SerializeField, HideInInspector, Resolve(nameof(pauseButton), NullOnly = false)] GameObject pauseButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_Stop))]
        [SerializeField, LocalizedLabel] Button stopButton;
        [SerializeField, HideInInspector, Resolve(nameof(stopButton), NullOnly = false)] GameObject stopButtonObject;

        [BindEvent(nameof(Button.onClick), nameof(_LocalSync))]
        [SerializeField, LocalizedLabel] Button reloadButton;
        [SerializeField, HideInInspector, Resolve(nameof(reloadButton), NullOnly = false)] GameObject reloadButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_GlobalSync))]
        [SerializeField, LocalizedLabel] Button globalReloadButton;
        [SerializeField, HideInInspector, Resolve(nameof(globalReloadButton), NullOnly = false)] GameObject globalReloadButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_Skip))]
        [SerializeField, LocalizedLabel] Button playNextButton;
        [SerializeField, HideInInspector, Resolve(nameof(playNextButton), NullOnly = false)] GameObject playNextButtonObject;
        [SerializeField, LocalizedLabel] GameObject enqueueCountObject;
        [SerializeField, HideInInspector, Resolve(nameof(enqueueCountObject), NullOnly = false)] Text enqueueCountText;
        [SerializeField, HideInInspector, Resolve(nameof(enqueueCountObject), NullOnly = false)] TextMeshProUGUI enqueueCountTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOne))]
        [SerializeField, LocalizedLabel] Button repeatOffButton;
        [SerializeField, HideInInspector, Resolve(nameof(repeatOffButton), NullOnly = false)] GameObject repeatOffButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatAll))]
        [SerializeField, LocalizedLabel] Button repeatOneButton;
        [SerializeField, HideInInspector, Resolve(nameof(repeatOneButton), NullOnly = false)] GameObject repeatOneButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOff))]
        [FormerlySerializedAs("RepeatAllButton")]
        [SerializeField, LocalizedLabel] Button repeatAllButton;
        [SerializeField, HideInInspector, Resolve(nameof(repeatAllButton), NullOnly = false)] GameObject repeatAllButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOn))]
        [SerializeField, LocalizedLabel] Button shuffleOffButton;
        [SerializeField, HideInInspector, Resolve(nameof(shuffleOffButton), NullOnly = false)] GameObject shuffleOffButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOff))]
        [SerializeField, LocalizedLabel] Button shuffleOnButton;
        [SerializeField, HideInInspector, Resolve(nameof(shuffleOnButton), NullOnly = false)] GameObject shuffleOnButtonObject;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_PlayListToggle))]
        [SerializeField, LocalizedLabel] Toggle playlistToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnSeek))]
        [SerializeField, LocalizedLabel] Slider progressSlider;
        [SerializeField, LocalizedLabel] GameObject statusObject, timeObject, durationObject;
        [SerializeField, HideInInspector, Resolve(nameof(statusObject), NullOnly = false)] Text statusText;
        [SerializeField, HideInInspector, Resolve(nameof(timeObject), NullOnly = false)] Text timeText;
        [SerializeField, HideInInspector, Resolve(nameof(durationObject), NullOnly = false)] Text durationText;
        [SerializeField, HideInInspector, Resolve(nameof(statusObject), NullOnly = false)] TextMeshProUGUI statusTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(timeObject), NullOnly = false)] TextMeshProUGUI timeTMPro;
        [SerializeField, HideInInspector, Resolve(nameof(durationObject), NullOnly = false)] TextMeshProUGUI durationTMPro;
        [SerializeField, LocalizedLabel] GameObject timeContainer;

        [LocalizedHeader("HEADER:Volume_Control")]
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSlide))]
        [SerializeField, LocalizedLabel] Slider volumeSlider;
        [BindEvent(nameof(Button.onClick), nameof(_OnMute))]
        [SerializeField, LocalizedLabel] Button muteButton, unmuteButton;
        [SerializeField, HideInInspector, Resolve(nameof(muteButton), NullOnly = false)] GameObject muteButtonObject;
        [SerializeField, HideInInspector, Resolve(nameof(unmuteButton), NullOnly = false)] GameObject unmuteButtonObject;

        [LocalizedHeader("HEADER:Idle_Screen")]
        [SerializeField, LocalizedLabel] GameObject idleScreenRoot;

        [LocalizedHeader("HEADEAR:Queue_List_PlayList")]
        [SerializeField, LocalizedLabel] GameObject playListPanelRoot;
        [SerializeField, LocalizedLabel, BindUdonSharpEvent] PooledScrollView playListScrollView;
        [SerializeField, HideInInspector, Resolve(nameof(playListScrollView), NullOnly = false)] GameObject playListGameObject;
        [BindEvent(nameof(Button.onClick), nameof(_PlayListTogglePanel))]
        [SerializeField, LocalizedLabel] Button playListTogglePanelButton;
        [SerializeField, LocalizedLabel, BindUdonSharpEvent] PooledScrollView queueListScrollView;
        [SerializeField, HideInInspector, Resolve(nameof(queueListScrollView), NullOnly = false)] GameObject queueListScrollViewObject;
        [SerializeField, LocalizedLabel] GameObject playNextIndicator;
        [SerializeField, LocalizedLabel] GameObject selectedPlayListObject;
        [SerializeField, HideInInspector, Resolve(nameof(selectedPlayListObject), NullOnly = false)] Text selectedPlayListText;
        [SerializeField, HideInInspector, Resolve(nameof(selectedPlayListObject), NullOnly = false)] TextMeshProUGUI selectedPlayListTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_OnCurrentPlayListSelectClick))]
        [SerializeField, LocalizedLabel] Button currentPlayListButton;
        [SerializeField, HideInInspector, Resolve(nameof(currentPlayListButton), NullOnly = false)] GameObject currentPlayListButtonObject;
        [SerializeField, LocalizedLabel] bool autoHideCurrentPlayListButton = true;

        [LocalizedHeader("HEADER:Sync_Offset_Controls")]
        [SerializeField, LocalizedLabel] GameObject shiftControlsRoot;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBackL))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftBack100msButton")] Button shiftBackLButton;
        [SerializeField, HideInInspector, Resolve(nameof(shiftBackLButton), NullOnly = false)] GameObject shiftBackLButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBackS))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftBack50msButton")] Button shiftBackSButton;
        [SerializeField, HideInInspector, Resolve(nameof(shiftBackSButton), NullOnly = false)] GameObject shiftBackSButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForwardS))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftForward50msButton")] Button shiftForwardSButton;
        [SerializeField, HideInInspector, Resolve(nameof(shiftForwardSButton), NullOnly = false)] GameObject shiftForwardSButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForwardL))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftForward100msButton")] Button shiftForwardLButton;
        [SerializeField, HideInInspector, Resolve(nameof(shiftForwardLButton), NullOnly = false)] GameObject shiftForwardLButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftReset))]
        [SerializeField, LocalizedLabel] Button shiftResetButton;
        [SerializeField, HideInInspector, Resolve(nameof(shiftResetButton), NullOnly = false)] GameObject shiftResetButtonObject;
        [SerializeField, LocalizedLabel] GameObject shiftOffsetObject;
        [SerializeField, HideInInspector, Resolve(nameof(shiftOffsetObject), NullOnly = false)] Text shiftOffsetText;
        [SerializeField, HideInInspector, Resolve(nameof(shiftOffsetObject), NullOnly = false)] TextMeshProUGUI shiftOffsetTMPro;

        [BindEvent(nameof(Button.onClick), nameof(_PerformanceModeToggle))]
        [SerializeField, LocalizedLabel] Button performanceModeToggle;
        [SerializeField, LocalizedLabel] GameObject performanceModeSelf, performanceModeOthers, performanceModeOff;
        [SerializeField, LocalizedLabel] GameObject performerDisplay;
        [SerializeField, HideInInspector, Resolve(nameof(performerDisplay), NullOnly = false)] Text performerText;
        [SerializeField, HideInInspector, Resolve(nameof(performerDisplay), NullOnly = false)] TextMeshProUGUI performerTMPro;

        [LocalizedHeader("HEADER:Speed_Adjustment_Controls")]
        [SerializeField, LocalizedLabel] GameObject speedControlsRoot;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedDownL))]
        [SerializeField, LocalizedLabel] Button speedDownLButton;
        [SerializeField, HideInInspector, Resolve(nameof(speedDownLButton), NullOnly = false)] GameObject speedDownLButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedDownS))]
        [SerializeField, LocalizedLabel] Button speedDownSButton;
        [SerializeField, HideInInspector, Resolve(nameof(speedDownSButton), NullOnly = false)] GameObject speedDownSButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedUpS))]
        [SerializeField, LocalizedLabel] Button speedUpSButton;
        [SerializeField, HideInInspector, Resolve(nameof(speedUpSButton), NullOnly = false)] GameObject speedUpSButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedUpL))]
        [SerializeField, LocalizedLabel] Button speedUpLButton;
        [SerializeField, HideInInspector, Resolve(nameof(speedUpLButton), NullOnly = false)] GameObject speedUpLButtonObject;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedReset))]
        [SerializeField, LocalizedLabel] Button speedResetButton;
        [SerializeField, HideInInspector, Resolve(nameof(speedResetButton), NullOnly = false)] GameObject speedResetButtonObject;
        [SerializeField, LocalizedLabel] GameObject speedOffsetObject;
        [SerializeField, HideInInspector, Resolve(nameof(speedOffsetObject), NullOnly = false)] Text speedOffsetText;
        [SerializeField, HideInInspector, Resolve(nameof(speedOffsetObject), NullOnly = false)] TextMeshProUGUI speedOffsetTMPro;

        [LocalizedHeader("HEADER:Screen_Controls")]
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnLuminanceSliderChanged))]
        [SerializeField, LocalizedLabel] Slider luminanceSlider;
        [SerializeField, LocalizedLabel] string luminancePropertyName = "_EmissionIntensity";
        int luminancePropertyId;

        bool hasUpdate, wasUnlocked, hasUnlockInit;
        byte selectedPlayer = 1;
        DateTime joinTime, playListLastInteractTime;
        TimeSpan interactCoolDown = TimeSpan.FromSeconds(5);
        bool afterFirstRun;
        int initKey, playbackStateKey, enqueueKey;

        void OnEnable() {
            if (Utilities.IsValid(playbackControlsAnimator)) {
                if (!afterFirstRun) {
                    initKey = Animator.StringToHash("Init");
                    playbackStateKey = Animator.StringToHash("PlaybackState");
                    enqueueKey = Animator.StringToHash("Enqueue");
                }
                playbackControlsAnimator.SetTrigger(initKey);
            }
            if (afterFirstRun) return;
            afterFirstRun = true;
            joinTime = DateTime.UtcNow;
            if (Utilities.IsValid(luminanceSlider) && !string.IsNullOrEmpty(luminancePropertyName)) {
                luminancePropertyId = VRCShader.PropertyToID(luminancePropertyName);
                _OnScreenSharedPropertiesChanged();
            }
            InitPlayQueueList();
            InitPlayerSelect();
            if (Utilities.IsValid(playNextIndicator)) playNextIndicator.SetActive(false);
            InitShiftControl();
            _OnUIUpdate();
            _OnVolumeChange();
            _OnSyncOffsetChange();
            _OnSpeedChange();
            UpdatePlayerText();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _Play() {
            if (Utilities.IsValid(handler))
                handler._Play();
            else
                core.Play();
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _Pause() {
            if (Utilities.IsValid(handler))
                handler._Pause();
            else
                core.Pause();
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _Stop() {
            if (Utilities.IsValid(handler))
                handler._Stop();
            else
                core.Stop();
            if (!string.IsNullOrEmpty(enqueueCountFormat))
                SetText(enqueueCountText, enqueueCountTMPro, string.Format(enqueueCountFormat, 0));
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _Skip() {
            if (!Utilities.IsValid(handler)) return;
            handler._Skip();
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _RepeatOff() {
            if (Utilities.IsValid(handler))
                handler.NoRepeat();
            else
                core.Loop = false;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _RepeatOne() {
            if (Utilities.IsValid(handler))
                handler.RepeatOne = true;
            else
                core.Loop = true;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _RepeatAll() {
            if (Utilities.IsValid(handler))
                handler.RepeatAll = true;
            else
                core.Loop = true;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _ShuffleOff() {
            if (Utilities.IsValid(handler))
                handler.Shuffle = false;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _ShuffleOn() {
            if (Utilities.IsValid(handler))
                handler.Shuffle = true;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _LocalSync() {
            if (Utilities.IsValid(handler))
                handler._LocalSync();
            else
                core.LocalSync();
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _GlobalSync() {
            if (Utilities.IsValid(handler))
                handler._GlobalSync();
            else
                core.GlobalSync();
            _InputCancelClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnVolumeSlide() {
            core.Volume = volumeSlider.value;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnMute() {
            core.Muted = !core.Muted;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnVolumeChange() {
            if (!afterFirstRun) return;
            if (Utilities.IsValid(volumeSlider))
                volumeSlider.SetValueWithoutNotify(core.Volume);
            if (Utilities.IsValid(muteButtonObject) && Utilities.IsValid(unmuteButtonObject)) {
                var muted = core.Muted;
                muteButtonObject.SetActive(!muted);
                unmuteButtonObject.SetActive(muted);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnLanguageChanged() {
            if (!afterFirstRun) return;
            _OnUIUpdate();
            _OnSyncOffsetChange();
            if (Utilities.IsValid(handler)) {
                bool hasQueueList = handler.HasQueueList;
                bool hasHistory = handler.HistorySize > 0;
                if ((hasQueueList || hasHistory) && Utilities.IsValid(playListNames)) {
                    int i = 0;
                    if (hasHistory) playListNames[i++] = languageManager.GetLocale("PlaybackHistory");
                    if (hasQueueList) playListNames[i++] = languageManager.GetLocale("QueueList");
                    if (Utilities.IsValid(playListScrollView)) playListScrollView.EntryNames = playListNames;
                }
            }
            UpdatePlayerText();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnUIUpdate() {
            if (!afterFirstRun) return;
            bool hasHandler = Utilities.IsValid(handler);
            bool unlocked = !hasHandler || !handler.Locked;
            bool canPlay = false;
            bool canPause = false;
            bool canStop = false;
            bool canLocalSync = false;
            bool canSeek = false;
            int state = core.State;
            switch (state) {
                case 0: // Idle
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(true);
                    SetStatusEnabled(true);
                    SetLocalizedText(statusText, statusTMPro, "VVMW_Name");
                    SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                    SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                    break;
                case 1: // Loading
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(true);
                    SetStatusEnabled(true);
                    SetLocalizedText(statusText, statusTMPro, "Loading");
                    SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                    SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                    canStop = unlocked;
                    break;
                case 2: // Error
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(true);
                    if (!Utilities.IsValid(statusText) && !Utilities.IsValid(statusTMPro)) break;
                    SetStatusEnabled(true);
                    var errorCode = core.LastError;
                    switch (errorCode) {
                        case VideoError.InvalidURL: SetLocalizedText(statusText, statusTMPro, "InvalidURL"); break;
                        case VideoError.AccessDenied: SetLocalizedText(statusText, statusTMPro, core.IsTrusted ? "AccessDenied" : "AccessDeniedUntrusted"); break;
                        case VideoError.PlayerError: SetLocalizedText(statusText, statusTMPro, "PlayerError"); break;
                        case VideoError.RateLimited: SetLocalizedText(statusText, statusTMPro, "RateLimited"); break;
                        default: SetText(statusText, statusTMPro, string.Format(languageManager.GetLocale("Unknown"), (int)errorCode)); break;
                    }
                    SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                    SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                    canStop = unlocked;
                    break;
                case 3: // Ready
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(true);
                    if (Utilities.IsValid(statusText) || Utilities.IsValid(statusTMPro)) {
                        SetStatusEnabled(true);
                        SetLocalizedText(statusText, statusTMPro, "Ready");
                    }
                    if (Utilities.IsValid(progressSlider)) {
                        progressSlider.SetValueWithoutNotify(1);
                        progressSlider.interactable = false;
                    }
                    canPlay = unlocked;
                    break;
                case 4: // Playing
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(false);
                    SetStatusEnabled(false);
                    canPause = unlocked && !core.IsStatic;
                    canStop = unlocked;
                    canSeek = true;
                    break;
                case 5: // Paused
                    if (Utilities.IsValid(idleScreenRoot)) idleScreenRoot.SetActive(false);
                    SetStatusEnabled(false);
                    canPlay = unlocked && !core.IsStatic;
                    canStop = unlocked;
                    canSeek = true;
                    break;
            }
            if (Utilities.IsValid(playbackControlsAnimator)) playbackControlsAnimator.SetInteger(playbackStateKey, state);
            if (Utilities.IsValid(reloadButton)) {
                var localUrl = core.Url;
                canLocalSync = !VRCUrl.IsNullOrEmpty(localUrl);
            }
            if (Utilities.IsValid(playButtonObject)) playButtonObject.SetActive(canPlay);
            if (Utilities.IsValid(pauseButtonObject)) pauseButtonObject.SetActive(canPause);
            if (Utilities.IsValid(stopButtonObject)) stopButtonObject.SetActive(canStop);
            if (Utilities.IsValid(reloadButtonObject)) reloadButtonObject.SetActive(canLocalSync);
            if (Utilities.IsValid(progressSlider)) {
                if (canSeek) {
                    UpdateProgressOnce();
                    if (!hasUpdate) {
                        hasUpdate = true;
                        _UpdateProgress();
                    } else
                        progressSlider.interactable = unlocked;
                } else {
                    progressSlider.SetValueWithoutNotify(1);
                    progressSlider.interactable = false;
                }
            }
            if (wasUnlocked != unlocked || !hasUnlockInit) {
                hasUnlockInit = true;
                wasUnlocked = unlocked;
                if (Utilities.IsValid(queueListScrollView)) queueListScrollView.CanInteract = unlocked;
                if (Utilities.IsValid(playListScrollView)) playListScrollView.CanInteract = unlocked;
                if (Utilities.IsValid(repeatOffButton)) repeatOffButton.interactable = unlocked;
                if (Utilities.IsValid(repeatOneButton)) repeatOneButton.interactable = unlocked;
                if (Utilities.IsValid(repeatAllButton)) repeatAllButton.interactable = unlocked;
                if (Utilities.IsValid(shuffleOnButton)) shuffleOnButton.interactable = unlocked;
                if (Utilities.IsValid(playNextButton)) playNextButton.interactable = unlocked;
                if (Utilities.IsValid(playListTogglePanelButton)) playListTogglePanelButton.interactable = unlocked && Utilities.IsValid(playListNames) && playListNames.Length > 1;
                if (Utilities.IsValid(urlInput)) {
                    urlInput.interactable = unlocked;
                    if (!unlocked) urlInput.SetUrl(VRCUrl.Empty);
                }
                if (Utilities.IsValid(altUrlInput)) {
                    altUrlInput.interactable = unlocked;
                    if (!unlocked) altUrlInput.SetUrl(VRCUrl.Empty);
                }
                InitShiftControl();
            }
            if (hasHandler) {
                bool isRepeatOne = handler.RepeatOne;
                bool isRepeatAll = handler.RepeatAll;
                bool isShuffle = handler.Shuffle;
                if (Utilities.IsValid(repeatOffButtonObject)) repeatOffButtonObject.SetActive(!isRepeatOne && !isRepeatAll);
                if (Utilities.IsValid(repeatOneButtonObject)) repeatOneButtonObject.SetActive(isRepeatOne);
                if (Utilities.IsValid(repeatAllButtonObject)) repeatAllButtonObject.SetActive(isRepeatAll);
                if (Utilities.IsValid(shuffleOffButton)) {
                    shuffleOffButtonObject.SetActive(!isShuffle);
                    shuffleOffButton.interactable = unlocked;
                }
                if (Utilities.IsValid(shuffleOnButtonObject)) shuffleOnButtonObject.SetActive(isShuffle);
                UpdatePlayList();
                bool willPlayNext = handler.PlayListIndex == 0 && handler.HasQueueList && (core.IsReady || core.IsLoading || handler.QueueUrls.Length > 0);
                if (Utilities.IsValid(urlInputConfirmButton) && Utilities.IsValid(enforcePlayImmediatelyButton))
                    urlInputConfirmButtonObject.SetActive(willPlayNext);
                SetLocalizedText(queueModeText, queueModeTMPro, willPlayNext ? "QueueModeNext" : "QueueModeInstant");
            } else {
                bool isRepeatOne = core.Loop;
                if (Utilities.IsValid(repeatOffButtonObject)) repeatOffButtonObject.SetActive(!isRepeatOne);
                if (Utilities.IsValid(repeatOneButtonObject)) repeatOneButtonObject.SetActive(isRepeatOne);
                if (Utilities.IsValid(repeatAllButtonObject)) repeatAllButtonObject.SetActive(false);
                if (Utilities.IsValid(shuffleOffButtonObject)) {
                    shuffleOffButtonObject.SetActive(true);
                    shuffleOffButton.interactable = false;
                }
                if (Utilities.IsValid(shuffleOnButton)) shuffleOnButtonObject.SetActive(false);
                SetLocalizedText(queueModeText, queueModeTMPro, "QueueModeInstant");
            }
            if (Utilities.IsValid(speedDownLButton)) speedDownLButton.interactable = unlocked;
            if (Utilities.IsValid(speedDownSButton)) speedDownSButton.interactable = unlocked;
            if (Utilities.IsValid(speedUpSButton)) speedUpSButton.interactable = unlocked;
            if (Utilities.IsValid(speedUpLButton)) speedUpLButton.interactable = unlocked;
            if (Utilities.IsValid(speedResetButton)) speedResetButton.interactable = unlocked;
            if (Utilities.IsValid(performanceModeToggle)) {
                performanceModeToggle.interactable = unlocked;
                _OnPerformerChange();
            }
        }

        void SetLocalizedText(Text text, TextMeshProUGUI tmp, string locale) {
            if (!Utilities.IsValid(text) && !Utilities.IsValid(tmp)) return;
            SetText(text, tmp, languageManager.GetLocale(locale));
        }

        void SetText(Text text, TextMeshProUGUI tmp, string content) {
            if (Utilities.IsValid(text)) text.text = content;
            if (Utilities.IsValid(tmp)) tmp.text = content;
        }

        void SetStatusEnabled(bool enabled) {
            if (!Utilities.IsValid(timeContainer) || (!Utilities.IsValid(statusText) && !Utilities.IsValid(statusTMPro))) return;
            timeContainer.SetActive(!enabled);
            if (Utilities.IsValid(statusText)) statusText.enabled = enabled;
            if (Utilities.IsValid(statusTMPro)) statusTMPro.enabled = enabled;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnLuminanceSliderChanged() => core.SetScreenFloatExtra(luminancePropertyId, luminanceSlider.value);

#if COMPILER_UDONSHARP
        public
#endif
        void _OnScreenSharedPropertiesChanged() {
            if (!Utilities.IsValid(luminanceSlider)) return;
            luminanceSlider.SetValueWithoutNotify(core.GetScreenFloatExtra(luminancePropertyId));
        }

        #region Core Callbacks
#if COMPILER_UDONSHARP
        public override void OnVideoReady() => _OnUIUpdate();
        public override void OnVideoStart() => _OnUIUpdate();
        public override void OnVideoPlay() => _OnUIUpdate();
        public override void OnVideoPause() => _OnUIUpdate();
        public override void OnVideoEnd() => _OnUIUpdate();
        public void _OnVideoError() => _OnUIUpdate();
        public void _OnVideoBeginLoad() => _OnUIUpdate();
#endif
        #endregion
    }

#if !COMPILER_UDONSHARP
    public partial class UIHandler : IVizVidCompoonent {
        Core IVizVidCompoonent.Core {
            get {
                if (handler) return handler.core;
                return core;
            }
        }
    }
#endif
}