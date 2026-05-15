using System;
using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        string[] playListNames;
        string[] historyCopyContents;
        [NonSerialized] public byte loadWithIndex;
        int lastSelectedPlayListIndex, lastPlayingIndex;
        int selectedPlaylistTabIndex;
        int lastDisplayCount;
        string enqueueCountFormat;
        bool playListUpdateRequired;

        int SelectedPlayListIndex {
            get {
                if (!Utilities.IsValid(playListScrollView)) return 0;
                int selectedIndex = playListScrollView.SelectedIndex;
                if (Utilities.IsValid(handler)) {
                    bool hasHistoryButton = Utilities.IsValid(historySelectButton);
                    if (hasHistoryButton && selectedPlaylistTabIndex < 0) return -1;
                    bool hasQueueListButton = Utilities.IsValid(queueListSelectButton);
                    if (hasQueueListButton && selectedPlaylistTabIndex == 0) return 0;
                    if (handler.HistorySize > 0 && !hasHistoryButton) {
                        if (selectedIndex == 0) return -1;
                        if (handler.HasQueueList && !hasQueueListButton) selectedIndex--;
                    } else if (!handler.HasQueueList || hasQueueListButton)
                        selectedIndex++;
                }
                return selectedIndex;
            }
            set {
                if (Utilities.IsValid(handler)) {
                    selectedPlaylistTabIndex = value;
                    bool hasHistoryButton = Utilities.IsValid(historySelectButton);
                    bool hasHistory = handler.HistorySize > 0;
                    bool hasQueueListButton = Utilities.IsValid(queueListSelectButton);
                    bool hasQueueList = handler.HasQueueList;
                    if (hasHistoryButton && Utilities.IsValid(historySelectedIndicator)) {
                        bool isHistory = selectedPlaylistTabIndex < 0;
                        historySelectButtonObject.SetActive(hasHistory && !isHistory);
                        historySelectedIndicator.SetActive(isHistory);
                    }
                    if (hasQueueListButton && Utilities.IsValid(queueListSelectedIndicator)) {
                        bool isQueueList = selectedPlaylistTabIndex == 0;
                        queueListSelectButtonObject.SetActive(hasQueueList && !isQueueList);
                        queueListSelectedIndicator.SetActive(isQueueList);
                    }
                    if (Utilities.IsValid(playListTogglePanelButton) && Utilities.IsValid(currentPlayListSelectButton)) {
                        bool isPlayList = selectedPlaylistTabIndex > 0;
                        bool hasPlayList = Utilities.IsValid(playListNames) && playListNames.Length > 0;
                        currentPlayListSelectButtonObject.SetActive(hasPlayList && !isPlayList);
                        playListTogglePanelButtonObject.SetActive(isPlayList);
                    }
                    if (Utilities.IsValid(currentPlayListButton)) playListGameObject.SetActive(false);
                    if (hasHistory && !hasHistoryButton) {
                        if (hasQueueList && !hasQueueListButton) value++;
                    } else if (!hasQueueList || hasQueueListButton)
                        value--;
                }
                if (!Utilities.IsValid(playListScrollView)) return;
                playListScrollView.SelectedIndex = Mathf.Max(0, value);
            }
        }

        void InitPlayQueueList() {
            if (Utilities.IsValid(enqueueCountText)) {
                enqueueCountFormat = enqueueCountText.text;
                enqueueCountText.text = string.Format(enqueueCountFormat, 0);
            } else if (Utilities.IsValid(enqueueCountTMPro)) {
                enqueueCountFormat = enqueueCountTMPro.text;
                enqueueCountTMPro.text = string.Format(enqueueCountFormat, 0);
            }
            var hasHandler = Utilities.IsValid(handler);
            if (Utilities.IsValid(playListPanelRoot)) playListPanelRoot.SetActive(true);
            if (Utilities.IsValid(playListScrollView)) {
                playListNames = hasHandler ? handler.PlayListTitles : null;
                if (Utilities.IsValid(playListNames)) {
                    bool hasQueueList = handler.HasQueueList && !Utilities.IsValid(queueListSelectButton);
                    bool hasHistory = handler.HistorySize > 0 && !Utilities.IsValid(historySelectButton);
                    if (hasQueueList || hasHistory) {
                        int length = playListNames.Length;
                        if (hasQueueList) length++;
                        if (hasHistory) length++;
                        var temp = new string[length];
                        int i = 0;
                        if (hasHistory) temp[i++] = languageManager.GetLocale("PlaybackHistory");
                        if (hasQueueList) temp[i++] = languageManager.GetLocale("QueueList");
                        Array.Copy(playListNames, 0, temp, i, playListNames.Length);
                        playListNames = temp;
                    }
                } else if (!Utilities.IsValid(playListNames))
                    playListNames = Utilities.IsValid(queueListSelectButton) ?
                        new string[0] :
                        new[] { languageManager.GetLocale("QueueList") };
                bool hasPlayList = playListNames.Length > 1;
                playListScrollView.EventPrefix = "_OnPlayList";
                playListScrollView.CanDelete = false;
                playListScrollView.EntryNames = playListNames;
                SelectedPlayListIndex = hasHandler ? handler.PlayListIndex : 0;
                if (Utilities.IsValid(playListTogglePanelButton))
                    playListGameObject.SetActive(false);
                else
                    playListGameObject.SetActive(hasPlayList);
            }
            if (Utilities.IsValid(queueListScrollView)) {
                queueListScrollView.EventPrefix = "_OnQueueList";
                queueListScrollViewObject.SetActive(hasHandler);
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _PlayListTogglePanel() {
            if (!Utilities.IsValid(playListGameObject)) return;
            playListGameObject.SetActive(!playListGameObject.activeSelf);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _PlayListToggle() {
            if (!Utilities.IsValid(playListScrollView)) return;
            if (playlistToggle.isOn) {
                playListPanelRoot.SetActive(true);
                if (Utilities.IsValid(handler)) {
                    if (Utilities.IsValid(queueListScrollView))
                        queueListScrollView.SelectedIndex = handler.PlayListIndex;
                    playListLastInteractTime = joinTime;
                }
            } else {
                playListLastInteractTime = DateTime.UtcNow;
                playListPanelRoot.SetActive(false);
            }
        }

        bool UpdatePlayList() {
            int playListIndex = handler.PlayListIndex;
            int playingIndex = handler.CurrentPlayingIndex;
            int displayCount, offset;
            int pendingCount = handler.PendingCount;
            VRCUrl[] queuedUrls = handler.QueueUrls, playListUrls = handler.PlayListUrls, historyUrls = handler.HistoryUrls;
            string[] entryTitles = handler.PlayListEntryTitles, queuedTitles = handler.QueueTitles, historyTitles = handler.HistoryTitles;
            int[] urlOffsets = handler.PlayListUrlOffsets;
            if (playListIndex > 0) {
                offset = urlOffsets[playListIndex - 1];
                displayCount = (playListIndex < urlOffsets.Length ? urlOffsets[playListIndex] : playListUrls.Length) - offset;
            } else {
                offset = 0;
                displayCount = queuedUrls.Length;
            }
            bool hasPending = pendingCount > 0;
            bool isEntryContainerInactive = !Utilities.IsValid(queueListScrollViewObject) || !queueListScrollViewObject.activeInHierarchy;
            int selectedPlayListIndex = SelectedPlayListIndex;
            bool isNotCoolingDown = (DateTime.UtcNow - playListLastInteractTime) >= interactCoolDown;
            if (isEntryContainerInactive || isNotCoolingDown)
                SelectedPlayListIndex = selectedPlayListIndex = playListIndex;
            if (Utilities.IsValid(playNextButtonObject)) playNextButtonObject.SetActive(hasPending);
            if (autoHideCurrentPlayListButton && Utilities.IsValid(currentPlayListButtonObject)) currentPlayListButtonObject.SetActive(hasPending && selectedPlayListIndex >= 0);
            if (!string.IsNullOrEmpty(enqueueCountFormat))
                SetText(enqueueCountText, enqueueCountTMPro, string.Format(enqueueCountFormat, pendingCount));
            if (selectedPlayListIndex > 0)
                SetText(selectedPlayListText, selectedPlayListTMPro, handler.PlayListTitles[selectedPlayListIndex - 1]);
            else
                SetLocalizedText(selectedPlayListText, selectedPlayListTMPro, selectedPlayListIndex < 0 ? "PlaybackHistory" : "QueueList");
            if (Utilities.IsValid(playNextIndicator))
                playNextIndicator.SetActive(!handler.Shuffle && selectedPlayListIndex == 0 && handler.PlayListIndex == 0 && handler.PendingCount > 0);
            bool shouldRefreshQueue = playListUpdateRequired || selectedPlayListIndex <= 0 || lastSelectedPlayListIndex != selectedPlayListIndex || lastPlayingIndex != playingIndex;
            lastSelectedPlayListIndex = selectedPlayListIndex;
            lastPlayingIndex = playingIndex;
            if (!shouldRefreshQueue || !Utilities.IsValid(queueListScrollView))
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
                } else if (selectedPlayListIndex < 0) {
                    offset = 0;
                    displayCount = historyTitles.Length;
                } else {
                    offset = 0;
                    displayCount = queuedUrls.Length;
                }
                playingIndex = -1;
            }
            if (selectedPlayListIndex == 0) {
                queueListScrollView.CanDelete = true;
                queueListScrollView.SetEntries(queuedTitles, null);
                queueListScrollView.SetIndexWithoutScroll(-1);
            } else if (selectedPlayListIndex == -1) {
                if (!Utilities.IsValid(historyCopyContents) || historyCopyContents.Length < historyTitles.Length)
                    historyCopyContents = new string[historyTitles.Length];
                for (int i = 0; i < historyTitles.Length; i++)
                    historyCopyContents[i] = historyUrls[i].ToString();
                queueListScrollView.CanDelete = false;
                queueListScrollView.SetEntries(historyTitles, historyCopyContents);
                queueListScrollView.SetIndexWithoutScroll(-1);
            } else {
                queueListScrollView.CanDelete = false;
                queueListScrollView.SetEntries(entryTitles, null, offset, displayCount);
                queueListScrollView.SetIndexWithoutScroll(playingIndex);
            }
            if (isNotCoolingDown) queueListScrollView.ScrollToSelected();
            return true;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnPlayListEntryClick() {
            if (Utilities.IsValid(currentPlayListButton)) playListGameObject.SetActive(false);
            playListLastInteractTime = DateTime.UtcNow;
            UpdatePlayList();
            queueListScrollView.SendCustomEventDelayedFrames(nameof(queueListScrollView.ScrollToSelected), 0);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnPlayListScroll() {
            playListLastInteractTime = DateTime.UtcNow;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnQueueListScroll() {
            playListLastInteractTime = DateTime.UtcNow;
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnCurrentPlayListSelectClick() {
            SelectedPlayListIndex = Utilities.IsValid(handler) ? handler.PlayListIndex : 0;
            _OnPlayListEntryClick();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnQueueListEntryClick() {
            playListLastInteractTime = DateTime.UtcNow;
            int selectedPlayListIndex = SelectedPlayListIndex;
            handler.PlayAt(selectedPlayListIndex, queueListScrollView.lastInteractIndex, false);
            if (selectedPlayListIndex < 0) {
                SelectedPlayListIndex = 0;
                UpdatePlayList();
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnQueueListEntryDelete() {
            playListLastInteractTime = DateTime.UtcNow;
            int selectedPlayListIndex = SelectedPlayListIndex;
            handler.PlayAt(selectedPlayListIndex, queueListScrollView.lastInteractIndex, true);
            if (selectedPlayListIndex < 0) {
                SelectedPlayListIndex = 0;
                UpdatePlayList();
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnPlayListSelect() {
            if (!Utilities.IsValid(handler)) return;
            playListLastInteractTime = DateTime.UtcNow;
            SelectedPlayListIndex = Mathf.Max(1, handler.PlayListIndex);
            UpdatePlayList();
            if (Utilities.IsValid(playListGameObject)) playListGameObject.SetActive(false);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnQueueListSelect() {
            SelectedPlayListIndex = 0;
            playListLastInteractTime = DateTime.UtcNow;
            UpdatePlayList();
            if (Utilities.IsValid(playListGameObject)) playListGameObject.SetActive(false);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnHistorySelect() {
            SelectedPlayListIndex = -1;
            playListLastInteractTime = DateTime.UtcNow;
            UpdatePlayList();
            if (Utilities.IsValid(playListGameObject)) playListGameObject.SetActive(false);
        }
    }
}