using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;
using System;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(FrontendHandler))]
    public class FrontendHandlerEditor : VVMWEditorBase {
        SerializedProperty coreProperty;
        SerializedProperty lockedProperty;
        SerializedProperty defaultLoopProperty;
        SerializedProperty defaultShuffleProperty;
        SerializedProperty enableQueueListProperty;
        SerializedProperty defaultPlayListIndexProperty;
        SerializedProperty playListTitlesProperty;
        SerializedProperty autoPlayProperty;
        SerializedProperty targetsProperty;
        SerializedReorderableList targetsPropertyList;
        SerializedObject coreSerializedObject;
        string[] playListNames;
        GUIContent tempContent;

        protected override void OnEnable() {
            base.OnEnable();
            if (tempContent == null) tempContent = new GUIContent();
            coreProperty = serializedObject.FindProperty("core");
            lockedProperty = serializedObject.FindProperty("locked");
            defaultLoopProperty = serializedObject.FindProperty("defaultLoop");
            defaultShuffleProperty = serializedObject.FindProperty("defaultShuffle");
            enableQueueListProperty = serializedObject.FindProperty("enableQueueList");
            defaultPlayListIndexProperty = serializedObject.FindProperty("defaultPlayListIndex");
            playListTitlesProperty = serializedObject.FindProperty("playListTitles");
            autoPlayProperty = serializedObject.FindProperty("autoPlay");
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
            EditorGUILayout.PropertyField(enableQueueListProperty);
            if (playListNames == null || playListNames.Length != playListTitlesProperty.arraySize + 1)
                UpdatePlayListNames();
            if (GUILayout.Button("Edit Play Lists..."))
                PlayListEditorWindow.StartEditPlayList(target as FrontendHandler);
            var rect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight);
            tempContent.text = "Default Play List";
            using (new EditorGUI.PropertyScope(rect, tempContent, playListTitlesProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                rect = EditorGUI.PrefixLabel(rect, tempContent);
                var index = EditorGUI.Popup(rect, defaultPlayListIndexProperty.intValue, playListNames);
                if (changed.changed) defaultPlayListIndexProperty.intValue = index;
            }
            if (coreSerializedObject != null && defaultPlayListIndexProperty.intValue > 0) {
                var url = coreSerializedObject.FindProperty("defaultUrl.url");
                if (url != null && !string.IsNullOrEmpty(url.stringValue)) {
                    EditorGUILayout.HelpBox("You cannot set default URL in core and mark a play list to be autoplayed at the same time.", MessageType.Warning);
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
            if (playListNames == null || playListNames.Length != playListTitlesProperty.arraySize + 1)
                playListNames = new string[playListTitlesProperty.arraySize + 1];
            playListNames[0] = "<Queue List>";
            for (int i = 0; i < playListNames.Length - 1; i++)
                playListNames[i + 1] = playListTitlesProperty.GetArrayElementAtIndex(i).stringValue;
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