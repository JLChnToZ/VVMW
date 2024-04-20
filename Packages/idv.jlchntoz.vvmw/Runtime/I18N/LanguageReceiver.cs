﻿using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace JLChnToZ.VRC.VVMW.I18N {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Locales/Language Receiver")]
    [DefaultExecutionOrder(1)]
    public class LanguageReceiver : UdonSharpBehaviour {
        [SerializeField, HideInInspector, BindUdonSharpEvent] LanguageManager manager;
        [SerializeField] string key;
        object[] args;
        Text text;
        TextMeshProUGUI textMeshPro;
        bool afterFirstRun;

        public object[] Args {
            get => args;
            set {
                args = value;
                _OnLanguageChanged();
            }
        }

        void OnEnable() {
            if (afterFirstRun) return;
            afterFirstRun = true;
            if (manager == null) return;
            text = GetComponent<Text>();
            textMeshPro = GetComponent<TextMeshProUGUI>();
            if (string.IsNullOrEmpty(key)) {
                if (text != null) key = text.text;
                else if (textMeshPro != null) key = textMeshPro.text;
            }
            _OnLanguageChanged();
        }

        public void _OnLanguageChanged() {
            var result = manager.GetLocale(key);
            if (args != null && args.Length > 0)
                result = string.Format(result, args);
            if (text != null) text.text = result;
            if (textMeshPro != null) textMeshPro.text = result;
        }
    }
}