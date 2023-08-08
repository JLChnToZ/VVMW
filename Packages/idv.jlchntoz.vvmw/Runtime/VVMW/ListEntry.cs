using UnityEngine;
using UnityEngine.UI;
using UdonSharp;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    [DisallowMultipleComponent]
    public class ListEntry : UdonSharpBehaviour {
        [SerializeField] Text content;
        [BindEvent(nameof(Button.onClick), nameof(_OnClick))]
        [SerializeField] Button primaryButton;
        [BindEvent(nameof(Button.onClick), nameof(_OnDeleteClick))]
        [SerializeField] Button deleteButton;
        [SerializeField] Color selectedColor, normalColor;
        public UdonSharpBehaviour callbackTarget;
        public string callbackEventName;
        public string callbackVariableName;
        public object callbackUserData;
        public string deleteEventName;
        bool isSelected;
        
        public string TextContent {
            get => content.text;
            set => content.text = value;
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
                content.color = isSelected ? selectedColor : normalColor;
            }
        }

        void Start() {
            if (callbackUserData == null) callbackUserData = this;
        }

        public void _OnClick() {
            if (callbackTarget == null) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(callbackEventName))
                callbackTarget.SendCustomEvent(callbackEventName);
        }

        public void _OnDeleteClick() {
            if (callbackTarget == null) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(deleteEventName))
                callbackTarget.SendCustomEvent(deleteEventName);
        }
    }
}