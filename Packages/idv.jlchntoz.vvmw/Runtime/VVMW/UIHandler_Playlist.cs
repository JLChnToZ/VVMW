using System;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        string[] playListNames;
        [NonSerialized] public byte loadWithIndex;
        int lastSelectedPlayListIndex, lastPlayingIndex;
        int lastDisplayCount;
        string enqueueCountFormat;
        bool playListUpdateRequired;

        int SelectedPlayListIndex {
            get {
                if (!Utilities.IsValid(playListScrollView)) return 0;
                int selectedIndex = playListScrollView.SelectedIndex;
                if (Utilities.IsValid(handler)) {
                    if (handler.HistorySize > 0) {
                        if (selectedIndex == 0) return -1;
                        if (handler.HasQueueList) selectedIndex--;
                    } else if (!handler.HasQueueList)
                        selectedIndex++;
                }
                return selectedIndex;
            }
            set {
                if (!Utilities.IsValid(playListScrollView)) return;
                if (value < 0) {
                    playListScrollView.SelectedIndex = 0;
                    return;
                }
                if (Utilities.IsValid(handler)) {
                    if (handler.HistorySize > 0) {
                        if (handler.HasQueueList) value++;
                    } else if (!handler.HasQueueList)
                        value--;
                }
                playListScrollView.SelectedIndex = value;
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
                    bool hasQueueList = handler.HasQueueList;
                    bool hasHistory = handler.HistorySize > 0;
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
                    playListNames = new[] { languageManager.GetLocale("QueueList") };
                bool hasPlayList = playListNames.Length > 1;
                playListScrollView.EventPrefix = "_OnPlayList";
                playListScrollView.CanDelete = false;
                playListScrollView.EntryNames = playListNames;
                SelectedPlayListIndex = hasHandler ? handler.PlayListIndex : 0;
                if (Utilities.IsValid(playListTogglePanelButton))
                    playListScrollView.gameObject.SetActive(false);
                else
                    playListScrollView.gameObject.SetActive(hasPlayList);
            }
            if (Utilities.IsValid(queueListScrollView)) {
                queueListScrollView.EventPrefix = "_OnQueueList";
                queueListScrollView.gameObject.SetActive(hasHandler);
            }
        }

        public void _PlayListTogglePanel() {
            if (!Utilities.IsValid(playListScrollView)) return;
            var playListGameObject = playListScrollView.gameObject;
            playListGameObject.SetActive(!playListGameObject.activeSelf);
        }

        public void _PlayListToggle() {
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
            VRCUrl[] queuedUrls = handler.QueueUrls, playListUrls = handler.PlayListUrls;
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
            bool isEntryContainerInactive = !Utilities.IsValid(queueListScrollView) || !queueListScrollView.gameObject.activeInHierarchy;
            int selectedPlayListIndex = SelectedPlayListIndex;
            bool isNotCoolingDown = (DateTime.UtcNow - playListLastInteractTime) >= interactCoolDown;
            if (isEntryContainerInactive || isNotCoolingDown)
                SelectedPlayListIndex = selectedPlayListIndex = playListIndex;
            if (Utilities.IsValid(playNextButton)) playNextButton.gameObject.SetActive(hasPending);
            if (Utilities.IsValid(currentPlayListButton)) currentPlayListButton.gameObject.SetActive(hasPending && selectedPlayListIndex >= 0);
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
                queueListScrollView.EntryNames = queuedTitles;
                queueListScrollView.SetIndexWithoutScroll(-1);
            } else if (selectedPlayListIndex == -1) {
                queueListScrollView.CanDelete = false;
                queueListScrollView.EntryNames = historyTitles;
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
            if (Utilities.IsValid(currentPlayListButton)) playListScrollView.gameObject.SetActive(false);
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
            SelectedPlayListIndex = Utilities.IsValid(handler) ? handler.PlayListIndex : 0;
            _OnPlayListEntryClick();
        }

        public void _OnQueueListEntryClick() {
            playListLastInteractTime = DateTime.UtcNow;
            int selectedPlayListIndex = SelectedPlayListIndex;
            handler.PlayAt(selectedPlayListIndex, queueListScrollView.lastInteractIndex, false);
            if (selectedPlayListIndex < 0) {
                SelectedPlayListIndex = 0;
                UpdatePlayList();
            }
        }

        public void _OnQueueListEntryDelete() {
            playListLastInteractTime = DateTime.UtcNow;
            int selectedPlayListIndex = SelectedPlayListIndex;
            handler.PlayAt(selectedPlayListIndex, queueListScrollView.lastInteractIndex, true);
            if (selectedPlayListIndex < 0) {
                SelectedPlayListIndex = 0;
                UpdatePlayList();
            }
        }
    }
}