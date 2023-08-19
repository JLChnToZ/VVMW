using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(FrontendHandler))]
    public class FrontendHandlerEditor : Editor {
        SerializedProperty coreProperty;
        SerializedProperty lockedProperty;
        SerializedProperty defaultLoopProperty;
        SerializedProperty defaultShuffleProperty;
        SerializedProperty enableQueueListProperty;
        SerializedProperty playListTitlesProperty;
        SerializedProperty playListUrlOffsetsProperty;
        SerializedProperty playListUrlsProperty;
        SerializedProperty playListEntryTitlesProperty;
        SerializedProperty playListPlayerIndexProperty;
        SerializedProperty localPlayListIndexProperty;
        SerializedProperty targetsProperty;
        ReorderableListUtils targetsPropertyList;
        readonly List<PlayList> playLists = new List<PlayList>();
        ReorderableList playListView;
        ReorderableList playListEntryView;
        SerializedObject coreSerializedObject;
        PlayList selectedPlayList;
        string[] playerHandlerNames;
        GUIContent tempContent;

        void OnEnable() {
            if (tempContent == null) tempContent = new GUIContent();
            coreProperty = serializedObject.FindProperty("core");
            lockedProperty = serializedObject.FindProperty("locked");
            defaultLoopProperty = serializedObject.FindProperty("defaultLoop");
            defaultShuffleProperty = serializedObject.FindProperty("defaultShuffle");
            enableQueueListProperty = serializedObject.FindProperty("enableQueueList");
            playListTitlesProperty = serializedObject.FindProperty("playListTitles");
            playListUrlOffsetsProperty = serializedObject.FindProperty("playListUrlOffsets");
            playListUrlsProperty = serializedObject.FindProperty("playListUrls");
            playListEntryTitlesProperty = serializedObject.FindProperty("playListEntryTitles");
            playListPlayerIndexProperty = serializedObject.FindProperty("playListPlayerIndex");
            localPlayListIndexProperty = serializedObject.FindProperty("localPlayListIndex");
            targetsProperty = serializedObject.FindProperty("targets");
            targetsPropertyList = new ReorderableListUtils(targetsProperty);
            DeserializePlayList();
            playListView = new ReorderableList(playLists, typeof(PlayList), true, true, true, true) {
                drawHeaderCallback = DrawPlayListHeader,
                drawElementCallback = DrawPlayList,
                onSelectCallback = PlayListSelected,
                onAddCallback = AddPlayList,
                onRemoveCallback = RemovePlayList,
                elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
            };
            UpdateCore();
        }

        public override void OnInspectorGUI() {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();
            EditorGUILayout.PropertyField(coreProperty);
            UpdateCore();
            if (coreProperty.objectReferenceValue == null) EditorGUILayout.HelpBox("Core is not assigned.", MessageType.Error);
            EditorGUILayout.PropertyField(enableQueueListProperty);
            playListView.DoLayoutList();
            EditorGUILayout.HelpBox("You may click on the radio button to mark the play list to be played automatically.", MessageType.Info);
            if (coreSerializedObject != null && localPlayListIndexProperty.intValue > 0) {
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
            if (playListEntryView != null) {
                EditorGUILayout.Space();
                UpdatePlayerHandlerNames();
                playListEntryView.DoLayoutList();
            } else if (playLists.Count > 0)
                EditorGUILayout.HelpBox("You may select a play list to edit here.", MessageType.Info);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button("Reload")) DeserializePlayList();
                if (GUILayout.Button("Save")) SerializePlayList();
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(defaultLoopProperty);
            EditorGUILayout.PropertyField(defaultShuffleProperty);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(lockedProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                GUILayout.Label("This function designed to work with Udon Auth,", GUILayout.ExpandWidth(false));
                if (GUILayout.Button("Learn more", EditorStyles.linkLabel, GUILayout.ExpandWidth(false)))
                    Application.OpenURL("https://xtl.booth.pm/items/3826907");
            }
            EditorGUILayout.Space();
            targetsPropertyList.list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawPlayListHeader(Rect rect) => EditorGUI.LabelField(rect, "Play Lists");

        void DrawPlayList(Rect rect, int index, bool isActive, bool isFocused) {
            var playList = playLists[index];
            var toggleRect = rect;
            toggleRect.width = EditorGUIUtility.singleLineHeight;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var selected = EditorGUI.Toggle(toggleRect, index + 1 == localPlayListIndexProperty.intValue, EditorStyles.radioButton);
                if (changed.changed) localPlayListIndexProperty.intValue = selected ? index + 1 : 0;
            }
            var textFieldRect = rect;
            textFieldRect.xMin = toggleRect.xMax;
            if (playListView.index == index) selectedPlayList = playList;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newTitle = EditorGUI.TextField(textFieldRect, playList.title);
                if (changed.changed) {
                    playList.title = newTitle;
                    playLists[index] = playList;
                }
            }
        }

        void PlayListSelected(ReorderableList list) {
            var index = list.index;
            if (index < 0 || index >= playLists.Count) {
                selectedPlayList = default;
                playListEntryView = null;
                return;
            }
            selectedPlayList = playLists[index];
            playListEntryView = new ReorderableList(selectedPlayList.entries, typeof(PlayListEntry), true, true, true, true) {
                drawHeaderCallback = DrawPlayListEntryHeader,
                drawElementCallback = DrawPlayListEntry,
                elementHeight = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 2,
            };
        }

        void DrawPlayListEntryHeader(Rect rect) => EditorGUI.LabelField(rect, selectedPlayList.title);

        void DrawPlayListEntry(Rect rect, int index, bool isActive, bool isFocused) {
            var labelSize = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 40;
            var entry = selectedPlayList.entries[index];
            var titleRect = rect;
            titleRect.height = EditorGUIUtility.singleLineHeight;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                tempContent.text = "Title";
                titleRect = EditorGUI.PrefixLabel(titleRect, tempContent);
                var newTitle = EditorGUI.TextField(titleRect, entry.title);
                if (changed.changed) {
                    entry.title = newTitle;
                    selectedPlayList.entries[index] = entry;
                }
            }
            var urlRect = rect;
            urlRect.yMin = titleRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            urlRect.height = EditorGUIUtility.singleLineHeight;
            float playerRectWidth = urlRect.width;
            if (playerHandlerNames != null) {
                if (entry.playerIndex >= 0 && entry.playerIndex < playerHandlerNames.Length)
                    tempContent.text = playerHandlerNames[entry.playerIndex];
                else
                    tempContent.text = "";
                playerRectWidth = EditorStyles.popup.CalcSize(tempContent).x;
            }
            urlRect.xMax = urlRect.width - playerRectWidth - 10;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                tempContent.text = "URL";
                urlRect = EditorGUI.PrefixLabel(urlRect, tempContent);
                var newUrl = EditorGUI.TextField(urlRect, entry.url);
                if (changed.changed) {
                    entry.url = newUrl;
                    selectedPlayList.entries[index] = entry;
                }
            }
            if (playerHandlerNames != null) {
                var playerRect = rect;
                playerRect.yMin = titleRect.yMax + EditorGUIUtility.standardVerticalSpacing;
                playerRect.height = EditorGUIUtility.singleLineHeight;
                playerRect.xMin = urlRect.xMax;
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    var newPlayerIndex = EditorGUI.Popup(playerRect, entry.playerIndex, playerHandlerNames);
                    if (changed.changed) {
                        entry.playerIndex = (byte)newPlayerIndex;
                        selectedPlayList.entries[index] = entry;
                    }
                }
            }
            EditorGUIUtility.labelWidth = labelSize;
        }

        void DeserializePlayList() {
            playLists.Clear();
            var playListCount = playListTitlesProperty.arraySize;
            for (int i = 0; i < playListCount; i++) {
                var urlOffset = playListUrlOffsetsProperty.GetArrayElementAtIndex(i).intValue;
                var urlCount = (i < playListCount - 1 ? playListUrlOffsetsProperty.GetArrayElementAtIndex(i + 1).intValue : playListUrlsProperty.arraySize) - urlOffset;
                var playList = new PlayList {
                    title = playListTitlesProperty.GetArrayElementAtIndex(i).stringValue,
                    entries = new List<PlayListEntry>(urlCount)
                };
                for (int j = 0; j < urlCount; j++)
                    playList.entries.Add(new PlayListEntry {
                        title = playListEntryTitlesProperty.GetArrayElementAtIndex(urlOffset + j).stringValue,
                        url = playListUrlsProperty.GetArrayElementAtIndex(urlOffset + j).FindPropertyRelative("url").stringValue,
                        playerIndex = playListPlayerIndexProperty.GetArrayElementAtIndex(urlOffset + j).intValue - 1
                    });
                playLists.Add(playList);
            }
        }

        void SerializePlayList() {
            var temp = new List<PlayListEntry>();
            int offset = 0;
            int count = playLists.Count;
            playListTitlesProperty.arraySize = count;
            playListUrlOffsetsProperty.arraySize = count;
            for (int i = 0; i < count; i++) {
                PlayList playList = playLists[i];
                playListTitlesProperty.GetArrayElementAtIndex(i).stringValue = playList.title;
                playListUrlOffsetsProperty.GetArrayElementAtIndex(i).intValue = offset;
                temp.AddRange(playList.entries);
                offset += playList.entries.Count;
            }
            playListUrlsProperty.arraySize = offset;
            playListEntryTitlesProperty.arraySize = offset;
            playListPlayerIndexProperty.arraySize = offset;
            for (int i = 0; i < offset; i++) {
                var entry = temp[i];
                playListUrlsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue = entry.url;
                playListEntryTitlesProperty.GetArrayElementAtIndex(i).stringValue = entry.title;
                playListPlayerIndexProperty.GetArrayElementAtIndex(i).intValue = entry.playerIndex + 1;
            }
        }

        void AddPlayList(ReorderableList list) {
            playLists.Add(new PlayList {
                title = $"Play List {playLists.Count + 1}",
                entries = new List<PlayListEntry>(),
            });
            playListView.index = playLists.Count - 1;
        }

        void RemovePlayList(ReorderableList list) {
            playLists.RemoveAt(list.index);
            playListView.index = Mathf.Clamp(playListView.index, 0, playLists.Count - 1);
            PlayListSelected(list);
        }

        void UpdateCore() {
            var core = coreProperty.objectReferenceValue;
            if (coreSerializedObject == null || coreSerializedObject.targetObject != core) {
                if (coreSerializedObject != null) coreSerializedObject.Dispose();
                if (core == null) {
                    playerHandlerNames = null;
                    coreSerializedObject = null;
                    return;
                }
                coreSerializedObject = new SerializedObject(core);
            }
        }

        void UpdatePlayerHandlerNames() {
            var playerHandlersProperty = coreSerializedObject.FindProperty("playerHandlers");
            var handlersCount = playerHandlersProperty.arraySize;
            if (playerHandlerNames == null || playerHandlerNames.Length != handlersCount)
                playerHandlerNames = new string[handlersCount];
            for (int i = 0; i < handlersCount; i++) {
                var handler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as VideoPlayerHandler;
                if (handler == null) playerHandlerNames[i] = $"Player {i + 1}";
                else playerHandlerNames[i] = string.IsNullOrEmpty(handler.playerName) ? handler.name : handler.playerName;
            }
        }

        void OnDestroy() {
            if (coreSerializedObject != null) coreSerializedObject.Dispose();
        }

        struct PlayList {
            public string title;
            public List<PlayListEntry> entries;
        }

        struct PlayListEntry {
            public string title;
            public string url;
            public int playerIndex;
        }
    }
}