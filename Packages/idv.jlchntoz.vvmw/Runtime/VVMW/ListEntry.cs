﻿using System;
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
        [TMProMigratable(nameof(contentTMPro))]
        [SerializeField] Text content;
        [SerializeField] TextMeshProUGUI contentTMPro;
        [BindEvent(nameof(Button.onClick), nameof(_OnClick))]
        [SerializeField] Button primaryButton;
        [BindEvent(nameof(Button.onClick), nameof(_OnDeleteClick))]
        [SerializeField] Button deleteButton;
        [SerializeField] Color selectedColor, normalColor;
        RectTransform rectTransform, parentRectTransform;
        public UdonSharpBehaviour callbackTarget;
        public string callbackEventName;
        public string callbackVariableName;
        public object callbackUserData;
        public string deleteEventName;
        [NonSerialized] public bool asPooledEntry;
        [NonSerialized] public bool indexAsUserData;
        [NonSerialized] public string[] pooledEntryNames;
        [NonSerialized] public object[] callbackUserDatas;
        [NonSerialized] public int selectedEntryIndex;
        [NonSerialized] public int entryOffset;
        [NonSerialized] public int spawnedEntryCount = 1;
        [NonSerialized] public int pooledEntryOffset, pooledEntryCount;
        float height;
        int lastOffset = -1;
        bool isSelected;

        public string TextContent {
            get {
                if (Utilities.IsValid(content)) return content.text;
                if (Utilities.IsValid(contentTMPro)) return contentTMPro.text;
                return "";
            }
            set {
                if (Utilities.IsValid(content)) content.text = value;
                if (Utilities.IsValid(contentTMPro)) contentTMPro.text = value;
            }
        }

        public bool HasDelete {
            get => deleteButton.gameObject.activeSelf;
            set => deleteButton.gameObject.SetActive(value);
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
            rectTransform = GetComponent<RectTransform>();
        }

        bool UpdateIndex() {
            if (!Utilities.IsValid(rectTransform)) rectTransform = GetComponent<RectTransform>();
            if (!Utilities.IsValid(parentRectTransform)) parentRectTransform = rectTransform.parent.GetComponent<RectTransform>();
            int newOffset = Mathf.FloorToInt((-parentRectTransform.anchoredPosition.y / rectTransform.rect.height - entryOffset - 1) / spawnedEntryCount + 1) * spawnedEntryCount + entryOffset;
            if (lastOffset == newOffset) return false;
            lastOffset = newOffset;
            return true;
        }

        void UpdatePositionAndContent() {
            if (!asPooledEntry) return;
            if (!Utilities.IsValid(rectTransform)) rectTransform = GetComponent<RectTransform>();
            if (lastOffset >= 0 && lastOffset < pooledEntryCount) {
                _UpdateContent();
                rectTransform.anchoredPosition = new Vector2(0, lastOffset * rectTransform.rect.height);
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
            TextContent = pooledEntryNames[lastOffset + pooledEntryOffset];
            Selected = lastOffset == selectedEntryIndex;
        }
    }
}