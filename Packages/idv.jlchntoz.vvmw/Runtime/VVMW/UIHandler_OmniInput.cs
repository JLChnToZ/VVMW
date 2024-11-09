using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public partial class UIHandler {
        ButtonEntry[] videoPlayerSelectButtons;

        void InitPlayerSelect() {
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
        }

        public void _OnURLChanged() {
            bool isEmpty = string.IsNullOrEmpty(urlInput.textComponent.text);
            if (otherObjectUnderUrlInput != null) otherObjectUnderUrlInput.SetActive(isEmpty);
            if (videoPlayerSelectPanel != null) videoPlayerSelectPanel.SetActive(!isEmpty);
            if (Utilities.IsValid(altUrlInput)) altUrlInput.gameObject.SetActive(!isEmpty);
        }

        public void _OnURLEndEdit() {
            _OnURLChanged();
            if (urlInputConfirmButton == null) {
                _InputConfirmClick();
                return;
            }
            byte player = core.GetSuitablePlayerType(urlInput.GetUrl());
            if (player > 0) {
                loadWithIndex = player;
                _LoadPlayerClick();
            }
        }

        public void _InputConfirmClick() {
            var url = urlInput.GetUrl();
            var altUrl = url;
            if (!VRCUrl.IsNullOrEmpty(url)) {
                if (Utilities.IsValid(altUrlInput)) {
                    altUrl = altUrlInput.GetUrl();
                    if (VRCUrl.IsNullOrEmpty(altUrl)) altUrl = url;
                }
                playListLastInteractTime = joinTime;
                if (Utilities.IsValid(handler)) {
                    handler.PlayUrl(url, altUrl, selectedPlayer);
                    if (queueListScrollView != null)
                        SelectedPlayListIndex = handler.PlayListIndex;
                    UpdatePlayList();
                } else
                    core.PlayUrl(url, altUrl, selectedPlayer);
                _InputCancelClick();
            }
        }

        public void _VideoPlayerSelect() {
            if (videoPlayerSelectRoot == null) return;
            videoPlayerSelectRoot.SetActive(!videoPlayerSelectRoot.activeSelf);
        }

        public void _InputCancelClick() {
            urlInput.SetUrl(VRCUrl.Empty);
            if (Utilities.IsValid(altUrlInput)) altUrlInput.SetUrl(VRCUrl.Empty);
            _OnUIUpdate();
            _OnURLChanged();
        }

        public void _LoadPlayerClick() {
            selectedPlayer = loadWithIndex;
            UpdatePlayerText();
            if (videoPlayerSelectRoot != null) videoPlayerSelectRoot.SetActive(false);
        }

        void UpdatePlayerText() =>
            SetLocalizedText(selectdPlayerText, selectdPlayerTMPro, videoPlayerSelectButtons[selectedPlayer - 1].Text);

        public void _DeferUpdatePlayList() {
            if (playListUpdateRequired && !UpdatePlayList() && playListUpdateRequired)
                SendCustomEventDelayedFrames(nameof(_DeferUpdatePlayList), 0);
        }
    }
}