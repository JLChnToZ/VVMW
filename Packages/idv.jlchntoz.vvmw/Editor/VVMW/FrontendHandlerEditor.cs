using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(FrontendHandler))]
    public class FrontendHandlerEditor : VVMWEditorBase {
        static string[] loopModeNames = new string[3];
        SerializedProperty coreProperty;
        SerializedProperty lockedProperty;
        SerializedProperty defaultLoopProperty;
        SerializedProperty defaultShuffleProperty;
        SerializedProperty enableQueueListProperty;
        SerializedProperty historySizeProperty;
        SerializedProperty defaultPlayListIndexProperty;
        SerializedProperty playListTitlesProperty;
        SerializedProperty autoPlayProperty;
        SerializedProperty autoPlayDelayProperty;
        SerializedProperty targetsProperty;
        SerializedProperty seedRandomBeforeShuffleProperty;
        SerializedReorderableList targetsPropertyList;
        SerializedObject coreSerializedObject;
        string[] playListNames;

        protected override void OnEnable() {
            base.OnEnable();
            coreProperty = serializedObject.FindProperty("core");
            lockedProperty = serializedObject.FindProperty("locked");
            defaultLoopProperty = serializedObject.FindProperty("defaultLoop");
            defaultShuffleProperty = serializedObject.FindProperty("defaultShuffle");
            enableQueueListProperty = serializedObject.FindProperty("enableQueueList");
            historySizeProperty = serializedObject.FindProperty("historySize");
            defaultPlayListIndexProperty = serializedObject.FindProperty("defaultPlayListIndex");
            playListTitlesProperty = serializedObject.FindProperty("playListTitles");
            autoPlayProperty = serializedObject.FindProperty("autoPlay");
            autoPlayDelayProperty = serializedObject.FindProperty("autoPlayDelay");
            targetsProperty = serializedObject.FindProperty("targets");
            seedRandomBeforeShuffleProperty = serializedObject.FindProperty("seedRandomBeforeShuffle");
            targetsPropertyList = new SerializedReorderableList(targetsProperty);
            playListNames = null;
            PlayListEditorWindow.OnFrontendUpdated += OnFrontEndUpdated;
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (coreSerializedObject != null) {
                coreSerializedObject.Dispose();
                coreSerializedObject = null;
            }
            PlayListEditorWindow.OnFrontendUpdated -= OnFrontEndUpdated;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            serializedObject.Update();
            EditorGUILayout.PropertyField(coreProperty);
            if (coreProperty.objectReferenceValue == null) EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core:empty_message"), MessageType.Error);
            if (coreSerializedObject == null || coreSerializedObject.targetObject != coreProperty.objectReferenceValue) {
                coreSerializedObject?.Dispose();
                coreSerializedObject = coreProperty.objectReferenceValue != null ? new SerializedObject(coreProperty.objectReferenceValue) : null;
            }
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                EditorGUILayout.PropertyField(enableQueueListProperty);
                if (changed.changed &&
                    !enableQueueListProperty.boolValue &&
                    defaultPlayListIndexProperty.intValue == 0 &&
                    playListTitlesProperty.arraySize > 0)
                    defaultPlayListIndexProperty.intValue = 1;
            }
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                EditorGUILayout.PropertyField(historySizeProperty);
                if (changed.changed && historySizeProperty.intValue < 0) historySizeProperty.intValue = 0;
            }
            if (playListNames == null || playListNames.Length != playListTitlesProperty.arraySize + (enableQueueListProperty.boolValue ? 1 : 0))
                UpdatePlayListNames();
            if (GUILayout.Button(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.editPlaylist")))
                PlayListEditorWindow.StartEditPlayList(target as FrontendHandler);
            var rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight);
            var tempContent = Utils.GetTempContent(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.defaultPlaylist"));
            using (new EditorGUI.DisabledScope(playListNames.Length == 0))
            using (new EditorGUI.PropertyScope(rect, tempContent, defaultPlayListIndexProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                rect = EditorGUI.PrefixLabel(rect, tempContent);
                var index = defaultPlayListIndexProperty.intValue;
                bool forceUpdate = false;
                if (!enableQueueListProperty.boolValue) index--;
                if (index < 0 || index >= playListNames.Length) {
                    index = 0;
                    forceUpdate = defaultPlayListIndexProperty.intValue != index;
                }
                index = EditorGUI.Popup(rect, index, playListNames);
                if (forceUpdate || changed.changed) {
                    if (!enableQueueListProperty.boolValue && playListNames.Length > 0) index++;
                    defaultPlayListIndexProperty.intValue = index;
                }
            }
            if (coreSerializedObject != null && defaultPlayListIndexProperty.intValue > 0) {
                var url = coreSerializedObject.FindProperty("defaultUrl.url");
                if (url != null && !string.IsNullOrEmpty(url.stringValue)) {
                    EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.default_url_conflict_message"), MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.default_url_conflict_message:confrim"), EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
                            url.stringValue = string.Empty;
                            coreSerializedObject.ApplyModifiedProperties();
                        }
                    }
                }
            }
            EditorGUILayout.PropertyField(autoPlayProperty);
            if (autoPlayProperty.boolValue) {
                EditorGUILayout.PropertyField(autoPlayDelayProperty);
                if (autoPlayDelayProperty.floatValue < 0) autoPlayDelayProperty.floatValue = 0;
            }
            EditorGUILayout.Space();
            var loopMode = LoopMode.None;
            bool hasLoopOne = false;
            var core = coreProperty.objectReferenceValue as Core;
            if (core != null) {
                hasLoopOne = core.Loop;
                if (hasLoopOne) loopMode = LoopMode.SingleLoop;
            }
            if (defaultLoopProperty.boolValue) loopMode = LoopMode.RepeatAll;
            using (var changeCheck = new EditorGUI.ChangeCheckScope()) {
                loopModeNames[0] = i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.loopMode.none");
                loopModeNames[1] = i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.loopMode.singleLoop");
                loopModeNames[2] = i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.loopMode.repeatAll");
                loopMode = (LoopMode)EditorGUILayout.Popup(
                    i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.loopMode"),
                    (int)loopMode, loopModeNames
                );
                if (changeCheck.changed || (hasLoopOne && loopMode == LoopMode.RepeatAll)) {
                    if (core != null)
                        using (var so = new SerializedObject(core)) {
                            so.FindProperty("loop").boolValue = loopMode == LoopMode.SingleLoop;
                            so.ApplyModifiedProperties();
                        }
                    defaultLoopProperty.boolValue = loopMode == LoopMode.RepeatAll;
                }
            }
            EditorGUILayout.PropertyField(defaultShuffleProperty);
            EditorGUILayout.PropertyField(seedRandomBeforeShuffleProperty);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(lockedProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Label(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.locked:hint"), GUILayout.ExpandWidth(false));
                if (GUILayout.Button(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.locked:hint_link"), EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                    Application.OpenURL("https://xtl.booth.pm/items/3826907");
            }
            EditorGUILayout.Space();
            targetsPropertyList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void OnFrontEndUpdated(FrontendHandler handler) {
            if (handler != target) return;
            UpdatePlayListNames();
        }

        void UpdatePlayListNames() {
            int queueListOffset = enableQueueListProperty.boolValue ? 1 : 0;
            int requiredSize = playListTitlesProperty.arraySize + queueListOffset;
            if (playListNames == null || playListNames.Length != requiredSize)
                playListNames = new string[requiredSize];
            if (queueListOffset > 0) playListNames[0] = i18n.GetOrDefault("JLChnToZ.VRC.VVMW.FrontendHandler.queueList");
            for (int i = queueListOffset; i < requiredSize; i++)
                playListNames[i] = playListTitlesProperty.GetArrayElementAtIndex(i - queueListOffset).stringValue;
        }

        struct PlayList {
            public string title;
            public List<PlayListEntry> entries;
        }

        struct PlayListEntry {
            public string title;
            public string url;
            public string urlForQuest;
            public int playerIndex;
        }

        enum LoopMode {
            None,
            SingleLoop,
            RepeatAll,
        }
    }
}