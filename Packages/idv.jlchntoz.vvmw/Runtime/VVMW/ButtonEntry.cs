using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    [BindEvent(typeof(Button), nameof(Button.onClick), nameof(_OnClick))]
    [AddComponentMenu("VizVid/Components/Button Entry")]
    [DefaultExecutionOrder(3)]
    public class ButtonEntry : UdonSharpBehaviour {
        LanguageManager manager;
        [SerializeField] Text buttonText;
        object[] args;
        string key;
        public UdonSharpBehaviour callbackTarget;
        public string callbackEventName;
        public string callbackVariableName;
        public object callbackUserData;

        public object[] Args {
            get => args;
            set {
                args = value;
                _OnLanguageChanged();
            }
        }

        public string Key {
            get => key;
            set {
                key = value;
                _OnLanguageChanged();
            }
        }

        public string Text => buttonText.text;

        public LanguageManager LanguageManager {
            get => manager;
            set {
                manager = value;
                if (manager != null) manager._AddListener(this);
                _OnLanguageChanged();
            }
        }

        public void _OnLanguageChanged() {
            var result = manager.GetLocale(key);
            if (args != null && args.Length > 0)
                result = string.Format(result, args);
            if (buttonText != null) buttonText.text = result;
        }

        public void _OnClick() {
            if (callbackTarget == null) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(callbackEventName))
                callbackTarget.SendCustomEvent(callbackEventName);
        }
    }
}