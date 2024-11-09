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
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/UI Handler")]
    [DefaultExecutionOrder(2)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#default-ui--screen-with-overlay")]
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
            if (playbackControlsAnimator != null) {
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
            if (luminanceSlider != null && !string.IsNullOrEmpty(luminancePropertyName)) {
                luminancePropertyId = VRCShader.PropertyToID(luminancePropertyName);
                _OnScreenSharedPropertiesChanged();
            }
            InitPlayQueueList();
            InitPlayerSelect();
            if (playNextIndicator != null) playNextIndicator.SetActive(false);
            InitShiftControl();
            _OnUIUpdate();
            _OnVolumeChange();
            _OnSyncOffsetChange();
            _OnSpeedChange();
            UpdatePlayerText();
        }

        public void _Play() {
            if (Utilities.IsValid(handler))
                handler._Play();
            else
                core.Play();
            _InputCancelClick();
        }

        public void _Pause() {
            if (Utilities.IsValid(handler))
                handler._Pause();
            else
                core.Pause();
            _InputCancelClick();
        }

        public void _Stop() {
            if (Utilities.IsValid(handler))
                handler._Stop();
            else
                core.Stop();
            SetText(enqueueCountText, enqueueCountTMPro, string.Format(enqueueCountFormat, 0));
            _InputCancelClick();
        }

        public void _Skip() {
            if (!Utilities.IsValid(handler)) return;
            handler._Skip();
            _InputCancelClick();
        }

        public void _RepeatOff() {
            if (Utilities.IsValid(handler))
                handler.NoRepeat();
            else
                core.Loop = false;
        }

        public void _RepeatOne() {
            if (Utilities.IsValid(handler))
                handler.RepeatOne = true;
            else
                core.Loop = true;
        }

        public void _RepeatAll() {
            if (Utilities.IsValid(handler))
                handler.RepeatAll = true;
            else
                core.Loop = true;
        }

        public void _ShuffleOff() {
            if (Utilities.IsValid(handler))
                handler.Shuffle = false;
        }

        public void _ShuffleOn() {
            if (Utilities.IsValid(handler))
                handler.Shuffle = true;
        }

        public void _LocalSync() {
            if (Utilities.IsValid(handler))
                handler._LocalSync();
            else
                core.LocalSync();
            _InputCancelClick();
        }

        public void _GlobalSync() {
            if (Utilities.IsValid(handler))
                handler._GlobalSync();
            else
                core.GlobalSync();
            _InputCancelClick();
        }

        public void _OnVolumeSlide() {
            core.Volume = volumeSlider.value;
        }

        public void _OnMute() {
            core.Muted = !core.Muted;
        }

        public void _OnVolumeChange() {
            if (!afterFirstRun) return;
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(core.Volume);
            if (muteButton != null && unmuteButton != null) {
                var muted = core.Muted;
                muteButton.gameObject.SetActive(!muted);
                unmuteButton.gameObject.SetActive(muted);
            }
        }

        public void _OnLanguageChanged() {
            if (!afterFirstRun) return;
            _OnUIUpdate();
            _OnSyncOffsetChange();
            if (Utilities.IsValid(handler)) {
                bool hasQueueList = handler.HasQueueList;
                bool hasHistory = handler.HistorySize > 0;
                if ((hasQueueList || hasHistory) && playListNames != null) {
                    int i = 0;
                    if (hasHistory) playListNames[i++] = languageManager.GetLocale("PlaybackHistory");
                    if (hasQueueList) playListNames[i++] = languageManager.GetLocale("QueueList");
                    if (playListScrollView != null) playListScrollView.EntryNames = playListNames;
                }
            }
            UpdatePlayerText();
        }

        public void _OnUIUpdate() {
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
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    SetStatusEnabled(true);
                    SetLocalizedText(statusText, statusTMPro, "VVMW_Name");
                    SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                    SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                    break;
                case 1: // Loading
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    SetStatusEnabled(true);
                    SetLocalizedText(statusText, statusTMPro, "Loading");
                    SetLocalizedText(durationText, durationTMPro, "TimeIdleFormat");
                    SetLocalizedText(timeText, timeTMPro, "TimeIdleFormat");
                    canStop = unlocked;
                    break;
                case 2: // Error
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText == null && statusTMPro == null) break;
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
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText != null || statusTMPro != null) {
                        SetStatusEnabled(true);
                        SetLocalizedText(statusText, statusTMPro, "Ready");
                    }
                    if (progressSlider != null) {
                        progressSlider.SetValueWithoutNotify(1);
                        progressSlider.interactable = false;
                    }
                    canPlay = unlocked;
                    break;
                case 4: // Playing
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(false);
                    SetStatusEnabled(false);
                    canPause = unlocked && !core.IsStatic;
                    canStop = unlocked;
                    canSeek = true;
                    break;
                case 5: // Paused
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(false);
                    SetStatusEnabled(false);
                    canPlay = unlocked && !core.IsStatic;
                    canStop = unlocked;
                    canSeek = true;
                    break;
            }
            if (playbackControlsAnimator != null) playbackControlsAnimator.SetInteger(playbackStateKey, state);
            if (reloadButton != null) {
                var localUrl = core.Url;
                canLocalSync = !VRCUrl.IsNullOrEmpty(localUrl);
            }
            if (playButton != null) playButton.gameObject.SetActive(canPlay);
            if (pauseButton != null) pauseButton.gameObject.SetActive(canPause);
            if (stopButton != null) stopButton.gameObject.SetActive(canStop);
            if (reloadButton != null) reloadButton.gameObject.SetActive(canLocalSync);
            if (progressSlider != null) {
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
                if (queueListScrollView != null) queueListScrollView.CanInteract = unlocked;
                if (playListScrollView != null) playListScrollView.CanInteract = unlocked;
                if (repeatOffButton != null) repeatOffButton.interactable = unlocked;
                if (repeatOneButton != null) repeatOneButton.interactable = unlocked;
                if (repeatAllButton != null) repeatAllButton.interactable = unlocked;
                if (shuffleOnButton != null) shuffleOnButton.interactable = unlocked;
                if (playNextButton != null) playNextButton.interactable = unlocked;
                if (playListTogglePanelButton != null) playListTogglePanelButton.interactable = unlocked && playListNames != null && playListNames.Length > 1;
                if (urlInput != null) {
                    urlInput.interactable = unlocked;
                    if (!unlocked) urlInput.SetUrl(VRCUrl.Empty);
                }
                if (altUrlInput != null) {
                    altUrlInput.interactable = unlocked;
                    if (!unlocked) altUrlInput.SetUrl(VRCUrl.Empty);
                }
            }
            if (hasHandler) {
                bool isRepeatOne = handler.RepeatOne;
                bool isRepeatAll = handler.RepeatAll;
                bool isShuffle = handler.Shuffle;
                if (repeatOffButton != null) repeatOffButton.gameObject.SetActive(!isRepeatOne && !isRepeatAll);
                if (repeatOneButton != null) repeatOneButton.gameObject.SetActive(isRepeatOne);
                if (repeatAllButton != null) repeatAllButton.gameObject.SetActive(isRepeatAll);
                if (shuffleOffButton != null) {
                    shuffleOffButton.gameObject.SetActive(!isShuffle);
                    shuffleOffButton.interactable = unlocked;
                }
                if (shuffleOnButton != null) shuffleOnButton.gameObject.SetActive(isShuffle);
                UpdatePlayList();
                SetLocalizedText(queueModeText, queueModeTMPro,
                    handler.PlayListIndex == 0 && handler.HasQueueList && (core.IsReady || core.IsLoading || handler.QueueUrls.Length > 0) ?
                    "QueueModeNext" : "QueueModeInstant"
                );
            } else {
                bool isRepeatOne = core.Loop;
                if (repeatOffButton != null) repeatOffButton.gameObject.SetActive(!isRepeatOne);
                if (repeatOneButton != null) repeatOneButton.gameObject.SetActive(isRepeatOne);
                if (repeatAllButton != null) repeatAllButton.gameObject.SetActive(false);
                if (shuffleOffButton != null) {
                    shuffleOffButton.gameObject.SetActive(true);
                    shuffleOffButton.interactable = false;
                }
                if (shuffleOnButton != null) shuffleOnButton.gameObject.SetActive(false);
                SetLocalizedText(queueModeText, queueModeTMPro, "QueueModeInstant");
            }
            bool canChangeSpeed = unlocked && core.SupportSpeedAdjustment;
            if (speedDownLButton != null) speedDownLButton.interactable = canChangeSpeed;
            if (speedDownSButton != null) speedDownSButton.interactable = canChangeSpeed;
            if (speedUpSButton != null) speedUpSButton.interactable = canChangeSpeed;
            if (speedUpLButton != null) speedUpLButton.interactable = canChangeSpeed;
            if (speedResetButton != null) speedResetButton.interactable = canChangeSpeed;
        }

        void SetLocalizedText(Text text, TextMeshProUGUI tmp, string locale) {
            if (text == null && tmp == null) return;
            SetText(text, tmp, languageManager.GetLocale(locale));
        }

        void SetText(Text text, TextMeshProUGUI tmp, string content) {
            if (text != null) text.text = content;
            if (tmp != null) tmp.text = content;
        }

        void SetStatusEnabled(bool enabled) {
            if (timeContainer == null || (statusText == null && statusTMPro == null)) return;
            timeContainer.SetActive(!enabled);
            if (statusText != null) statusText.enabled = enabled;
            if (statusTMPro != null) statusTMPro.enabled = enabled;
        }

        public void _OnLuminanceSliderChanged() => core.SetScreenFloatExtra(luminancePropertyId, luminanceSlider.value);

        public void _OnScreenSharedPropertiesChanged() {
            if (luminanceSlider == null) return;
            luminanceSlider.SetValueWithoutNotify(core.GetScreenFloatExtra(luminancePropertyId));
        }

        #region Core Callbacks
        public override void OnVideoReady() => _OnUIUpdate();
        public override void OnVideoStart() => _OnUIUpdate();
        public override void OnVideoPlay() => _OnUIUpdate();
        public override void OnVideoPause() => _OnUIUpdate();
        public override void OnVideoEnd() => _OnUIUpdate();
        public void _OnVideoError() => _OnUIUpdate();
        public void _OnVideoBeginLoad() => _OnUIUpdate();
        #endregion
    }
}