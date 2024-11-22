using UnityEngine;
using UnityEngine.UI;
using TMPro;
using VRC.SDKBase;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// A button entry that can be localized.
    /// </summary>
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
                if (Utilities.IsValid(buttonText)) return buttonText.text;
                if (Utilities.IsValid(buttonTMPro)) return buttonTMPro.text;
                return "";
            }
        }

        public LanguageManager LanguageManager {
            get => manager;
            set {
                manager = value;
                if (Utilities.IsValid(manager)) manager._AddListener(this);
                _OnLanguageChanged();
            }
        }

        public void _OnLanguageChanged() {
            var result = manager.GetLocale(key);
            if (Utilities.IsValid(args) && args.Length > 0)
                result = string.Format(result, args);
            if (Utilities.IsValid(buttonText)) buttonText.text = result;
            if (Utilities.IsValid(buttonTMPro)) buttonTMPro.text = result;
        }

        public void _OnClick() {
            if (!Utilities.IsValid(callbackTarget)) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(callbackEventName))
                callbackTarget.SendCustomEvent(callbackEventName);
        }
    }
}