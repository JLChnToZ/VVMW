using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW.I18N {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Locales/Language Manager")]
    [DefaultExecutionOrder(0)]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#locale")]
    public class LanguageManager : UdonSharpEventSender {
        [SerializeField] TextAsset[] languageJsonFiles;
        [SerializeField, Multiline] string languageJson;
        DataDictionary languages, currentLanguage;

        [FieldChangeCallback(nameof(LanguageKey))]
        string languageKey = "EN";
        string[] languageKeys;
        string[] languageNames;
        bool afterFirstRun;

        public string[] LanguageKeys => languageKeys;
        public string[] LanguageNames => languageNames;

        public string LanguageKey {
            get => languageKey;
            set {
                languageKey = value;
                ChangeLanguage();
            }
        }

        void OnEnable() {
            if (afterFirstRun) return;
            afterFirstRun = true;
            if (VRCJson.TryDeserializeFromJson(languageJson, out DataToken rawLanguages) && rawLanguages.TokenType == TokenType.DataDictionary) {
                var uiLanguage = VRCPlayerApi.GetCurrentLanguage();
                languages = rawLanguages.DataDictionary;
                languageKeys = new string[languages.Count];
                languageNames = new string[languages.Count];
                var keys = languages.GetKeys();
                var localTimeZone = TimeZoneInfo.Local.Id;
                int hasMatchingLanguage = 0;
                for (int i = 0, count = keys.Count; i < count; i++) {
                    var currentLanguageKey = keys[i].String;
                    languageKeys[i] = currentLanguageKey;
                    if (languages.TryGetValue(currentLanguageKey, TokenType.DataDictionary, out DataToken language)) {
                        if (hasMatchingLanguage < 2 && language.DataDictionary.TryGetValue("_vrclang", out DataToken langToken)) {
                            switch (langToken.TokenType) {
                                case TokenType.String:
                                    if (langToken.String == uiLanguage) {
                                        languageKey = currentLanguageKey;
                                        hasMatchingLanguage = 2;
                                    }
                                    break;
                                case TokenType.DataList:
                                    if (langToken.DataList.Contains(uiLanguage)) {
                                        languageKey = currentLanguageKey;
                                        hasMatchingLanguage = 2;
                                    }
                                    break;
                            }
                        }
                        if (hasMatchingLanguage < 1 && language.DataDictionary.TryGetValue("_timezone", out DataToken timezoneToken))
                            switch (timezoneToken.TokenType) {
                                case TokenType.String:
                                    if (timezoneToken.String == localTimeZone) {
                                        languageKey = currentLanguageKey;
                                        hasMatchingLanguage = 1;
                                    }
                                    break;
                                case TokenType.DataList:
                                    if (timezoneToken.DataList.Contains(localTimeZone)) {
                                        languageKey = currentLanguageKey;
                                        hasMatchingLanguage = 1;
                                    }
                                    break;
                            }
                        if (language.DataDictionary.TryGetValue("_name", TokenType.String, out DataToken nameToken)) {
                            languageNames[i] = nameToken.String;
                            continue;
                        }
                    }
                    languageNames[i] = currentLanguageKey;
                }
                ChangeLanguage();
            } else
                Debug.LogError("Failed to parse language json.");
        }

        public string GetLocale(string key) {
            if (string.IsNullOrEmpty(key)) return "";
            if (currentLanguage == null) return key;
            if (currentLanguage.TryGetValue(key, TokenType.String, out DataToken token))
                return token.String;
            return key;
        }

        void ChangeLanguage() {
            if (!languages.TryGetValue(languageKey, TokenType.DataDictionary, out DataToken currentLanguageWrapped))
                return;
            currentLanguage = currentLanguageWrapped.DataDictionary;
            SendEvent("_OnLanguageChanged");
        }
    }
}