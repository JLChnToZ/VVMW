using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [RequireComponent(typeof(Button))]
    [DisallowMultipleComponent]
    [BindEvent(typeof(Button), nameof(Button.onClick), nameof(_OnClick))]
    [AddComponentMenu("VizVid/Components/Button Entry")]
    [DefaultExecutionOrder(3)]
    public class ButtonEntry : UdonSharpBehaviour {
        LanguageManager manager;
        [TMProMigratable(nameof(buttonTMPro))]
        [SerializeField, LocalizedLabel] Text buttonText;
        [SerializeField, LocalizedLabel] TextMeshProUGUI buttonTMPro;
        object[] args;
        string key;
        [LocalizedLabel] public UdonSharpBehaviour callbackTarget;
        [LocalizedLabel] public string callbackEventName;
        [LocalizedLabel] public string callbackVariableName;
        [LocalizedLabel] public object callbackUserData;

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

        public string Text {
            get {
                if (buttonText != null) return buttonText.text;
                if (buttonTMPro != null) return buttonTMPro.text;
                return "";
            }
        }

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
            if (buttonTMPro != null) buttonTMPro.text = result;
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