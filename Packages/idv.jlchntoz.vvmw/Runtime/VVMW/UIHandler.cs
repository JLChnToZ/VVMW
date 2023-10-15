using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [DisallowMultipleComponent]
    public class UIHandler : UdonSharpBehaviour {
        [Header("Main Reference")]
        [SerializeField, Locatable] Core core;
        [Locatable] public FrontendHandler handler;
        [SerializeField] LanguageManager languageManager;

        [Header("URL Input")]
        [BindEvent(nameof(VRCUrlInputField.onValueChanged), nameof(_OnURLChanged))]
        [BindEvent(nameof(VRCUrlInputField.onEndEdit), nameof(_OnURLEndEdit))]
        [SerializeField] VRCUrlInputField urlInput; 
        [SerializeField] GameObject videoPlayerSelectButtonTemplate;
        [SerializeField] GameObject videoPlayerSelectRoot, videoPlayerSelectPanel;
        [BindEvent(nameof(Button.onClick), nameof(_VideoPlayerSelect))]
        [SerializeField] Button videoPlayerSelectButton;
        [BindEvent(nameof(Button.onClick), nameof(_InputCancelClick))]
        [SerializeField] Button cancelButton;
        [BindEvent(nameof(Button.onClick), nameof(_InputConfirmClick))]
        [SerializeField] Button urlInputConfirmButton;
        [SerializeField] Text selectdPlayerText;
        [SerializeField] Text queueModeText;
        [SerializeField] GameObject otherObjectUnderUrlInput;

        [Header("Playback Controls")]
        [SerializeField] Animator playbackControlsAnimator;
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField] Button playButton;
        [BindEvent(nameof(Button.onClick), nameof(_Pause))]
        [SerializeField] Button pauseButton;
        [BindEvent(nameof(Button.onClick), nameof(_Stop))]
        [SerializeField] Button stopButton;
        [BindEvent(nameof(Button.onClick), nameof(_LocalSync))]
        [SerializeField] Button reloadButton;
        [BindEvent(nameof(Button.onClick), nameof(_GlobalSync))]
        [SerializeField] Button globalReloadButton;
        [BindEvent(nameof(Button.onClick), nameof(_Skip))]
        [SerializeField] Button playNextButton;
        [SerializeField] Text enqueueCountText;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOne))]
        [SerializeField] Button repeatOffButton;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatAll))]
        [SerializeField] Button repeatOneButton;
        [BindEvent(nameof(Button.onClick), nameof(_RepeatOff))]
        [FormerlySerializedAs("RepeatAllButton")]
        [SerializeField] Button repeatAllButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOn))]
        [SerializeField] Button shuffleOffButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShuffleOff))]
        [SerializeField] Button shuffleOnButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_PlayListToggle))]
        [SerializeField] Toggle playlistToggle;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnSeek))]
        [SerializeField] Slider progressSlider;
        [SerializeField] Text statusText, timeText, durationText;
        [SerializeField] GameObject timeContainer;

        [Header("Volume Control")]
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSlide))]
        [SerializeField] Slider volumeSlider;
        [BindEvent(nameof(Button.onClick), nameof(_OnMute))]
        [SerializeField] Button muteButton, unmuteButton;

        [Header("Idle Screen")]
        [SerializeField] GameObject idleScreenRoot;

        [Header("Queue List / Play List")]
        [SerializeField] GameObject playListPanelRoot;
        [SerializeField] PooledScrollView playListScrollView;
        [BindEvent(nameof(Button.onClick), nameof(_PlayListTogglePanel))]
        [SerializeField] Button playListTogglePanelButton;
        [SerializeField] PooledScrollView queueListScrollView;
        [SerializeField] GameObject playNextIndicator;
        [SerializeField] Text selectedPlayListText;
        [BindEvent(nameof(Button.onClick), nameof(_OnCurrentPlayListSelectClick))]
        [SerializeField] Button currentPlayListButton;

        [Header("Sync Offset Controls")]
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBack100ms))]
        [SerializeField] Button shiftBack100msButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftBack50ms))]
        [SerializeField] Button shiftBack50msButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForward50ms))]
        [SerializeField] Button shiftForward50msButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftForward100ms))]
        [SerializeField] Button shiftForward100msButton;
        [BindEvent(nameof(Button.onClick), nameof(_ShiftReset))]
        [SerializeField] Button shiftResetButton;
        [SerializeField] Text shiftOffsetText;

        string[] playListNames;
        ButtonEntry[] videoPlayerSelectButtons;
        [NonSerialized] public byte loadWithIndex;
        int lastSelectedPlayListIndex, lastPlayingIndex;
        int lastDisplayCount;
        bool hasUpdate, wasUnlocked, hasUnlockInit, playListUpdateRequired;
        string enqueueCountFormat;
        byte selectedPlayer = 1;
        int interactTriggerId;
        DateTime joinTime, playListLastInteractTime;
        TimeSpan interactCoolDown = TimeSpan.FromSeconds(5);

        int SelectedPlayListIndex {
            get {
                if (playListScrollView == null) return 0;
                int selectedIndex = playListScrollView.SelectedIndex;
                if (handler != null && !handler.HasQueueList) selectedIndex++;
                return selectedIndex;
            }
            set {
                if (playListScrollView == null) return;
                if (handler != null && !handler.HasQueueList) value--;
                playListScrollView.SelectedIndex = value;
            }
        }

        void Start() {
            joinTime = DateTime.UtcNow;
            var hasHandler = Utilities.IsValid(handler);
            if (hasHandler) core = handler.core;
            if (enqueueCountText != null) {
                enqueueCountFormat = enqueueCountText.text;
                enqueueCountText.text = string.Format(enqueueCountFormat, 0);
            }
            if (playListPanelRoot != null) playListPanelRoot.SetActive(true);
            if (playListScrollView != null) {
                playListNames = hasHandler ? handler.PlayListTitles : null;
                if (playListNames != null) {
                    if (handler.HasQueueList) {
                        var temp = new string[playListNames.Length + 1];
                        temp[0] = languageManager.GetLocale("QueueList");
                        Array.Copy(playListNames, 0, temp, 1, playListNames.Length);
                        playListNames = temp;
                    }
                } else if (playListNames == null)
                    playListNames = new [] { languageManager.GetLocale("QueueList") };
                bool hasPlayList = playListNames.Length > 1;
                playListScrollView.EventPrefix = "_OnPlayList";
                playListScrollView.CanDelete = false;
                playListScrollView.EntryNames = playListNames;
                playListScrollView._AddListener(this);
                SelectedPlayListIndex = handler.PlayListIndex;
                if (playListTogglePanelButton != null)
                    playListScrollView.gameObject.SetActive(false);
                else
                    playListScrollView.gameObject.SetActive(hasPlayList);
            }
            if (queueListScrollView != null) {
                queueListScrollView.EventPrefix = "_OnQueueList";
                queueListScrollView._AddListener(this);
                queueListScrollView.gameObject.SetActive(hasHandler);
            }
            if (videoPlayerSelectButtonTemplate != null) {
                var templateTransform = videoPlayerSelectButtonTemplate.transform;
                var parent = videoPlayerSelectRoot.transform;
                var sibling = templateTransform.GetSiblingIndex() + 1;
                var videoPlayerNames = core.PlayerNames;
                videoPlayerSelectButtons = new ButtonEntry[videoPlayerNames.Length];
                for (int i = 0; i < videoPlayerNames.Length; i++) {
                    var button = Instantiate(videoPlayerSelectButtonTemplate);
                    button.SetActive(true);
                    var buttonTransform = button.transform;
                    buttonTransform.SetParent(parent, false);
                    buttonTransform.SetSiblingIndex(sibling + i);
                    var buttonControl = button.GetComponent<ButtonEntry>();
                    buttonControl.LanguageManager = languageManager;
                    buttonControl.Key = videoPlayerNames[i];
                    buttonControl.callbackTarget = this;    
                    buttonControl.callbackEventName = nameof(_LoadPlayerClick);
                    buttonControl.callbackVariableName = nameof(loadWithIndex);
                    buttonControl.callbackUserData = (byte)(i + 1);
                    videoPlayerSelectButtons[i] = buttonControl;
                }
                videoPlayerSelectButtonTemplate.SetActive(false);
            }
            if (playNextIndicator != null) playNextIndicator.SetActive(false);
            bool isSynced = core.IsSynced;
            if (shiftBack100msButton != null) shiftBack100msButton.gameObject.SetActive(isSynced);
            if (shiftBack50msButton != null) shiftBack50msButton.gameObject.SetActive(isSynced);
            if (shiftForward50msButton != null) shiftForward50msButton.gameObject.SetActive(isSynced);
            if (shiftForward100msButton != null) shiftForward100msButton.gameObject.SetActive(isSynced);
            if (shiftResetButton != null) shiftResetButton.gameObject.SetActive(isSynced);
            if (shiftOffsetText != null) shiftOffsetText.gameObject.SetActive(isSynced);
            if (hasHandler) handler._AddListener(this);
            else core._AddListener(this);
            languageManager._AddListener(this);
            _OnUIUpdate();
            _OnVolumeChange();
            _OnSyncOffsetChange();
            UpdatePlayerText();
        }

        void OnEnable() {
            if (playbackControlsAnimator != null) playbackControlsAnimator.SetTrigger("Init");
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
            if (enqueueCountText != null) enqueueCountText.text = string.Format(enqueueCountFormat, 0);
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

        public void _OnSeek() {
            core.Progress = progressSlider.value;
        }

        public void _OnVolumeSlide() {
            core.Volume = volumeSlider.value;
        }

        public void _OnMute() {
            core.Muted = !core.Muted;
        }

        public void _OnVolumeChange() {
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(core.Volume);
            if (muteButton != null && unmuteButton != null) {
                var muted = core.Muted;
                muteButton.gameObject.SetActive(!muted);
                unmuteButton.gameObject.SetActive(muted);
            }
        }

        public void _OnURLChanged() {
            bool isEmpty =  string.IsNullOrEmpty(urlInput.textComponent.text);
            if (otherObjectUnderUrlInput != null) otherObjectUnderUrlInput.SetActive(isEmpty);
            if (videoPlayerSelectPanel != null) videoPlayerSelectPanel.SetActive(!isEmpty);
        }

        public void _OnURLEndEdit() {
            _OnURLChanged();
            if (urlInputConfirmButton == null) _InputConfirmClick();
        }

        public void _InputConfirmClick() {
            var url = urlInput.GetUrl();
            if (Utilities.IsValid(url) && !string.IsNullOrEmpty(url.Get())) {
                playListLastInteractTime = joinTime;
                if (Utilities.IsValid(handler)) {
                    handler.PlayUrl(url, selectedPlayer);
                    if (queueListScrollView != null)
                        SelectedPlayListIndex = handler.PlayListIndex;
                    UpdatePlayList();
                } else
                    core.PlayUrl(url, selectedPlayer);
                _InputCancelClick();
            }
        }
        
        public void _VideoPlayerSelect() {
            if (videoPlayerSelectRoot == null) return;
            videoPlayerSelectRoot.SetActive(!videoPlayerSelectRoot.activeSelf);
        }

        public void _InputCancelClick() {
            urlInput.SetUrl(VRCUrl.Empty);
            _OnUIUpdate();
            _OnURLChanged();
        }

        public void _PlayListTogglePanel() {
            if (playListScrollView == null) return;
            var playListGameObject = playListScrollView.gameObject;
            playListGameObject.SetActive(!playListGameObject.activeSelf);
        }

        public void _PlayListToggle() {
            if (playListScrollView == null) return;
            if (playlistToggle.isOn) {
                playListPanelRoot.SetActive(true);
                if (Utilities.IsValid(handler)) {
                    if (queueListScrollView != null)
                        queueListScrollView.SelectedIndex = handler.PlayListIndex;
                    playListLastInteractTime = joinTime;
                }
            } else {
                playListLastInteractTime = DateTime.UtcNow;
                playListPanelRoot.SetActive(false);
            }
        }

        public void _OnLanguageChanged() {
            _OnUIUpdate();
            _OnSyncOffsetChange();
            if (Utilities.IsValid(handler) && handler.HasQueueList && playListNames != null) {
                playListNames[0] = languageManager.GetLocale("QueueList");
                if (playListScrollView != null) playListScrollView.EntryNames = playListNames;
            }
            UpdatePlayerText();
        }

        public void _LoadPlayerClick() {
            selectedPlayer = loadWithIndex;
            UpdatePlayerText();
            if (videoPlayerSelectRoot != null) videoPlayerSelectRoot.SetActive(false);
        }

        void UpdatePlayerText() {
            selectdPlayerText.text = videoPlayerSelectButtons[selectedPlayer - 1].Text;
        }

        public void _OnUIUpdate() {
            bool hasHandler = Utilities.IsValid(handler);
            bool unlocked = !hasHandler || !handler.Locked;
            bool canPlay = false;
            bool canPause = false;
            bool canStop = false;
            bool canLocalSync = false;
            bool canSeek = false;
            switch (core.State) {
                case 0: // Idle
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText != null) {
                        if (timeContainer != null) {
                            timeContainer.SetActive(false);
                            statusText.enabled = true;
                        }
                        statusText.text = languageManager.GetLocale("VVMW_Name");
                    }
                    if (durationText != null) durationText.text = languageManager.GetLocale("TimeIdleFormat");
                    if (timeText != null) timeText.text = languageManager.GetLocale("TimeIdleFormat");
                    break;
                case 1: // Loading
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText != null) {
                        if (timeContainer != null) {
                            timeContainer.SetActive(false);
                            statusText.enabled = true;
                        }
                        statusText.text = languageManager.GetLocale("Loading");
                    }
                    if (durationText != null) durationText.text = languageManager.GetLocale("TimeIdleFormat");
                    if (timeText != null) timeText.text = languageManager.GetLocale("TimeIdleFormat");
                    canStop = unlocked;
                    break;
                case 2: // Error
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText == null) break;
                    if (timeContainer != null) {
                        statusText.enabled = true;
                        timeContainer.SetActive(false);
                    }
                    var errorCode = core.LastError;
                    switch (errorCode) {
                        case VideoError.InvalidURL: statusText.text = languageManager.GetLocale("InvalidURL"); break;
                        case VideoError.AccessDenied: statusText.text = languageManager.GetLocale(core.IsTrusted ? "AccessDenied" : "AccessDeniedUntrusted"); break;
                        case VideoError.PlayerError: statusText.text = languageManager.GetLocale("PlayerError"); break;
                        case VideoError.RateLimited: statusText.text = languageManager.GetLocale("RateLimited"); break;
                        default: statusText.text = string.Format(languageManager.GetLocale("Unknown"), (int)errorCode); break;
                    }
                    if (durationText != null) durationText.text = "";
                    if (timeText != null) timeText.text = "";
                    canStop = unlocked;
                    break;
                case 3: // Ready
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(true);
                    if (statusText != null) {
                        if (timeContainer != null) {
                            timeContainer.SetActive(false);
                            statusText.enabled = true;
                        }
                        statusText.text = languageManager.GetLocale("Ready");
                    }
                    if (progressSlider != null) {
                        progressSlider.SetValueWithoutNotify(1);
                        progressSlider.interactable = false;
                    }
                    canPlay = unlocked;
                    break;
                case 4: // Playing
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(false);
                    if (timeContainer != null && statusText != null) {
                        timeContainer.SetActive(true);
                        statusText.enabled = false;
                    }
                    canPause = unlocked;
                    canStop = unlocked;
                    canSeek = true;
                    break;
                case 5: // Paused
                    if (idleScreenRoot != null) idleScreenRoot.SetActive(false);
                    if (timeContainer != null && statusText != null) {
                        timeContainer.SetActive(true);
                        statusText.enabled = false;
                    }
                    canPlay = unlocked;
                    canStop = unlocked;
                    canSeek = true;
                    break;
            }
            if (reloadButton != null) {
                var localUrl = core.Url;
                canLocalSync = Utilities.IsValid(localUrl) && !localUrl.Equals(VRCUrl.Empty);
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
                if (playNextIndicator != null)
                    playNextIndicator.SetActive(!isShuffle && SelectedPlayListIndex == 0 && handler.PlayListIndex == 0 && handler.PendingCount > 0);
                if (queueModeText != null)
                    queueModeText.text = languageManager.GetLocale(
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
                if (queueModeText != null) queueModeText.text = languageManager.GetLocale("QueueModeInstant");
            }
        }

        public void _DeferUpdatePlayList() {
            if (playListUpdateRequired && !UpdatePlayList() && playListUpdateRequired)
                SendCustomEventDelayedFrames(nameof(_DeferUpdatePlayList), 0);
        }

        bool UpdatePlayList() {
            int playListIndex = handler.PlayListIndex;
            int playingIndex = handler.CurrentPlayingIndex;
            int displayCount, offset;
            int pendingCount = handler.PendingCount;
            VRCUrl[] queuedUrls = handler.QueueUrls, playListUrls = handler.PlayListUrls;
            string[] entryTitles = handler.PlayListEntryTitles, queuedTitles = handler.QueueTitles;
            int[] urlOffsets = handler.PlayListUrlOffsets;
            if (playListIndex > 0) {
                offset = urlOffsets[playListIndex - 1];
                displayCount = (playListIndex < urlOffsets.Length ? urlOffsets[playListIndex] : playListUrls.Length) - offset;
            } else {
                offset = 0;
                displayCount = queuedUrls.Length;
            }
            bool hasPending = pendingCount > 0;
            bool isEntryContainerInactive = queueListScrollView == null || !queueListScrollView.gameObject.activeInHierarchy;
            int selectedPlayListIndex = SelectedPlayListIndex;
            bool isNotCoolingDown = (DateTime.UtcNow - playListLastInteractTime) >= interactCoolDown;
            if (isEntryContainerInactive || isNotCoolingDown)
                SelectedPlayListIndex = selectedPlayListIndex = playListIndex;
            if (playNextButton != null) playNextButton.gameObject.SetActive(hasPending);
            if (currentPlayListButton != null) currentPlayListButton.gameObject.SetActive(hasPending);
            if (enqueueCountText != null)
                enqueueCountText.text = string.Format(enqueueCountFormat, pendingCount);
            if (selectedPlayListText != null)
                selectedPlayListText.text = selectedPlayListIndex > 0 ?
                    handler.PlayListTitles[selectedPlayListIndex - 1] :
                    languageManager.GetLocale("QueueList");
            bool shouldRefreshQueue = playListUpdateRequired || selectedPlayListIndex <= 0 || lastSelectedPlayListIndex != selectedPlayListIndex || lastPlayingIndex != playingIndex;
            lastSelectedPlayListIndex = selectedPlayListIndex;
            lastPlayingIndex = playingIndex;
            if (!shouldRefreshQueue || queueListScrollView == null)
                return false;
            if (isEntryContainerInactive) {
                if (!playListUpdateRequired) {
                    playListUpdateRequired = true;
                    SendCustomEventDelayedFrames(nameof(_DeferUpdatePlayList), 0);
                }
                return false;
            }
            playListUpdateRequired = false;
            if (selectedPlayListIndex != playListIndex) {
                if (selectedPlayListIndex > 0) {
                    offset = urlOffsets[selectedPlayListIndex - 1];
                    displayCount = (selectedPlayListIndex < urlOffsets.Length ? urlOffsets[selectedPlayListIndex] : playListUrls.Length) - offset;
                } else {
                    offset = 0;
                    displayCount = queuedUrls.Length;
                }
                playingIndex = -1;
            }
            if (selectedPlayListIndex == 0) {
                queueListScrollView.CanDelete = true;
                queueListScrollView.EntryNames = queuedTitles;
                queueListScrollView.SetIndexWithoutScroll(-1);
            } else {
                queueListScrollView.CanDelete = false;
                queueListScrollView.SetEntries(entryTitles, offset, displayCount);
                queueListScrollView.SetIndexWithoutScroll(playingIndex);
            }
            if (isNotCoolingDown) queueListScrollView.ScrollToSelected();
            return true;
        }

        public void _OnPlayListEntryClick() {
            if (currentPlayListButton != null) playListScrollView.gameObject.SetActive(false);
            playListLastInteractTime = DateTime.UtcNow;
            UpdatePlayList();
            queueListScrollView.ScrollToSelected();
        }

        public void _OnPlayListScroll() {
            playListLastInteractTime = DateTime.UtcNow;
        }

        public void _OnQueueListScroll() {
            playListLastInteractTime = DateTime.UtcNow;
        }

        public void _OnCurrentPlayListSelectClick() {
            SelectedPlayListIndex = handler != null ? handler.PlayListIndex : 0;
            _OnPlayListEntryClick();
        }

        public void _OnQueueListEntryClick() {
            playListLastInteractTime = DateTime.UtcNow;
            handler._PlayAt(SelectedPlayListIndex, queueListScrollView.lastInteractIndex, false);
        }

        public void _OnQueueListEntryDelete() {
            playListLastInteractTime = DateTime.UtcNow;
            handler._PlayAt(SelectedPlayListIndex, queueListScrollView.lastInteractIndex, true);
        }

        public void _UpdateProgress() {
            if (!core.IsPlaying) {
                hasUpdate = false;
                return;
            }
            UpdateProgressOnce();
            SendCustomEventDelayedSeconds(nameof(_UpdateProgress), 0.25F);
        }

        void UpdateProgressOnce() {
            var duration = core.Duration;
            if (duration <= 0 || float.IsInfinity(duration)) {
                if (timeContainer != null && statusText != null) {
                    timeContainer.SetActive(false);
                    statusText.enabled = true;
                }
                if (timeText != null)
                    timeText.text = languageManager.GetLocale("TimeIdleFormat");
                if (durationText != null)
                    durationText.text = languageManager.GetLocale("TimeIdleFormat");
                if (statusText != null) {
                    if (!string.IsNullOrEmpty(core.title) || !string.IsNullOrEmpty(core.author))
                        statusText.text = string.Format(languageManager.GetLocale("StreamingWithTitle"), core.title, core.author);
                    else
                        statusText.text = languageManager.GetLocale("Streaming");
                }
                if (progressSlider != null) {
                    progressSlider.SetValueWithoutNotify(1);
                    progressSlider.interactable = false;
                }
            } else {
                if (timeContainer != null && statusText != null) {
                    timeContainer.SetActive(true);
                    statusText.enabled = false;
                }
                var time = TimeSpan.FromSeconds(core.Time);
                var durationTS = TimeSpan.FromSeconds(duration);
                if (durationText != null)
                    durationText.text = string.Format(languageManager.GetLocale("TimeFormat"), durationTS);
                if (timeText != null)
                    timeText.text = string.Format(languageManager.GetLocale("TimeFormat"), time);
                if (statusText != null) {
                    if (core.IsPaused)
                        statusText.text = string.Format(languageManager.GetLocale("Paused"), time, durationTS);
                    else if (!string.IsNullOrEmpty(core.title) || !string.IsNullOrEmpty(core.author))
                        statusText.text = string.Format(languageManager.GetLocale("PlayingWithTitle"), time, durationTS, core.title, core.author);
                    else
                        statusText.text = string.Format(languageManager.GetLocale("Playing"), time, durationTS);
                }
                if (progressSlider != null) {
                    progressSlider.SetValueWithoutNotify(core.Progress);
                    progressSlider.interactable = true;
                }
            }
        }

        public void _ShiftBack100ms() {
            core.SyncOffset -= 0.1F;
        }
        public void _ShiftBack50ms() {
            core.SyncOffset -= 0.05F;
        }
        public void _ShiftForward50ms() {
            core.SyncOffset += 0.05F;
        }
        public void _ShiftForward100ms() {
            core.SyncOffset += 0.1F;
        }
        public void _ShiftReset() {
            core.SyncOffset = 0;
        }
        public void _OnSyncOffsetChange() {
            if (shiftOffsetText != null) shiftOffsetText.text = string.Format(languageManager.GetLocale("TimeDrift"), core.SyncOffset);
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