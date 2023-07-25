using System;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Components;
using VRC.SDK3.Components.Video;

using JLChnToZ.VRC.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class UIHandler : UdonSharpBehaviour {
        [Header("Main Reference")]
        [SerializeField] Core core;
        public FrontendHandler handler;
        [SerializeField] LanguageManager languageManager;
        [Header("Main UI")]
        [BindEvent(nameof(Button.onClick), nameof(_Play))]
        [SerializeField] Button playButton;
        [BindEvent(nameof(Button.onClick), nameof(_Pause))]
        [SerializeField] Button pauseButton;
        [BindEvent(nameof(Button.onClick), nameof(_Stop))]
        [SerializeField] Button stopButton;
        [BindEvent(nameof(Button.onClick), nameof(_LocalSync))]
        [SerializeField] Button reloadButton;
        [BindEvent(nameof(Button.onClick), nameof(_Skip))]
        [SerializeField] Button playNextButton;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_RepeatOne))]
        [SerializeField] Toggle repeatOneToggle;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_RepeatAll))]
        [SerializeField] Toggle repeatAllToggle;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_Shuffle))]
        [SerializeField] Toggle shuffleToggle;
        [BindEvent(nameof(Toggle.onValueChanged), nameof(_PlayListToggle))]
        [SerializeField] Toggle playlistToggle;
        [SerializeField] Text enqueueCountText;
        [BindEvent(nameof(VRCUrlInputField.onValueChanged), nameof(_OnURLChanged))]
        [BindEvent(nameof(VRCUrlInputField.onEndEdit), nameof(_OnURLEndEdit))]
        [SerializeField] VRCUrlInputField urlInput;
        [SerializeField] Text statusText;
        [SerializeField] GameObject videoPlayerSelectButtonTemplate;
        [BindEvent(nameof(Button.onClick), nameof(_InputCancelClick))]
        [SerializeField] Button cancelButton;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnSeek))]
        [SerializeField] Slider progressSlider;
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnVolumeSlide))]
        [SerializeField] Slider volumeSlider;
        [Header("Queue List / Play List")]
        [SerializeField] GameObject playListPanelRoot;
        [SerializeField] GameObject playListTemplate;
        [SerializeField] Transform playListContainer;
        [SerializeField] GameObject queueEntryTemplate;
        [SerializeField] Transform queueEntryContainer;
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
        ListEntry[] queueEntries, playListEntries;
        GameObject[] videoPlayerSelectButtons;
        [NonSerialized] public int queueEntryIndex = -1;
        [NonSerialized] public int selectedPlayListIndex;
        [NonSerialized] public byte loadWithIndex;
        int lastSelectedPlayListIndex, lastPlayingIndex;
        int lastDisplayCount;
        bool hasUpdate, wasUnlocked;
        string enqueueCountFormat;

        void Start() {
            var hasHandler = Utilities.IsValid(handler);
            if (hasHandler) core = handler.core;
            if (enqueueCountText != null) {
                enqueueCountFormat = enqueueCountText.text;
                enqueueCountText.text = string.Format(enqueueCountFormat, 0);
            }
            selectedPlayListIndex = hasHandler ? handler.PlayListIndex : 0;
            var playListRoot = playListContainer.GetComponentInParent<ScrollRect>();
            var playListRootGameObject = playListRoot != null ? playListRoot.gameObject : playListContainer.gameObject;
            var queueListRoot = queueEntryContainer.GetComponentInParent<ScrollRect>();
            var queueListRootGameObject = queueListRoot != null ? queueListRoot.gameObject : queueEntryContainer.gameObject;
            if (playListTemplate != null) {
                var playListNames = hasHandler ? handler.PlayListTitles : null;
                if (playListNames != null) {
                    playListEntries = new ListEntry[playListNames.Length + 1];
                    if (handler.HasQueueList)
                        InstantiatePlayListTemplate(0, languageManager.GetLocale("QueueList"));
                    for (int i = 0; i < playListNames.Length; i++)
                        InstantiatePlayListTemplate(i + 1, playListNames[i]);
                } else if (playListEntries == null)
                    playListEntries = new ListEntry[0];
                playListRootGameObject.SetActive(playListEntries.Length > 1);
            } else {
                playListRootGameObject.SetActive(false);
            }
            queueListRootGameObject.SetActive(hasHandler);
            if (videoPlayerSelectButtonTemplate != null) {
                var templateTransform = videoPlayerSelectButtonTemplate.transform;
                var parent = templateTransform.parent;
                var sibling = templateTransform.GetSiblingIndex() + 1;
                var videoPlayerNames = core.PlayerNames;
                videoPlayerSelectButtons = new GameObject[videoPlayerNames.Length];
                for (int i = 0; i < videoPlayerNames.Length; i++) {
                    var button = Instantiate(videoPlayerSelectButtonTemplate);
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
                    videoPlayerSelectButtons[i] = button;
                }
            }
            if (repeatAllToggle != null) repeatAllToggle.gameObject.SetActive(hasHandler);
            if (shuffleToggle != null) shuffleToggle.gameObject.SetActive(hasHandler);
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
        }

        void InstantiatePlayListTemplate(int index, string text) {
            var entryGameObject = Instantiate(playListTemplate);
            entryGameObject.SetActive(true);
            entryGameObject.transform.SetParent(playListContainer, false);
            var entry = entryGameObject.GetComponent<ListEntry>();
            entry.callbackTarget = this;
            entry.callbackUserData = index;
            entry.callbackVariableName = nameof(selectedPlayListIndex);
            entry.callbackEventName = nameof(_OnPlayListSelectClick);
            entry.HasDelete = false;
            entry.TextContent = text;
            entry.Selected = index == selectedPlayListIndex;
            playListEntries[index] = entry;
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

        public void _RepeatOne() {
            if (Utilities.IsValid(handler))
                handler.RepeatOne = !handler.RepeatOne;
            else
                core.Loop = !core.Loop;
        }

        public void _RepeatAll() {
            if (!Utilities.IsValid(handler)) return;
            handler.RepeatAll = !handler.RepeatAll;
        }

        public void _Shuffle() {
            if (!Utilities.IsValid(handler)) return;
            handler.Shuffle = !handler.Shuffle;
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

        public void _OnVolumeChange() {
            if (volumeSlider != null)
                volumeSlider.SetValueWithoutNotify(core.Volume);
        }

        public void _OnURLChanged() {
            statusText.enabled = string.IsNullOrEmpty(urlInput.textComponent.text);
        }

        public void _OnURLEndEdit() {
            _OnUIUpdate();
            _OnURLChanged();
        }

        public void _InputCancelClick() {
            urlInput.SetUrl(VRCUrl.Empty);
            _OnUIUpdate();
            _OnURLChanged();
        }

        public void _PlayListToggle() {
            if (playListPanelRoot == null) return;
            if (playlistToggle.isOn) {
                playListPanelRoot.SetActive(true);
                if (Utilities.IsValid(handler)) {
                    selectedPlayListIndex = handler.PlayListIndex;
                    _OnPlayListSelectClick();
                }
            } else {
                playListPanelRoot.SetActive(false);
            }
        }

        public void _OnLanguageChanged() {
            _OnUIUpdate();
            _OnSyncOffsetChange();
            if (Utilities.IsValid(handler) && handler.HasQueueList) playListEntries[0].TextContent = languageManager.GetLocale("QueueList");
        }

        public void _LoadPlayerClick() {
            var url = urlInput.GetUrl();
            urlInput.SetUrl(VRCUrl.Empty);
            if (Utilities.IsValid(handler))
                handler.PlayUrl(url, loadWithIndex);
            else
                core.PlayUrl(url, loadWithIndex);
            _OnURLChanged();
        }

        public void _OnUIUpdate() {
            bool hasHandler = Utilities.IsValid(handler);
            bool unlocked = !hasHandler || !handler.Locked;
            bool canPlay = false;
            bool canPause = false;
            bool canStop = false;
            bool canLocalSync = false;
            bool canSelectPlayer = false;
            bool canSeek = false;
            switch (core.State) {
                case 0: // Idle
                    statusText.text = languageManager.GetLocale("VVMW_Name");
                    break;
                case 1: // Loading
                    statusText.text = languageManager.GetLocale("Loading");
                    break;
                case 2: // Error
                    var errorCode = core.LastError;
                    switch (errorCode) {
                        case VideoError.InvalidURL: statusText.text = languageManager.GetLocale("InvalidURL"); break;
                        case VideoError.AccessDenied: statusText.text = languageManager.GetLocale(core.IsTrusted ? "AccessDenied" : "AccessDeniedUntrusted"); break;
                        case VideoError.PlayerError: statusText.text = languageManager.GetLocale("PlayerError"); break;
                        case VideoError.RateLimited: statusText.text = languageManager.GetLocale("RateLimited"); break;
                        default: statusText.text = string.Format(languageManager.GetLocale("Unknown"), (int)errorCode); break;
                    }
                    break;
                case 3: // Ready
                    statusText.text = languageManager.GetLocale("Ready");
                    if (progressSlider != null) {
                        progressSlider.SetValueWithoutNotify(1);
                        progressSlider.interactable = false;
                    }
                    canPlay = unlocked && !canSelectPlayer;
                    break;
                case 4: // Playing
                    canPause = unlocked && !canSelectPlayer;
                    canStop = unlocked && !canSelectPlayer;
                    canSeek = true;
                    break;
                case 5: // Paused
                    canPlay = unlocked && !canSelectPlayer;
                    canStop = unlocked && !canSelectPlayer;
                    canSeek = true;
                    break;
            }
            var url = urlInput.GetUrl();
            if (unlocked && Utilities.IsValid(url) && !url.Equals(VRCUrl.Empty)) {
                canSelectPlayer = true;
            } else if (reloadButton != null) {
                var localUrl = core.Url;
                canLocalSync = Utilities.IsValid(localUrl) && !localUrl.Equals(VRCUrl.Empty);
            }
            if (playButton != null) playButton.gameObject.SetActive(canPlay);
            if (pauseButton != null) pauseButton.gameObject.SetActive(canPause);
            if (stopButton != null) stopButton.gameObject.SetActive(canStop);
            if (repeatOneToggle != null) {
                repeatOneToggle.interactable = unlocked;
                repeatOneToggle.gameObject.SetActive(!canSelectPlayer);
                repeatOneToggle.SetIsOnWithoutNotify(hasHandler ? handler.RepeatOne : core.Loop);
            }
            if (hasHandler) {
                if (repeatAllToggle != null) {
                    repeatAllToggle.interactable = unlocked;
                    repeatAllToggle.SetIsOnWithoutNotify(handler.RepeatAll);
                }
                if (shuffleToggle != null) {
                    shuffleToggle.interactable = unlocked;
                    shuffleToggle.SetIsOnWithoutNotify(handler.Shuffle);
                }
            }
            if (reloadButton != null) reloadButton.gameObject.SetActive(canLocalSync);
            if (cancelButton != null) cancelButton.gameObject.SetActive(canSelectPlayer);
            if (playlistToggle != null) playlistToggle.gameObject.SetActive(!canSelectPlayer);
            if (canSeek) {
                UpdateProgressOnce();
                if (!hasUpdate) {
                    hasUpdate = true;
                    _UpdateProgress();
                }
                progressSlider.interactable = true;
            } else {
                progressSlider.SetValueWithoutNotify(1);
                progressSlider.interactable = false;
            }
            for (int i = 0; i < videoPlayerSelectButtons.Length; i++)
                videoPlayerSelectButtons[i].SetActive(canSelectPlayer);
            if (wasUnlocked != unlocked) {
                wasUnlocked = unlocked;
                if (playListEntries != null)
                    for (int i = 0; i < playListEntries.Length; i++) {
                        var entry = playListEntries[i];
                        if (entry != null) entry.Unlocked = unlocked;
                    }
                if (queueEntries != null)
                    for (int i = 0; i < queueEntries.Length; i++) {
                        var entry = queueEntries[i];
                        if (entry != null) entry.Unlocked = unlocked;
                    }
                urlInput.interactable = unlocked;
                if (!unlocked) urlInput.SetUrl(VRCUrl.Empty);
            }
            if (hasHandler) UpdatePlayList();
        }

        void UpdatePlayList() {
            int playListIndex = handler.PlayListIndex;
            int playingIndex = handler.CurrentPlayingIndex;
            int pendingCount, offset;
            VRCUrl[] queuedUrls = handler.QueueUrls, playListUrls = handler.PlayListUrls;
            string[] entryTitles = handler.PlayListEntryTitles;
            int[] urlOffsets = handler.PlayListUrlOffsets;
            if (playListIndex > 0) {
                offset = urlOffsets[playListIndex - 1];
                pendingCount = (playListIndex < urlOffsets.Length ? urlOffsets[playListIndex] : playListUrls.Length) - offset;
            } else {
                offset = 0;
                pendingCount = queuedUrls.Length;
            }
            if (playNextButton != null) playNextButton.gameObject.SetActive(playingIndex < pendingCount - 1);
            if (enqueueCountText != null)
                enqueueCountText.text = string.Format(enqueueCountFormat, pendingCount - playingIndex - 1);
            bool shouldRefreshQueue = selectedPlayListIndex <= 0 || lastSelectedPlayListIndex != selectedPlayListIndex || lastPlayingIndex != playingIndex;
            lastSelectedPlayListIndex = selectedPlayListIndex;
            lastPlayingIndex = playingIndex;
            if (shouldRefreshQueue && queueEntryTemplate != null && queueEntryContainer.gameObject.activeInHierarchy) {
                if (selectedPlayListIndex != playListIndex) {
                    if (selectedPlayListIndex > 0) {
                        offset = urlOffsets[selectedPlayListIndex - 1];
                        pendingCount = (selectedPlayListIndex < urlOffsets.Length ? urlOffsets[selectedPlayListIndex] : playListUrls.Length) - offset;
                    } else {
                        offset = 0;
                        pendingCount = queuedUrls.Length;
                    }
                    playingIndex = -1;
                }
                EnsurePlaylistCapacity(pendingCount);
                if (queueEntries != null)
                    for (int i = 0, count = Mathf.Max(lastDisplayCount, pendingCount); i < count; i++) {
                        var entry = queueEntries[i];
                        if (i < pendingCount) {
                            entry.gameObject.SetActive(true);
                            if (selectedPlayListIndex > 0) {
                                entry.TextContent = entryTitles[i + offset];
                                entry.HasDelete = false;
                            } else {
                                entry.TextContent = queuedUrls[i].Get();
                                entry.HasDelete = true;
                            }
                            entry.Selected = i == playingIndex;
                        } else
                            entry.gameObject.SetActive(false);
                    }
                lastDisplayCount = pendingCount;
            }
        }

        void EnsurePlaylistCapacity(int requestedSize) {
            if (requestedSize <= 0 || (queueEntries != null && queueEntries.Length >= requestedSize))
                return;
            int oldLength = 0;
            var newEntries = new ListEntry[requestedSize + 10];
            if (queueEntries != null) {
                oldLength = queueEntries.Length;
                Array.Copy(queueEntries, newEntries, oldLength);
            }
            for (int i = oldLength; i < newEntries.Length; i++) {
                var entryGameObject = Instantiate(queueEntryTemplate);
                entryGameObject.SetActive(false);
                entryGameObject.transform.SetParent(queueEntryContainer, false);
                var entry = entryGameObject.GetComponent<ListEntry>();
                entry.callbackTarget = this;
                entry.callbackUserData = i;
                entry.callbackVariableName = nameof(queueEntryIndex);
                entry.callbackEventName = nameof(_OnQueueEntryClick);
                entry.deleteEventName = nameof(_OnQueueEntryDelete);
                newEntries[i] = entry;
            }
            queueEntries = newEntries;
        }

        public void _OnPlayListSelectClick() {
            for (int i = 0; i < playListEntries.Length; i++) {
                var entry = playListEntries[i];
                if (entry != null) entry.Selected = i == selectedPlayListIndex;
            }
            _OnUIUpdate();
        }

        public void _OnQueueEntryClick() => handler._PlayAt(selectedPlayListIndex, queueEntryIndex, false);

        public void _OnQueueEntryDelete() => handler._PlayAt(selectedPlayListIndex, queueEntryIndex, true);


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
                statusText.text = languageManager.GetLocale("Streaming");
                if (progressSlider != null) {
                    progressSlider.SetValueWithoutNotify(1);
                    progressSlider.interactable = false;
                }
            } else {
                var time = TimeSpan.FromSeconds(core.Time);
                var durationTS = TimeSpan.FromSeconds(duration);
                statusText.text = string.Format(languageManager.GetLocale("Playing"), time, durationTS);
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