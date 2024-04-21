using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(FrontendHandler))]
    public class FrontendHandlerEditor : VVMWEditorBase {
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
            if (coreProperty.objectReferenceValue == null) EditorGUILayout.HelpBox("Core is not assigned.", MessageType.Error);
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
            if (GUILayout.Button("Edit Playlists..."))
                PlayListEditorWindow.StartEditPlayList(target as FrontendHandler);
            var rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight);
            var tempContent = Utils.GetTempContent("Default Playlist");
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
                    EditorGUILayout.HelpBox("You cannot set default URL in core and mark a playlist to be autoplayed at the same time.", MessageType.Warning);
                    using (new EditorGUILayout.HorizontalScope()) {
                        GUILayout.FlexibleSpace();
                        if (GUILayout.Button("Clear Default URL", EditorStyles.miniButton, GUILayout.ExpandWidth(false))) {
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
                loopMode = (LoopMode)EditorGUILayout.EnumPopup("Default Repeat Mode", loopMode);
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
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(lockedProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Label("This function designed to work with Udon Auth,", GUILayout.ExpandWidth(false));
                if (GUILayout.Button("Learn more", EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
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
            if (queueListOffset > 0) playListNames[0] = "<Queue List>";
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