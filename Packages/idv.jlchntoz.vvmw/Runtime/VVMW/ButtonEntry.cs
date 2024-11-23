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
        /// <summary>
        /// The callback target for the button.
        /// </summary>
        [LocalizedLabel] public UdonSharpBehaviour callbackTarget;
        /// <summary>
        /// The event name to call on the callback target.
        /// </summary>
        [LocalizedLabel] public string callbackEventName;
        /// <summary>
        /// The variable name to set on the callback target.
        /// </summary>
        [LocalizedLabel] public string callbackVariableName;
        /// <summary>
        /// The custom data to pass to the callback target.
        /// Which will be set to the variable with the name of <see cref="callbackVariableName"/>.
        /// </summary>
        [LocalizedLabel] public object callbackUserData;

        /// <summary>
        /// The arguments for the localized text.
        /// </summary>
        public object[] Args {
            get => args;
            set {
                args = value;
                _OnLanguageChanged();
            }
        }

        /// <summary>
        /// The key for the localized text.
        /// </summary>
        public string Key {
            get => key;
            set {
                key = value;
                _OnLanguageChanged();
            }
        }

        /// <summary>
        /// The localized text.
        /// </summary>
        public string Text {
            get {
                if (Utilities.IsValid(buttonText)) return buttonText.text;
                if (Utilities.IsValid(buttonTMPro)) return buttonTMPro.text;
                return "";
            }
        }

        /// <summary>
        /// The <see cref="LanguageManager"/> to use for localization.
        /// </summary>
        public LanguageManager LanguageManager {
            get => manager;
            set {
                manager = value;
                if (Utilities.IsValid(manager)) manager._AddListener(this);
                _OnLanguageChanged();
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnLanguageChanged() {
            var result = manager.GetLocale(key);
            if (Utilities.IsValid(args) && args.Length > 0)
                result = string.Format(result, args);
            if (Utilities.IsValid(buttonText)) buttonText.text = result;
            if (Utilities.IsValid(buttonTMPro)) buttonTMPro.text = result;
        }

        /// <summary>
        /// Callback entry point for the button.
        /// </summary>
        public void _OnClick() {
            if (!Utilities.IsValid(callbackTarget)) return;
            if (!string.IsNullOrEmpty(callbackVariableName))
                callbackTarget.SetProgramVariable(callbackVariableName, callbackUserData);
            if (!string.IsNullOrEmpty(callbackEventName))
                callbackTarget.SendCustomEvent(callbackEventName);
        }
    }
}