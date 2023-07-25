using System;
using UdonSharp;
using UnityEngine;
using VRC.SDK3.Data;

namespace JLChnToZ.VRC.I18N {

    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class LanguageManager : UdonSharpEventSender {
        [SerializeField] TextAsset languagePack;
        DataDictionary languages, currentLanguage;

        [FieldChangeCallback(nameof(LanguageKey))]
        string languageKey = "EN";
        string[] languageKeys;
        string[] languageNames;

        public string[] LanguageKeys => languageKeys;
        public string[] LanguageNames => languageNames;

        public string LanguageKey {
            get => languageKey;
            set {
                languageKey = value;
                ChangeLanguage();
            }
        }

        void Start() {
            if (VRCJson.TryDeserializeFromJson(languagePack.text, out DataToken rawLanguages) && rawLanguages.TokenType == TokenType.DataDictionary) {
                languages = rawLanguages.DataDictionary;
                languageKeys = new string[languages.Count];
                languageNames = new string[languages.Count];
                var keys = languages.GetKeys();
                var localTimeZone = TimeZoneInfo.Local.Id;
                for (int i = 0, count = keys.Count; i < count; i++) {
                    var currentLanguageKey = keys[i].String;
                    languageKeys[i] = currentLanguageKey;
                    if (languages.TryGetValue(currentLanguageKey, TokenType.DataDictionary, out DataToken language)) {
                        if (language.DataDictionary.TryGetValue("_timezone", out DataToken timezoneToken))
                            switch (timezoneToken.TokenType) {
                                case TokenType.String:
                                    if (timezoneToken.String == localTimeZone)
                                        languageKey = currentLanguageKey;
                                    break;
                                case TokenType.DataList:
                                    if (timezoneToken.DataList.Contains(localTimeZone))
                                        languageKey = currentLanguageKey;
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
            }
        }

        public string GetLocale(string key) {
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