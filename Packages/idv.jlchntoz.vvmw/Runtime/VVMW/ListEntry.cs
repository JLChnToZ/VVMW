using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/List Entry")]
    [DefaultExecutionOrder(3)]
    public class ListEntry : UdonSharpBehaviour {
        [SerializeField] GameObject contentGameObject;
        [SerializeField, HideInInspector, Resolve(nameof(contentGameObject), NullOnly = false)] Text content;
        [SerializeField, HideInInspector, Resolve(nameof(contentGameObject), NullOnly = false)] TextMeshProUGUI contentTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_OnClick))]
        [SerializeField] Button primaryButton;
        [BindEvent(nameof(Button.onClick), nameof(_OnDeleteClick))]
        [SerializeField] Button deleteButton;
        [SerializeField, HideInInspector, Resolve(nameof(deleteButton))]
        GameObject deleteButtonGO;
        [SerializeField] InputField inputField;
        [SerializeField, HideInInspector, Resolve(nameof(inputField))]
        GameObject inputFieldGO;
        [SerializeField] Color selectedColor, normalColor;
        [SerializeField, HideInInspector, Resolve(".")]
        RectTransform rectTransform;
        RectTransform parentRectTransform;
        public UdonSharpBehaviour callbackTarget;
        public string callbackEventName;
        public string callbackVariableName;
        public object callbackUserData;
        public string deleteEventName;
        [NonSerialized] public bool asPooledEntry;
        [NonSerialized] public bool indexAsUserData;
        [NonSerialized] public string[] pooledEntryNames, pooledEntryCopyContents;
        [NonSerialized] public object[] callbackUserDatas;
        [NonSerialized] public int selectedEntryIndex;
        [NonSerialized] public int entryOffset;
        [NonSerialized] public int spawnedEntryCount = 1;
        [NonSerialized] public int pooledEntryOffset, pooledEntryCount;
        [NonSerialized] public bool isUpwards;
        [NonSerialized] public bool inverseOrder;
        int lastOffset = -1;
        bool isSelected;

        public string TextContent {
            get {
                if (Utilities.IsValid(content)) return content.text;
                if (Utilities.IsValid(contentTMPro)) return contentTMPro.text;
                return "";
            }
            private set {
                if (Utilities.IsValid(content)) content.text = value;
                if (Utilities.IsValid(contentTMPro)) contentTMPro.text = value;
            }
        }

        public string CopyContent {
            get {
                if (Utilities.IsValid(inputField))
                    return inputField.text;
                return "";
            }
            set {
                if (!Utilities.IsValid(inputFieldGO)) return;
                inputField.text = value;
                inputFieldGO.SetActive(!string.IsNullOrWhiteSpace(value));
            }
        }

        public bool HasDelete {
            get => Utilities.IsValid(deleteButtonGO) && deleteButtonGO.activeSelf;
            set => deleteButtonGO.SetActive(value);
        }

        public bool Unlocked {
            get => primaryButton.interactable;
            set {
                primaryButton.interactable = value;
                deleteButton.interactable = value;
            }
        }

        public bool Selected {
            get => isSelected;
            set {
                isSelected = value;
                if (Utilities.IsValid(content)) content.color = isSelected ? selectedColor : normalColor;
                if (Utilities.IsValid(contentTMPro)) contentTMPro.color = isSelected ? selectedColor : normalColor;
            }
        }

        object UserData {
            get {
                if (asPooledEntry) {
                    if (indexAsUserData) return lastOffset;
                    if (lastOffset < 0 && lastOffset >= pooledEntryCount) return null;
                    if (Utilities.IsValid(callbackUserDatas)) return callbackUserDatas[lastOffset + pooledEntryOffset];
                }
                return callbackUserData;
            }
        }

        void Start() {
            if (!Utilities.IsValid(callbackUserData)) callbackUserData = this;
        }

        bool UpdateIndex() {
            if (!Utilities.IsValid(parentRectTransform)) parentRectTransform = (RectTransform)rectTransform.parent;
            float anchoredPosition = parentRectTransform.anchoredPosition.y;
            if (isUpwards) anchoredPosition = -anchoredPosition;
            int newOffset = Mathf.FloorToInt((anchoredPosition / rectTransform.rect.height - entryOffset - 1) / spawnedEntryCount + 1) * spawnedEntryCount + entryOffset;
            if (inverseOrder) newOffset = pooledEntryCount - newOffset - 1;
            if (lastOffset == newOffset) return false;
            lastOffset = newOffset;
            return true;
        }

        void UpdatePositionAndContent() {
            if (!asPooledEntry) return;
            if (lastOffset >= 0 && lastOffset < pooledEntryCount) {
                _UpdateContent();
                float offset = lastOffset;
                if (!isUpwards) offset = -offset;
                if (inverseOrder) offset = pooledEntryCount - offset;
                var anchoredPosition = rectTransform.anchoredPosition;
                anchoredPosition.y = offset * rectTransform.rect.height;
                rectTransform.anchoredPosition = anchoredPosition;
                gameObject.SetActive(true);
            } else {
                gameObject.SetActive(false);
            }
        }

        public void _OnClick() {
            if (!Utilities.IsValid(callbackTarget)) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(callbackEventName))
                callbackTarget.SendCustomEvent(callbackEventName);
        }

        public void _OnDeleteClick() {
            if (!Utilities.IsValid(callbackTarget)) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(deleteEventName))
                callbackTarget.SendCustomEvent(deleteEventName);
        }

        public void _OnParentScroll() {
            if (!asPooledEntry) return;
            if (UpdateIndex()) {
                UpdatePositionAndContent();
                callbackUserData = UserData;
            }
        }

        public void _UpdatePositionAndContent() {
            if (!asPooledEntry) return;
            UpdateIndex();
            UpdatePositionAndContent();
            callbackUserData = UserData;
        }

        public void _UpdateContent() {
            if (!asPooledEntry || lastOffset < 0 || lastOffset >= pooledEntryCount) return;
            int currentIndex = lastOffset + pooledEntryOffset;
            TextContent = pooledEntryNames[currentIndex];
            CopyContent = Utilities.IsValid(pooledEntryCopyContents) ? pooledEntryCopyContents[currentIndex] : "";
            Selected = lastOffset == selectedEntryIndex;
        }
    }
}