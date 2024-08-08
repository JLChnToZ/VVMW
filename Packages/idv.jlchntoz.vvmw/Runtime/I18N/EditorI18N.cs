using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using VVMW.ThirdParties.LitJson;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.I18N {
    public class EditorI18N : ScriptableObject {
        const string ASSET_PATH = "Packages/idv.jlchntoz.vvmw/Resources/Editor I18N.asset";
        const string PREF_KEY = "vvmw.lang";
        const string DEFAULT_LANGUAGE = "en";
        static EditorI18N instance;
        [SerializeField] TextAsset i18nData;
        string[] languageNames;
        string[] languageKeys;
        readonly Dictionary<string, Dictionary<string, string>> i18nDict = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string, string> alias = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        [NonSerialized] string currentLanguage;

        public string CurrentLanguage {
            get => currentLanguage;
            set {
                currentLanguage = value;
                #if UNITY_EDITOR
                EditorPrefs.SetString(PREF_KEY, value);
                #endif
            }
        }

        public int LanguageIndex {
            get => Array.IndexOf(languageKeys, currentLanguage);
            set => CurrentLanguage = languageKeys[value];
        }

        public string[] LanguageNames => languageNames;

        public static EditorI18N Instance {
            get {
                #if UNITY_EDITOR
                if (instance == null)
                    instance = AssetDatabase.LoadAssetAtPath<EditorI18N>(ASSET_PATH);
                #endif
                if (instance == null) {
                    instance = CreateInstance<EditorI18N>();
                    instance.hideFlags = HideFlags.HideAndDontSave;
                }
                return instance;
            }
        }

        public string this[string key] {
            get {
                if (i18nDict.TryGetValue(currentLanguage, out var langDict) &&
                    langDict.TryGetValue(key, out var value))
                    return value;
                if (i18nDict.TryGetValue(DEFAULT_LANGUAGE, out langDict) &&
                    langDict.TryGetValue(key, out value))
                    return value;
                return null;
            }
        }

        public string GetOrDefault(string key, string defaultValue = null) {
            var value = this[key];
            return string.IsNullOrEmpty(value) ? defaultValue ?? key : value;
        }

        void OnEnable() => Reload();

        public void Reload() {
            if (i18nData == null) return;
            var jsonData = JsonMapper.ToObject(i18nData.text);
            i18nDict.Clear();
            var comparer = StringComparer.OrdinalIgnoreCase;
            languageNames = new string[jsonData.Count];
            languageKeys = new string[jsonData.Count];
            int i = 0;
            foreach (var lang in jsonData.Keys) {
                var langDict = new Dictionary<string, string>(comparer);
                var langData = jsonData[lang];
                foreach (var key in langData.Keys)
                    switch (key) {
                        case "_alias":
                            foreach (JsonData aliasKey in langData[key])
                                alias[(string)aliasKey] = lang;
                            break;
                        case "_name":
                            languageNames[i] = (string)langData[key];
                            break;
                        default:
                            langDict[key] = (string)langData[key];
                            break;
                    }
                i18nDict[lang] = langDict;
                languageKeys[i] = lang;
                i++;
            }
            if (string.IsNullOrEmpty(currentLanguage)) {
                currentLanguage = CultureInfo.CurrentCulture.Name;
                #if UNITY_EDITOR
                currentLanguage = EditorPrefs.GetString(PREF_KEY, currentLanguage);
                #endif
                if (alias.TryGetValue(currentLanguage, out var aliasLang))
                    currentLanguage = aliasLang;
                if (!i18nDict.ContainsKey(currentLanguage))
                    currentLanguage = DEFAULT_LANGUAGE;
            }
        }
    }
}