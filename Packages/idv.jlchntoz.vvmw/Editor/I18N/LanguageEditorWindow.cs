using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using JLChnToZ.VRC.VVMW.Editors;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    public class LanguageEditorWindow : EditorWindow {
        static EditorI18N i18n;
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
        GUIContent plusIconContent, minusIconContent;
        float viewWidth;
        [NonSerialized] bool hasInit;

        public static LanguageEditorWindow Open(LanguageManager languageManager) {
            if (languageManager == null) return null;
            var window = GetWindow<LanguageEditorWindow>();
            window.LanguageManager = languageManager;
            window.Show();
            window.RefreshAll();
            return window;
        }

        void OnEnable() {
            if (i18n == null) i18n = EditorI18N.Instance;
            VVMWEditorBase.UpdateTitle(titleContent, "LanguageEditor.title");
            titleContent = titleContent; // Trigger update title
            if (hasInit) return;
            hasInit = true;
            textContent = new GUIContent();
            langListSelect = new ReorderableList(langList, typeof(string), false, false, false, false) {
                drawElementCallback = DrawLangList,
                onSelectCallback = OnLangSelect,
                showDefaultBackground = false,
                elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                headerHeight = 0,
                footerHeight = 0,
            };
            langKeySelect = new ReorderableList(allKeysList, typeof((string, string)), false, false, false, false) {
                drawElementCallback = DrawLangKeys,
                elementHeightCallback = MeasureLangListElementHeight,
                showDefaultBackground = false,
                headerHeight = 0,
                footerHeight = 0,
            };
            wrapTextAreaStyle = new GUIStyle(EditorStyles.textArea) {
                wordWrap = true,
            };
            wrapBoldTextAreaStyle = new GUIStyle(wrapTextAreaStyle) {
                fontStyle = FontStyle.Bold,
            };
            plusIconContent = EditorGUIUtility.IconContent("Toolbar Plus", "Add");
            minusIconContent = EditorGUIUtility.IconContent("Toolbar Minus", "Remove");
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
            langKeySelect.index = -1;
            langListSelect.index = -1;
            selectedEntry = null;
            selectedDefaultEntry = null;
            if (LanguageManager == null) return;
            using (var so = new SerializedObject(LanguageManager)) {
                Load(so.FindProperty("languageJsonFiles"));
                var additionalJson = so.FindProperty("languageJson");
                if (!string.IsNullOrEmpty(additionalJson.stringValue))
                    LanguageManager.ParseFromJson(
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
                    LanguageManager.ParseFromJson(
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
                additionalJson.stringValue = LanguageManager.WriteToJson(currentLanguageMap, prettyPrint: true).Trim();
                so.ApplyModifiedProperties();
            }
        }

        void OnGUI() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    LanguageManager = EditorGUILayout.ObjectField(LanguageManager, typeof(LanguageManager), true) as LanguageManager;
                    if (changed.changed) RefreshAll();
                }
                if (GUILayout.Button(i18n.GetLocalizedContent("LanguageEditor.reload"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) RefreshAll();
                if (GUILayout.Button(i18n.GetLocalizedContent("LanguageEditor.save"), EditorStyles.toolbarButton, GUILayout.ExpandWidth(false))) Save();
                GUILayout.FlexibleSpace();
            }
            using (new EditorGUILayout.HorizontalScope()) {
                using (var vert = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(Mathf.Min(400, position.width / 2)))) {
                    if (LanguageManager == null)
                        EditorGUILayout.HelpBox(i18n.GetOrDefault("LanguageEditor.select_handler_message"), MessageType.Info);
                    else {
                        using (var scroll = new EditorGUILayout.ScrollViewScope(langViewPosition, GUI.skin.box)) {
                            langViewPosition = scroll.scrollPosition;
                            langListSelect.DoLayoutList();
                            GUILayout.FlexibleSpace();
                        }
                        using (new EditorGUILayout.HorizontalScope()) {
                            addLanguageTempString = EditorGUILayout.TextField(i18n.GetLocalizedContent("LanguageEditor.add"), addLanguageTempString);
                            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addLanguageTempString)))
                                if (GUILayout.Button(plusIconContent, EditorStyles.miniLabel, GUILayout.ExpandWidth(false))) {
                                    if (currentLanguageMap.ContainsKey(addLanguageTempString))
                                        i18n.DisplayLocalizedDialog1("LanguageEditor.language_exists_message");
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
                        EditorGUILayout.HelpBox(i18n.GetOrDefault("LanguageEditor.select_language_message"), MessageType.Info);
                    else {
                        using (var scroll = new EditorGUILayout.ScrollViewScope(langKeyPosition, GUI.skin.box)) {
                            langKeyPosition = scroll.scrollPosition;
                            var name = selectedEntry?.name ?? selectedDefaultEntry?.name;
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                name = EditorGUILayout.TextField(i18n.GetLocalizedContent("LanguageEditor.native_name"), name);
                                if (changed.changed) GetOrCreateLanguageEntry().name = name;
                            }
                            var vrcName = selectedEntry?.vrcName ?? selectedDefaultEntry?.vrcName;
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                vrcName = EditorGUILayout.TextField(i18n.GetLocalizedContent("LanguageEditor.vrc_name"), vrcName);
                                if (changed.changed) GetOrCreateLanguageEntry().vrcName = vrcName;
                            }
                            DrawTimeZones();
                            EditorGUILayout.Space();
                            viewWidth = vert.rect.width;
                            langKeySelect.DoLayoutList();
                            GUILayout.FlexibleSpace();
                        }
                        using (new EditorGUILayout.HorizontalScope()) {
                            addLanguageKeyTempString = EditorGUILayout.TextField(addLanguageKeyTempString, GUILayout.Width(EditorGUIUtility.labelWidth));
                            addLanguageTempValueString = EditorGUILayout.TextArea(addLanguageTempValueString, wrapTextAreaStyle);
                            using (new EditorGUI.DisabledScope(string.IsNullOrEmpty(addLanguageKeyTempString)))
                                if (GUILayout.Button(plusIconContent, EditorStyles.miniLabel, GUILayout.ExpandWidth(false))) {
                                    if (selectedEntry != null && selectedEntry.languages.ContainsKey(addLanguageKeyTempString))
                                        i18n.DisplayLocalizedDialog1("LanguageEditor.key_exists_message");
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
            var evt = Event.current;
            if (evt.type == EventType.KeyUp) {
                switch (evt.keyCode) {
                    case KeyCode.S: if (evt.control) { Save(); evt.Use(); } break;
                    case KeyCode.R: if (evt.control) { RefreshAll(); evt.Use(); } break;
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
            bool canRemove = currentLanguageMap.ContainsKey(value);
            var valueRect = rect;
            valueRect.height -= EditorGUIUtility.standardVerticalSpacing;
            Vector2 removeButonSize = default;
            if (canRemove) {
                removeButonSize = EditorStyles.miniLabel.CalcSize(minusIconContent);
                valueRect.width -= removeButonSize.x;
            }
            EditorGUI.LabelField(valueRect, value, currentLanguageMap.ContainsKey(value) ? EditorStyles.boldLabel : EditorStyles.label);
            if (canRemove) {
                var removeButtonRect = rect;
                removeButtonRect.xMin = valueRect.xMax;
                removeButtonRect.size = removeButonSize;
                if (GUI.Button(removeButtonRect, minusIconContent, EditorStyles.miniLabel)) {
                    currentLanguageMap.Remove(value);
                    OnLangSelect();
                }
            }
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

        void DrawTimeZones() {
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.LabelField(i18n.GetLocalizedContent("LanguageEditor.timezone"), GUILayout.Width(EditorGUIUtility.labelWidth));
                foreach (var tz in allTimeZones)
                    using (new EditorGUI.DisabledScope(defaultTimeZones.Contains(tz))) {
                        textContent.text = tz;
                        EditorGUILayout.LabelField(textContent, GUILayout.Width(EditorStyles.label.CalcSize(textContent).x));
                        if (GUILayout.Button(minusIconContent, EditorStyles.miniLabel, GUILayout.ExpandWidth(false))) {
                            allTimeZones.Remove(tz);
                            if (selectedEntry != null) selectedEntry.timezones.Remove(tz);
                            break;
                        }
                    }
                GUILayout.FlexibleSpace();
                addTimeZoneTempString = EditorGUILayout.TextField(addTimeZoneTempString, GUILayout.ExpandWidth(false));
                if (GUILayout.Button(plusIconContent, EditorStyles.miniLabel, GUILayout.ExpandWidth(false))) {
                    if (!allTimeZones.Add(addTimeZoneTempString))
                        i18n.DisplayLocalizedDialog1("LanguageEditor.tz_exists_message");
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
                            i18n.DisplayLocalizedDialog1("LanguageEditor.key_exists_message");
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
            bool canRemove = selectedEntry != null && selectedEntry.languages.ContainsKey(key);
            Vector2 removeButonSize = default;
            if (canRemove) {
                removeButonSize = EditorStyles.miniLabel.CalcSize(minusIconContent);
                valueRect.width -= removeButonSize.x;
            }
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
            if (canRemove) {
                var removeButtonRect = rect;
                removeButtonRect.xMin = valueRect.xMax;
                removeButtonRect.size = removeButonSize;
                if (GUI.Button(removeButtonRect, minusIconContent, EditorStyles.miniLabel)) OnRemoveKey(key);
            }
        }

        float MeasureLangListElementHeight(int index) {
            var key = allKeysList[index];
            textContent.text = GetValue(key, out bool isModified);
            return (isModified ? wrapBoldTextAreaStyle : wrapTextAreaStyle).CalcHeight(textContent, viewWidth - EditorGUIUtility.labelWidth) + EditorGUIUtility.standardVerticalSpacing;
        }

        void OnRemoveKey(string key) {
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