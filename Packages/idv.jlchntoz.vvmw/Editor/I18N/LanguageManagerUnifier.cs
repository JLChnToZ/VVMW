using System.Text;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using VRC.Udon;
using UdonSharpEditor;
using VVMW.ThirdParties.LitJson;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    // Resolve and group all language managers into one game object while building
    public class LanguageManagerUnifier : IProcessSceneWithReport {
        LanguageManager unifiedLanguageManager;
        UdonBehaviour unifiedLanguageManagerUdon;
        HashSet<LanguageManager> languageManagers = new HashSet<LanguageManager>();
        Dictionary<UdonBehaviour, LanguageManager> backingUdonBehaviours = new Dictionary<UdonBehaviour, LanguageManager>();
        List<string> jsonTexts = new List<string>();

        public int callbackOrder => 0;

        public static Dictionary<string, LanguageEntry> ParseFromJson(
            string json,
            List<object> keyStack = null,
            Dictionary<string, string> defaultLanguageMapping = null,
            HashSet<string> allLanguageKeys = null,
            Dictionary<string, LanguageEntry> langMap = null
        ) {
            if (langMap == null) langMap = new Dictionary<string, LanguageEntry>();
            if (keyStack == null) keyStack = new List<object>();
            else keyStack.Clear();
            var reader = new JsonReader(json);
            LanguageEntry currentEntry = null;
            while (reader.Read())
                switch (reader.Token) {
                    case JsonToken.ObjectStart:
                        switch (keyStack.Count) {
                            case 1:
                                if (keyStack[0] is string key && !langMap.TryGetValue(key, out currentEntry))
                                    langMap[key] = currentEntry = new LanguageEntry();
                                break;
                        }
                        keyStack.Add(null);
                        break;
                    case JsonToken.ArrayStart:
                        keyStack.Add(0);
                        break;
                    case JsonToken.ObjectEnd:
                    case JsonToken.ArrayEnd:
                        keyStack.RemoveAt(keyStack.Count - 1);
                        break;
                    case JsonToken.PropertyName:
                        keyStack[keyStack.Count - 1] = reader.Value;
                        break;
                    case JsonToken.String:
                        switch (keyStack.Count) {
                            case 2: {
                                if (currentEntry != null && keyStack[1] is string key) {
                                    var strValue = (string)reader.Value;
                                    switch (key) {
                                        case "_name":
                                            currentEntry.name = strValue;
                                            break;
                                        case "_vrclang":
                                            currentEntry.vrcName = strValue;
                                            break;
                                        case "_timezone":
                                            currentEntry.timezones.Add(strValue);
                                            break;
                                        default:
                                            currentEntry.languages[key] = strValue;
                                            allLanguageKeys?.Add(key);
                                            if (defaultLanguageMapping != null &&
                                                !defaultLanguageMapping.ContainsKey(key))
                                                defaultLanguageMapping[key] = strValue;
                                            break;
                                    }
                                }
                                break;
                            }
                            case 3: {
                                if (currentEntry != null && keyStack[1] is string key && key == "_timezone" && keyStack[2] is int)
                                    currentEntry.timezones.Add(reader.Value.ToString());
                                break;
                            }
                        }
                        goto default;
                    default:
                        if (keyStack.Count > 0 && keyStack[keyStack.Count - 1] is int index)
                            keyStack[keyStack.Count - 1] = index + 1;
                        break;
                }
            return langMap;
        }

        public static string WriteToJson(
            Dictionary<string, LanguageEntry> langMap,
            Dictionary<string, string> defaultLanguageMapping = null,
            bool prettyPrint = false
        ) {
            var sb = new StringBuilder();
            var jsonWriter = new JsonWriter(sb) {
                PrettyPrint = prettyPrint,
            };
            jsonWriter.WriteObjectStart();
            foreach (var kv in langMap) {
                jsonWriter.WritePropertyName(kv.Key);
                jsonWriter.WriteObjectStart();
                var lang = kv.Value;
                if (!string.IsNullOrEmpty(lang.name)) {
                    jsonWriter.WritePropertyName("_name");
                    jsonWriter.Write(lang.name);
                }
                if (!string.IsNullOrEmpty(lang.vrcName)) {
                    jsonWriter.WritePropertyName("_vrclang");
                    jsonWriter.Write(lang.vrcName);
                }
                if (lang.timezones.Count > 0) {
                    jsonWriter.WritePropertyName("_timezone");
                    if (lang.timezones.Count == 1)
                        jsonWriter.Write(lang.timezones[0]);
                    else {
                        jsonWriter.WriteArrayStart();
                        foreach (var timezone in lang.timezones) jsonWriter.Write(timezone);
                        jsonWriter.WriteArrayEnd();
                    }
                }
                foreach (var langEntry in lang.languages) {
                    jsonWriter.WritePropertyName(langEntry.Key);
                    jsonWriter.Write(langEntry.Value);
                }
                if (defaultLanguageMapping != null)
                    foreach (var defaultLang in defaultLanguageMapping)
                        if (!lang.languages.ContainsKey(defaultLang.Key)) {
                            jsonWriter.WritePropertyName(defaultLang.Key);
                            jsonWriter.Write(defaultLang.Value);
                        }
                jsonWriter.WriteObjectEnd();
            }
            jsonWriter.WriteObjectEnd();
            return sb.ToString();
        }

        public void OnProcessScene(Scene scene, BuildReport report) {
            var roots = scene.GetRootGameObjects();
            unifiedLanguageManager = null;
            languageManagers.Clear();
            jsonTexts.Clear();
            GatherAllLanguages(roots);
            CombineJsons();
            RemapLanguageManager(roots);
            RemoveLanguageManagers();
        }

        void GatherAllLanguages(GameObject[] roots) {
            foreach (var languageManager in roots.SelectMany(x => x.GetComponentsInChildren<LanguageManager>(true))) {
                if (unifiedLanguageManager == null && languageManager.tag != "EditorOnly") {
                    unifiedLanguageManager = languageManager;
                    unifiedLanguageManagerUdon = UdonSharpEditorUtility.GetBackingUdonBehaviour(languageManager);
                }
                languageManagers.Add(languageManager);
                using (var so = new SerializedObject(languageManager)) {
                    var languagePack = so.FindProperty("languageJsonFiles");
                    for (int i = 0, count = languagePack.arraySize; i < count; i++) {
                        var textAsset = languagePack.GetArrayElementAtIndex(i).objectReferenceValue as TextAsset;
                        if (textAsset != null) jsonTexts.Add(textAsset.text);
                    }
                    var additionalJson = so.FindProperty("languageJson");
                    if (!string.IsNullOrEmpty(additionalJson.stringValue))
                        jsonTexts.Add(additionalJson.stringValue);
                }
            }
        }

        void CombineJsons() {
            var langMap = new Dictionary<string, LanguageEntry>();
            var defaultLanguageMapping = new Dictionary<string, string>();
            var keyStack = new List<object>();
            var allLanguageKeys = new HashSet<string>();
            foreach (var json in jsonTexts)
                ParseFromJson(json, keyStack, defaultLanguageMapping, allLanguageKeys, langMap);
            var combinedJson = WriteToJson(langMap, defaultLanguageMapping);
            using (var so = new SerializedObject(unifiedLanguageManager)) {
                var jsonRefs = so.FindProperty("languageJsonFiles");
                jsonRefs.ClearArray();
                var languageJson = so.FindProperty("languageJson");
                languageJson.stringValue = combinedJson;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            UdonSharpEditorUtility.CopyProxyToUdon(unifiedLanguageManager);
            jsonTexts.Clear();
        }

        void RemapLanguageManager(GameObject[] roots) {
            foreach (var ub in roots.SelectMany(x => x.GetComponentsInChildren<UdonBehaviour>(true))) {
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(ub)) {
                    var usharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(ub);
                    bool hasModified = false;
                    using (var so = new SerializedObject(usharpBehaviour)) {
                        var iterator = so.GetIterator();
                        while (iterator.NextVisible(true)) {
                            if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (iterator.objectReferenceValue is UdonBehaviour udon) {
                                if (udon == unifiedLanguageManagerUdon) continue;
                                iterator.objectReferenceValue = unifiedLanguageManagerUdon;
                                hasModified = true;
                            } else {
                                if (iterator.objectReferenceValue is LanguageManager languageManager && languageManager != null) {
                                    // We find a language manager reference, further logic appears after the if-else block
                                } else if (iterator.objectReferenceValue == null) {
                                    // If the reference is null, we first firgure out what is the field type of the property.
                                    var field = usharpBehaviour.GetType().GetField(iterator.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                                    if (field == null || !typeof(LanguageManager).IsAssignableFrom(field.FieldType)) continue;
                                    languageManager = field.GetValue(usharpBehaviour) as LanguageManager;
                                } else
                                    continue; // Anything else, skip
                                if (unifiedLanguageManager == languageManager)
                                    continue;
                                iterator.objectReferenceValue = unifiedLanguageManager;
                                hasModified = true;
                            }
                        }
                        if (hasModified) so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (hasModified) UdonSharpEditorUtility.CopyProxyToUdon(usharpBehaviour);
                } else {
                    var programSource = ub.programSource;
                    if (programSource == null) continue;
                    var serializedProgramAsset = programSource.SerializedProgramAsset;
                    if (serializedProgramAsset == null) continue;
                    var program = serializedProgramAsset.RetrieveProgram();
                    if (program == null) continue;
                    var symbolTable = program.SymbolTable;
                    if (symbolTable == null) continue;
                    foreach (var symbolName in symbolTable.GetSymbols()) {
                        if (!ub.TryGetProgramVariable(symbolName, out var variable)) continue;
                        if (!(variable is UdonBehaviour udon)) continue;
                        if (!backingUdonBehaviours.TryGetValue(udon, out var languageManager))
                            continue;
                        if (languageManager == unifiedLanguageManager)
                            continue;
                        ub.SetProgramVariable(symbolName, unifiedLanguageManager);
                    }
                }
            }
        }

        void RemoveLanguageManagers() {
            foreach (var languageManager in languageManagers) {
                if (languageManager == unifiedLanguageManager) continue;
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(languageManager);
                DestroyImmediate(languageManager);
                if (udon != null) DestroyImmediate(udon);
            }
            languageManagers.Clear();
        }

    }

    public class LanguageEntry {
        public string name;
        public string vrcName;
        public readonly List<string> timezones = new List<string>();
        public readonly Dictionary<string, string> languages = new Dictionary<string, string>();
    }
}