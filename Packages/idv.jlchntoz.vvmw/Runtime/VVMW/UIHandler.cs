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
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core"), Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        ), BindUdonSharpEvent]
        Core core;
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
        [SerializeField, LocalizedLabel] GameObject videoPlayerSelectButtonTemplate;
        [SerializeField, LocalizedLabel] GameObject videoPlayerSelectRoot, videoPlayerSelectPanel;
        [BindEvent(nameof(Button.onClick), nameof(_VideoPlayerSelect))]
        [SerializeField, LocalizedLabel] Button videoPlayerSelectButton;
        [BindEvent(nameof(Button.onClick), nameof(_InputCancelClick))]
        [SerializeField, LocalizedLabel] Button cancelButton;
        [BindEvent(nameof(Button.onClick), nameof(_InputConfirmClick))]
        [SerializeField, LocalizedLabel] Button urlInputConfirmButton;
        [TMProMigratable(nameof(selectdPlayerTMPro))]
        [SerializeField, LocalizedLabel] Text selectdPlayerText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI selectdPlayerTMPro;
        [TMProMigratable(nameof(queueModeTMPro))]
        [SerializeField, LocalizedLabel] Text queueModeText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI queueModeTMPro;
        [SerializeField, LocalizedLabel] GameObject otherObjectUnderUrlInput;

        [LocalizedHeader("HEADER:Playback_Controls")]
        [SerializeField, LocalizedLabel] Animator playbackControlsAnimator;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField, LocalizedLabel] Button playButton;
        [BindEvent(nameof(Button.onClick), nameof(_Pause))]
        [SerializeField, LocalizedLabel] Button pauseButton;
        [BindEvent(nameof(Button.onClick), nameof(_Stop))]
        [SerializeField, LocalizedLabel] Button stopButton;
        [BindEvent(nameof(Button.onClick), nameof(_LocalSync))]
        [SerializeField, LocalizedLabel] Button reloadButton;
        [BindEvent(nameof(Button.onClick), nameof(_GlobalSync))]
        [SerializeField, LocalizedLabel] Button globalReloadButton;
        [BindEvent(nameof(Button.onClick), nameof(_Skip))]
        [SerializeField, LocalizedLabel] Button playNextButton;
        [TMProMigratable(nameof(enqueueCountTMPro))]
        [SerializeField, LocalizedLabel] Text enqueueCountText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI enqueueCountTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOne))]
        [SerializeField, LocalizedLabel] Button repeatOffButton;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatAll))]
        [SerializeField, LocalizedLabel] Button repeatOneButton;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOff))]
        [FormerlySerializedAs("RepeatAllButton")]
        [SerializeField, LocalizedLabel] Button repeatAllButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOn))]
        [SerializeField, LocalizedLabel] Button shuffleOffButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOff))]
        [SerializeField, LocalizedLabel] Button shuffleOnButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_PlayListToggle))]
        [SerializeField, LocalizedLabel] Toggle playlistToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnSeek))]
        [SerializeField, LocalizedLabel] Slider progressSlider;
        [TMProMigratable(nameof(statusTMPro))]
        [SerializeField, LocalizedLabel] Text statusText;
        [TMProMigratable(nameof(timeTMPro))]
        [SerializeField, LocalizedLabel] Text timeText;
        [TMProMigratable(nameof(durationTMPro))]
        [SerializeField, LocalizedLabel] Text durationText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI statusTMPro, timeTMPro, durationTMPro;
        [SerializeField, LocalizedLabel] GameObject timeContainer;

        [LocalizedHeader("HEADER:Volume_Control")]
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSlide))]
        [SerializeField, LocalizedLabel] Slider volumeSlider;
        [BindEvent(nameof(Button.onClick), nameof(_OnMute))]
        [SerializeField, LocalizedLabel] Button muteButton, unmuteButton;

        [LocalizedHeader("HEADER:Idle_Screen")]
        [SerializeField, LocalizedLabel] GameObject idleScreenRoot;

        [LocalizedHeader("HEADEAR:Queue_List_PlayList")]
        [SerializeField, LocalizedLabel] GameObject playListPanelRoot;
        [SerializeField, LocalizedLabel, BindUdonSharpEvent] PooledScrollView playListScrollView;
        [BindEvent(nameof(Button.onClick), nameof(_PlayListTogglePanel))]
        [SerializeField, LocalizedLabel] Button playListTogglePanelButton;
        [SerializeField, LocalizedLabel, BindUdonSharpEvent] PooledScrollView queueListScrollView;
        [SerializeField, LocalizedLabel] GameObject playNextIndicator;
        [TMProMigratable(nameof(selectedPlayListTMPro))]
        [SerializeField, LocalizedLabel] Text selectedPlayListText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI selectedPlayListTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_OnCurrentPlayListSelectClick))]
        [SerializeField, LocalizedLabel] Button currentPlayListButton;

        [LocalizedHeader("HEADER:Sync_Offset_Controls")]
        [SerializeField, LocalizedLabel] GameObject shiftControlsRoot;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBackL))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftBack100msButton")] Button shiftBackLButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBackS))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftBack50msButton")] Button shiftBackSButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForwardS))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftForward50msButton")] Button shiftForwardSButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForwardL))]
        [SerializeField, LocalizedLabel, FormerlySerializedAs("shiftForward100msButton")] Button shiftForwardLButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftReset))]
        [SerializeField, LocalizedLabel] Button shiftResetButton;
        [TMProMigratable(nameof(shiftOffsetTMPro))]
        [SerializeField, LocalizedLabel] Text shiftOffsetText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI shiftOffsetTMPro;

        [LocalizedHeader("HEADER:Speed_Adjustment_Controls")]
        [SerializeField, LocalizedLabel] GameObject speedControlsRoot;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedDownL))]
        [SerializeField, LocalizedLabel] Button speedDownLButton;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedDownS))]
        [SerializeField, LocalizedLabel] Button speedDownSButton;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedUpS))]
        [SerializeField, LocalizedLabel] Button speedUpSButton;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedUpL))]
        [SerializeField, LocalizedLabel] Button speedUpLButton;
        [BindEvent(nameof(Button.onClick), nameof(_SpeedReset))]
        [SerializeField, LocalizedLabel] Button speedResetButton;
        [TMProMigratable(nameof(speedOffsetTMPro))]
        [SerializeField, LocalizedLabel] Text speedOffsetText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI speedOffsetTMPro;

        [LocalizedHeader("HEADER:Screen_Controls")]
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnLuminanceSliderChanged))]
        [SerializeField, LocalizedLabel] Slider luminanceSlider;
        [SerializeField, LocalizedLabel] string luminancePropertyName = "_EmissionIntensity";
        int luminancePropertyId;

        bool hasUpdate, wasUnlocked, hasUnlockInit;
        byte selectedPlayer = 1;
        int interactTriggerId;
        DateTime joinTime, playListLastInteractTime;
        TimeSpan interactCoolDown = TimeSpan.FromSeconds(5);
        bool afterFirstRun;
        int initKey, playbackStateKey;

        void OnEnable() {
            if (Utilities.IsValid(playbackControlsAnimator)) {
                if (!afterFirstRun) {
                    initKey = Animator.StringToHash("Init");
                    playbackStateKey = Animator.StringToHash("PlaybackState");
                }
                playbackControlsAnimator.SetTrigger(initKey);
            }
            if (afterFirstRun) return;
            afterFirstRun = true;
            joinTime = DateTime.UtcNow;
            if (Utilities.IsValid(handler)) core = handler.core;
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
            if (Utilities.IsValid(muteButton) && Utilities.IsValid(unmuteButton)) {
                var muted = core.Muted;
                muteButton.gameObject.SetActive(!muted);
                unmuteButton.gameObject.SetActive(muted);
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
            if (Utilities.IsValid(playButton)) playButton.gameObject.SetActive(canPlay);
            if (Utilities.IsValid(pauseButton)) pauseButton.gameObject.SetActive(canPause);
            if (Utilities.IsValid(stopButton)) stopButton.gameObject.SetActive(canStop);
            if (Utilities.IsValid(reloadButton)) reloadButton.gameObject.SetActive(canLocalSync);
            if (Utilities.IsValid(progressSlider)) {
                if (canSeek) {
                    UpdateProgressOnce();
                    if (!hasUpdate) {
                        hasUpdate = true;
                        _UpdateProgress();
                    }
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
            }
            if (hasHandler) {
                bool isRepeatOne = handler.RepeatOne;
                bool isRepeatAll = handler.RepeatAll;
                bool isShuffle = handler.Shuffle;
                if (Utilities.IsValid(repeatOffButton)) repeatOffButton.gameObject.SetActive(!isRepeatOne && !isRepeatAll);
                if (Utilities.IsValid(repeatOneButton)) repeatOneButton.gameObject.SetActive(isRepeatOne);
                if (Utilities.IsValid(repeatAllButton)) repeatAllButton.gameObject.SetActive(isRepeatAll);
                if (Utilities.IsValid(shuffleOffButton)) {
                    shuffleOffButton.gameObject.SetActive(!isShuffle);
                    shuffleOffButton.interactable = unlocked;
                }
                if (Utilities.IsValid(shuffleOnButton)) shuffleOnButton.gameObject.SetActive(isShuffle);
                UpdatePlayList();
                SetLocalizedText(queueModeText, queueModeTMPro,
                    handler.PlayListIndex == 0 && handler.HasQueueList && (core.IsReady || core.IsLoading || handler.QueueUrls.Length > 0) ?
                    "QueueModeNext" : "QueueModeInstant"
                );
            } else {
                bool isRepeatOne = core.Loop;
                if (Utilities.IsValid(repeatOffButton)) repeatOffButton.gameObject.SetActive(!isRepeatOne);
                if (Utilities.IsValid(repeatOneButton)) repeatOneButton.gameObject.SetActive(isRepeatOne);
                if (Utilities.IsValid(repeatAllButton)) repeatAllButton.gameObject.SetActive(false);
                if (Utilities.IsValid(shuffleOffButton)) {
                    shuffleOffButton.gameObject.SetActive(true);
                    shuffleOffButton.interactable = false;
                }
                if (Utilities.IsValid(shuffleOnButton)) shuffleOnButton.gameObject.SetActive(false);
                SetLocalizedText(queueModeText, queueModeTMPro, "QueueModeInstant");
            }
            bool canChangeSpeed = unlocked && core.SupportSpeedAdjustment;
            if (Utilities.IsValid(speedDownLButton)) speedDownLButton.interactable = canChangeSpeed;
            if (Utilities.IsValid(speedDownSButton)) speedDownSButton.interactable = canChangeSpeed;
            if (Utilities.IsValid(speedUpSButton)) speedUpSButton.interactable = canChangeSpeed;
            if (Utilities.IsValid(speedUpLButton)) speedUpLButton.interactable = canChangeSpeed;
            if (Utilities.IsValid(speedResetButton)) speedResetButton.interactable = canChangeSpeed;
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
}