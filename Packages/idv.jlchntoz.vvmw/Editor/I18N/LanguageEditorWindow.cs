using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    public class LanguageEditorWindow : EditorWindow {
        public LanguageManager LanguageManager { get; private set; }
        readonly Dictionary<string, LanguageEntry>
            defaultLanguageMap = new Dictionary<string, LanguageEntry>(),
            currentLanguageMap = new Dictionary<string, LanguageEntry>();
        Vector2 langViewPosition, langKeyPosition;
        readonly List<string>
            langList = new List<string>(),
            allKeysList = new List<string>();
        readonly HashSet<string> allKeys = new HashSet<string>(), allTimeZones = new HashSet<string>(), defaultTimeZones = new HashSet<string>();
        ReorderableList langListSelect, langKeySelect;
        LanguageEntry selectedEntry, selectedDefaultEntry;
        GUIContent textContent;
        string addLanguageTempString = "", addLanguageKeyTempString = "", addLanguageTempValueString = "", addTimeZoneTempString = "";
        GUIStyle wrapTextAreaStyle, wrapBoldTextAreaStyle;

        public static LanguageEditorWindow Open(LanguageManager languageManager) {
            if (languageManager == null) return null;
            var window = GetWindow<LanguageEditorWindow>();
            window.LanguageManager = languageManager;
            window.titleContent = new GUIContent("Language Editor");
            window.Show();
            window.RefreshAll();
            return window;
        }

        void Awake() {
            textContent = new GUIContent();
            langListSelect = new ReorderableList(langList, typeof(string), false, false, false, true) {
                showDefaultBackground = false,
                drawElementCallback = DrawLangList,
                onSelectCallback = OnLangSelect,
                onCanRemoveCallback = CanRemoveLang,
                onRemoveCallback = OnRemoveLang,
                elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                headerHeight = 0,
            };
            langKeySelect = new ReorderableList(allKeysList, typeof((string, string)), false, false, false, true) {
                showDefaultBackground = false,
                drawElementCallback = DrawLangKeys,
                onCanRemoveCallback = CanRemoveKey,
                onRemoveCallback = OnRemoveKey,
                elementHeightCallback = MeasureLangListElementHeight,
                headerHeight = 0,
            };
            wrapTextAreaStyle = new GUIStyle(EditorStyles.textArea) {
                wordWrap = true,
            };
            wrapBoldTextAreaStyle = new GUIStyle(wrapTextAreaStyle) {
                fontStyle = FontStyle.Bold,
            };
        }

        void OnDisable() {
            Save();
            LanguageManager = null;
        }

        void RefreshAll() {
            defaultLanguageMap.Clear();
            currentLanguageMap.Clear();
            langList.Clear();
            allKeysList.Clear();
            allKeys.Clear();
            allTimeZones.Clear();
            defaultTimeZones.Clear();
            if (LanguageManager == null) return;
            using (var so = new SerializedObject(LanguageManager)) {
                Load(so.FindProperty("languageJsonFiles"));
                var additionalJson = so.FindProperty("languageJson");
                if (!string.IsNullOrEmpty(additionalJson.stringValue))
                    LanguageManagerUnifier.ParseFromJson(
                        additionalJson.stringValue, null, null, allKeys, currentLanguageMap
                    );
            }
            RefreshIndexes();
        }

        public void RefreshJsonLists() {
            if (LanguageManager == null) return;
            using (var so = new SerializedObject(LanguageManager))
                Load(so.FindProperty("languageJsonFiles"));
            RefreshIndexes(processCurrentMap: true);
            OnLangSelect();
        }

        void Load(SerializedProperty langPacks) {
            defaultLanguageMap.Clear();
            allKeys.Clear();
            var keyStack = new List<object>();
            for (int i = 0, count = langPacks.arraySize; i < count; i++) {
                var textAsset = langPacks.GetArrayElementAtIndex(i).objectReferenceValue as TextAsset;
                if (textAsset != null)
                    LanguageManagerUnifier.ParseFromJson(
                        textAsset.text, keyStack, null, allKeys, defaultLanguageMap
                    );
            }
        }

        void RefreshIndexes(bool processDefaultMap = false, bool processCurrentMap = false) {
            var langs = new HashSet<string>(defaultLanguageMap.Keys);
            langs.UnionWith(currentLanguageMap.Keys);
            langList.Clear();
            langList.AddRange(langs);
            if (processDefaultMap)
                foreach (var lang in defaultLanguageMap.Values)
                    allKeys.UnionWith(lang.languages.Keys);
            if (processCurrentMap)
                foreach (var lang in currentLanguageMap.Values)
                    allKeys.UnionWith(lang.languages.Keys);
            allKeysList.Clear();
            allKeysList.AddRange(allKeys);
        }

        void Save() {
            if (LanguageManager == null) return;
            using (var so = new SerializedObject(LanguageManager)) {
                var additionalJson = so.FindProperty("languageJson");
                var keyStack = new List<object>();
                additionalJson.stringValue = LanguageManagerUnifier.WriteToJson(currentLanguageMap, prettyPrint: true).Trim();
                so.ApplyModifiedProperties();
            }
        }

        void OnGUI() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    LanguageManager = EditorGUILayout.ObjectField(LanguageManager, typeof(LanguageManager), true) as LanguageManager;
                    if (changed.changed) RefreshAll();
                }
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) RefreshAll();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) Save();
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope()) {
                using (var vert = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(Mathf.Min(400, position.width / 2)))) {
                    if (LanguageManager == null)
                        EditorGUILayout.HelpBox("Please select a Language Handler first.", MessageType.Info);
                    else {
                        using (var scroll = new EditorGUILayout.ScrollViewScope(langViewPosition, GUI.skin.box)) {
                            langViewPosition = scroll.scrollPosition;
                            langListSelect.DoLayoutList();
                            GUILayout.FlexibleSpace();
                        }
                        using (new EditorGUILayout.HorizontalScope()) {
                            addLanguageTempString = EditorGUILayout.TextField("Add Language", addLanguageTempString);
                            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addLanguageTempString)))
                                if (GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
                                    if (currentLanguageMap.ContainsKey(addLanguageTempString))
                                        EditorUtility.DisplayDialog("Error", "Language already exists.", "OK");
                                    else {
                                        currentLanguageMap.Add(addLanguageTempString, selectedEntry = new LanguageEntry());
                                        langList.Add(addLanguageTempString);
                                        addLanguageTempString = "";
                                        OnLangSelect(langList.Count - 1);
                                    }
                                }
                        }
                    }
                }
                using (var vert = new EditorGUILayout.VerticalScope()) {
                    if (selectedEntry == null && selectedDefaultEntry == null)
                        EditorGUILayout.HelpBox("Please select a language first.", MessageType.Info);
                    else {
                        using (var scroll = new EditorGUILayout.ScrollViewScope(langKeyPosition, GUI.skin.box)) {
                            langKeyPosition = scroll.scrollPosition;
                            var name = selectedEntry?.name ?? selectedDefaultEntry?.name;
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                name = EditorGUILayout.TextField("Native Language Name", name);
                                if (changed.changed) GetOrCreateLanguageEntry().name = name;
                            }
                            var vrcName = selectedEntry?.vrcName ?? selectedDefaultEntry?.vrcName;
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                vrcName = EditorGUILayout.TextField("VRChat Language Name", vrcName);
                                if (changed.changed) GetOrCreateLanguageEntry().vrcName = vrcName;
                            }
                            DrawTimeZones();
                            EditorGUILayout.Space();
                            langKeySelect.DoLayoutList();
                            GUILayout.FlexibleSpace();
                        }
                        using (new EditorGUILayout.HorizontalScope()) {
                            addLanguageKeyTempString = EditorGUILayout.TextField(addLanguageKeyTempString, GUILayout.Width(EditorGUIUtility.labelWidth));
                            addLanguageTempValueString = EditorGUILayout.TextArea(addLanguageTempValueString, wrapTextAreaStyle);
                            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addLanguageKeyTempString)))
                                if (GUILayout.Button("Add", GUILayout.ExpandWidth(false))) {
                                    if (selectedEntry != null && selectedEntry.languages.ContainsKey(addLanguageKeyTempString))
                                        EditorUtility.DisplayDialog("Error", "Key already exists.", "OK");
                                    else {
                                        GetOrCreateLanguageEntry().languages.Add(addLanguageKeyTempString, addLanguageTempValueString);
                                        allKeys.Add(addLanguageKeyTempString);
                                        addLanguageKeyTempString = "";
                                        addLanguageTempValueString = "";
                                    }
                                }
                        }
                    }
                }
            }
        }

        LanguageEntry GetOrCreateLanguageEntry() {
            if (selectedEntry != null) return selectedEntry;
            if (selectedDefaultEntry != null) {
                selectedEntry = new LanguageEntry();
                currentLanguageMap.Add(langList[langListSelect.index], selectedEntry);
                return selectedEntry;
            }
            return null;
        }

        void DrawLangList(Rect rect, int index, bool isActive, bool isFocused) {
            var value = langList[index];
            EditorGUI.LabelField(rect, value, currentLanguageMap.ContainsKey(value) ? EditorStyles.boldLabel : EditorStyles.label);
        }

        void OnLangSelect(ReorderableList list) {
            selectedEntry = null;
            selectedDefaultEntry = null;
            var key = langList[list.index];
            allTimeZones.Clear();
            defaultTimeZones.Clear();
            if (currentLanguageMap.TryGetValue(key, out LanguageEntry entry)) {
                selectedEntry = entry;
                allTimeZones.UnionWith(entry.timezones);
            }
            if (defaultLanguageMap.TryGetValue(key, out entry)) {
                selectedDefaultEntry = entry;
                allTimeZones.UnionWith(entry.timezones);
                defaultTimeZones.UnionWith(entry.timezones);
            }
        }

        void OnLangSelect(int index = -1) {
            if (index < 0) index = langListSelect.index;
            OnLangSelect(langListSelect);
            langListSelect.index = index;
        }

        bool CanRemoveLang(ReorderableList list) =>
            list.index >= 0 &&
            list.index < langList.Count &&
            currentLanguageMap.ContainsKey(langList[list.index]);

        void OnRemoveLang(ReorderableList list) {
            var key = langList[list.index];
            if (currentLanguageMap.ContainsKey(key)) currentLanguageMap.Remove(key);
            OnLangSelect();
        }

        void DrawTimeZones() {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField("Time Zones", GUILayout.Width(EditorGUIUtility.labelWidth));
                foreach (var tz in allTimeZones)
                    using (new EditorGUI.DisabledScope(defaultTimeZones.Contains(tz))) {
                        textContent.text = tz;
                        EditorGUILayout.LabelField(textContent, GUILayout.Width(EditorStyles.label.CalcSize(textContent).x));
                        if (GUILayout.Button("-", GUILayout.ExpandWidth(false))) {
                            allTimeZones.Remove(tz);
                            if (selectedEntry != null) selectedEntry.timezones.Remove(tz);
                            break;
                        }
                    }
                GUILayout.FlexibleSpace();
                addTimeZoneTempString = EditorGUILayout.TextField(addTimeZoneTempString, GUILayout.ExpandWidth(false));
                if (GUILayout.Button("+", GUILayout.ExpandWidth(false))) {
                    if (!allTimeZones.Add(addTimeZoneTempString))
                        EditorUtility.DisplayDialog("Error", "Time Zone already exists.", "OK");
                    else {
                        GetOrCreateLanguageEntry().timezones.Add(addTimeZoneTempString);
                        addTimeZoneTempString = "";
                    }
                }
            }
        }

        void DrawLangKeys(Rect rect, int index, bool isActive, bool isFocused) {
            var key = allKeysList[index];
            var value = GetValue(key, out bool isModified);
            var keyRect = rect;
            keyRect.width = EditorGUIUtility.labelWidth;
            keyRect.height = EditorGUIUtility.singleLineHeight;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newKey = EditorGUI.TextField(keyRect, key);
                if (changed.changed) {
                    if (selectedEntry != null) {
                        if (selectedEntry.languages.ContainsKey(newKey))
                            EditorUtility.DisplayDialog("Error", "Key already exists.", "OK");
                        else {
                            selectedEntry.languages.Remove(key);
                            selectedEntry.languages.Add(newKey, value);
                        }
                    } else
                        GetOrCreateLanguageEntry().languages.Add(key, value);
                    OnLangSelect();
                }
            }
            var valueRect = rect;
            valueRect.xMin = keyRect.xMax;
            valueRect.height -= EditorGUIUtility.standardVerticalSpacing;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                value = EditorGUI.TextArea(valueRect, value, isModified ? wrapBoldTextAreaStyle : wrapTextAreaStyle);
                if (changed.changed) {
                    if (selectedEntry != null)
                        selectedEntry.languages[key] = value;
                    else
                        GetOrCreateLanguageEntry().languages.Add(key, value);
                    OnLangSelect();
                }
            }
        }

        float MeasureLangListElementHeight(int index) {
            var key = allKeysList[index];
            textContent.text = GetValue(key, out bool isModified);
            return (isModified ? wrapBoldTextAreaStyle : wrapTextAreaStyle).CalcHeight(textContent, EditorGUIUtility.currentViewWidth - EditorGUIUtility.labelWidth) + EditorGUIUtility.standardVerticalSpacing;
        }

        bool CanRemoveKey(ReorderableList list) =>
            list.index >= 0 &&
            list.index < allKeysList.Count &&
            selectedEntry != null &&
            selectedEntry.languages.ContainsKey(allKeysList[list.index]);

        void OnRemoveKey(ReorderableList list) {
            var key = allKeysList[list.index];
            if (selectedEntry != null && selectedEntry.languages.Remove(key)) {
                bool isRemains = false;
                foreach (var lang in defaultLanguageMap.Values)
                    if (lang.languages.ContainsKey(key)) {
                        isRemains = true;
                        break;
                    }
                if (!isRemains)
                    foreach (var lang in currentLanguageMap.Values)
                        if (lang.languages.ContainsKey(key)) {
                            isRemains = true;
                            break;
                        }
                if (!isRemains) allKeys.Remove(key);
            }
            OnLangSelect();
        }

        string GetValue(string key, out bool isModified) {
            if (selectedEntry != null && selectedEntry.languages.TryGetValue(key, out string value)) {
                isModified = true;
                return value;
            }
            isModified = false;
            if (selectedDefaultEntry != null && selectedDefaultEntry.languages.TryGetValue(key, out value))
                return value;
            return "";
        }
    }
}