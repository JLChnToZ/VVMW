using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using VRC.SDKBase;

using UnityObject = UnityEngine.Object;
using Cysharp.Threading.Tasks;

namespace JLChnToZ.VRC.VVMW.Editors {

    public class PlayListEditorWindow : EditorWindow {
        static GUIContent tempContent;
        FrontendHandler frontendHandler;
        Core loadedCore;
        string[] playerHandlerNames;
        int firstUnityPlayerIndex = -1, firstAvProPlayerIndex = -1;
        readonly List<PlayList> playLists = new List<PlayList>();
        ReorderableList playListView;
        ReorderableList playListEntryView;
        PlayList selectedPlayList;
        bool isDirty;
        Vector2 playListViewScrollPosition, playListEntryViewScrollPosition;
        string ytPlaylistUrl;
        public static event Action<FrontendHandler> OnFrontendUpdated;

        public FrontendHandler FrontendHandler {
            get => frontendHandler;
            set {
                if (frontendHandler == value) return;
                if (isDirty && EditorUtility.DisplayDialog("Unsaved Changes", "There are unsaved changes, do you want to save them?", "Yes", "No"))
                    SerializePlayList();
                frontendHandler = value;
                DeserializePlayList();
            }
        }

        public static void StartEditPlayList(FrontendHandler handler) {
            var window = GetWindow<PlayListEditorWindow>("Play List Editor");
            window.FrontendHandler = handler;
        }

        void OnEnable() {
            if (tempContent == null) tempContent = new GUIContent();
            if (playListView == null) playListView = new ReorderableList(playLists, typeof(PlayList), true, true, true, true) {
                drawHeaderCallback = DrawPlayListHeader,
                drawElementCallback = DrawPlayList,
                onSelectCallback = PlayListSelected,
                onAddCallback = AddPlayList,
                onRemoveCallback = RemovePlayList,
                onReorderCallbackWithDetails = ReorderPlayList,
                elementHeight = EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing,
                showDefaultBackground = false,
            };
        }

        void OnGUI() {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar)) {
                FrontendHandler = EditorGUILayout.ObjectField(FrontendHandler, typeof(FrontendHandler), true) as FrontendHandler;
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)) &&
                    EditorUtility.DisplayDialog("Reload", "Are you sure you want to reload the play list?", "Yes", "No"))
                    DeserializePlayList();
                if (GUILayout.Button("Save", EditorStyles.toolbarButton, GUILayout.ExpandWidth(false)))
                    SerializePlayList();
            }
            var evt = Event.current;
            using (new EditorGUILayout.HorizontalScope()) {
                Rect playListRect, playListEntryRect;
                using (var vert = new EditorGUILayout.VerticalScope(GUILayout.MaxWidth(Mathf.Min(400, position.width / 2)))) {
                    playListRect = vert.rect;
                    using (var scroll = new EditorGUILayout.ScrollViewScope(playListViewScrollPosition, GUI.skin.box)) {
                        if (frontendHandler == null) EditorGUILayout.HelpBox("Please select a Frontend Handler first.", MessageType.Info);
                        else playListView?.DoLayoutList();
                        GUILayout.FlexibleSpace();
                        playListViewScrollPosition = scroll.scrollPosition;
                    }
                }
                using (var vert = new EditorGUILayout.VerticalScope()) {
                    playListEntryRect = vert.rect;
                    using (var scroll = new EditorGUILayout.ScrollViewScope(playListEntryViewScrollPosition, GUI.skin.box)) {
                        if (frontendHandler != null) playListEntryView?.DoLayoutList();
                        GUILayout.FlexibleSpace();
                        playListEntryViewScrollPosition = scroll.scrollPosition;
                    }
                    if (playListEntryView != null) {
                        using (new EditorGUILayout.HorizontalScope()) {
                            ytPlaylistUrl = EditorGUILayout.TextField(ytPlaylistUrl);
                            if (GUILayout.Button("Load Play List from Youtube", GUILayout.ExpandWidth(false))) {
                                FetchPlayList(ytPlaylistUrl).Forget();
                                ytPlaylistUrl = string.Empty;
                            }
                        }
                    }
                }
                switch (evt.type) {
                    case EventType.DragUpdated:
                    case EventType.DragPerform:
                        if (playListRect.Contains(evt.mousePosition)) {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            if (evt.type == EventType.DragPerform) {
                                HandlePlayListObjectDrop(true);
                                evt.Use();
                            }
                        } else if (playListEntryRect.Contains(evt.mousePosition)) {
                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                            if (evt.type == EventType.DragPerform) {
                                HandlePlayListObjectDrop(false);
                                evt.Use();
                            }
                        }
                        break;
                }
            }
            EditorGUILayout.HelpBox("Hint: You can drag the play list game objects from other video players to here to import them.", MessageType.Info);
        }

        void DrawPlayListHeader(Rect rect) => EditorGUI.LabelField(rect, "Play Lists");

        void DrawPlayList(Rect rect, int index, bool isActive, bool isFocused) {
            var playList = playLists[index];
            if (playListView.index == index) selectedPlayList = playList;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newTitle = EditorGUI.TextField(rect, playList.title);
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
                onAddCallback = AddPlayListEntry,
                onRemoveCallback = RemovePlayListEntry,
                onReorderCallbackWithDetails = ReorderPlayListEntry,
                elementHeight = (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing) * 3,
                showDefaultBackground = false,
            };
        }

        void DrawPlayListEntryHeader(Rect rect) => EditorGUI.LabelField(rect, selectedPlayList.title);

        void DrawPlayListEntry(Rect rect, int index, bool isActive, bool isFocused) {
            var labelSize = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 80;
            var entry = selectedPlayList.entries[index];
            var titleRect = rect;
            titleRect.height = EditorGUIUtility.singleLineHeight;
            float playerRectWidth = titleRect.width;
            if (playerHandlerNames != null) {
                if (entry.playerIndex >= 0 && entry.playerIndex < playerHandlerNames.Length)
                    tempContent.text = playerHandlerNames[entry.playerIndex];
                else
                    tempContent.text = "";
                playerRectWidth = EditorStyles.popup.CalcSize(tempContent).x;
            }
            titleRect.xMax = titleRect.width - playerRectWidth - 10;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                tempContent.text = "Title";
                titleRect = EditorGUI.PrefixLabel(titleRect, tempContent);
                var newTitle = EditorGUI.TextField(titleRect, entry.title);
                if (changed.changed) {
                    entry.title = newTitle;
                    selectedPlayList.entries[index] = entry;
                    isDirty = true;
                }
            }
            if (playerHandlerNames != null) {
                var playerRect = rect;
                playerRect.yMin = titleRect.yMin;
                playerRect.height = EditorGUIUtility.singleLineHeight;
                playerRect.xMin = titleRect.xMax;
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    var newPlayerIndex = EditorGUI.Popup(playerRect, entry.playerIndex, playerHandlerNames);
                    if (changed.changed) {
                        entry.playerIndex = (byte)newPlayerIndex;
                        selectedPlayList.entries[index] = entry;
                        isDirty = true;
                    }
                }
            }
            var urlRect = rect;
            urlRect.yMin = titleRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            urlRect.height = EditorGUIUtility.singleLineHeight;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                tempContent.text = "URL (PC)";
                urlRect = EditorGUI.PrefixLabel(urlRect, tempContent);
                var newUrl = EditorGUI.TextField(urlRect, entry.url);
                if (changed.changed) {
                    entry.url = newUrl;
                    selectedPlayList.entries[index] = entry;
                    isDirty = true;
                }
            }
            var urlQuestRect = rect;
            urlQuestRect.yMin = urlRect.yMax + EditorGUIUtility.standardVerticalSpacing;
            urlQuestRect.height = EditorGUIUtility.singleLineHeight;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                tempContent.text = "URL (Quest)";
                urlQuestRect = EditorGUI.PrefixLabel(urlQuestRect, tempContent);
                var newUrl = string.IsNullOrEmpty(entry.urlForQuest) ? entry.url : entry.urlForQuest;
                newUrl = EditorGUI.TextField(urlQuestRect, newUrl);
                if (changed.changed) {
                    entry.urlForQuest = newUrl == entry.url ? string.Empty : newUrl;
                    selectedPlayList.entries[index] = entry;
                    isDirty = true;
                }
            }
            EditorGUIUtility.labelWidth = labelSize;
        }

        void DeserializePlayList() {
            if (frontendHandler == null) {
                playerHandlerNames = null;
                firstUnityPlayerIndex = -1;
                firstAvProPlayerIndex = -1;
                loadedCore = null;
                playLists.Clear();
                isDirty = false;
                return;
            }
            UpdatePlayerHandlerInfos();
            AppendPlaylist(frontendHandler);
            if (playListView != null) {
                playListView.index = -1;
                PlayListSelected(playListView);
            }
        }

        void AppendPlaylist(UnityObject frontendHandler) {
            using (var serializedObject = new SerializedObject(frontendHandler)) {
                var playListTitlesProperty = serializedObject.FindProperty("playListTitles");
                var playListUrlOffsetsProperty = serializedObject.FindProperty("playListUrlOffsets");
                var playListUrlsProperty = serializedObject.FindProperty("playListUrls");
                var playListUrlsQuestProperty = serializedObject.FindProperty("playListUrlsQuest");
                var playListEntryTitlesProperty = serializedObject.FindProperty("playListEntryTitles");
                var playListPlayerIndexProperty = serializedObject.FindProperty("playListPlayerIndex");
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
                            urlForQuest = playListUrlsQuestProperty.GetArrayElementAtIndex(urlOffset + j).FindPropertyRelative("url").stringValue,
                            playerIndex = playListPlayerIndexProperty.GetArrayElementAtIndex(urlOffset + j).intValue - 1
                        });
                    playLists.Add(playList);
                }
            }
        }

        void SerializePlayList() {
            if (frontendHandler == null) return;
            using (var serializedObject = new SerializedObject(frontendHandler)) {
                var temp = new List<PlayListEntry>();
                int offset = 0;
                int count = playLists.Count;
                var playListTitlesProperty = serializedObject.FindProperty("playListTitles");
                var playListUrlOffsetsProperty = serializedObject.FindProperty("playListUrlOffsets");
                var playListUrlsProperty = serializedObject.FindProperty("playListUrls");
                var playListUrlsQuestProperty = serializedObject.FindProperty("playListUrlsQuest");
                var playListEntryTitlesProperty = serializedObject.FindProperty("playListEntryTitles");
                var playListPlayerIndexProperty = serializedObject.FindProperty("playListPlayerIndex");
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
                playListUrlsQuestProperty.arraySize = offset;
                playListEntryTitlesProperty.arraySize = offset;
                playListPlayerIndexProperty.arraySize = offset;
                for (int i = 0; i < offset; i++) {
                    var entry = temp[i];
                    playListUrlsProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue = entry.url;
                    playListUrlsQuestProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue = entry.urlForQuest;
                    playListEntryTitlesProperty.GetArrayElementAtIndex(i).stringValue = entry.title;
                    playListPlayerIndexProperty.GetArrayElementAtIndex(i).intValue = entry.playerIndex + 1;
                }
                serializedObject.ApplyModifiedProperties();
            }
            isDirty = false;
            OnFrontendUpdated?.Invoke(frontendHandler);
        }

        void AddPlayList(ReorderableList list) {
            playLists.Add(new PlayList {
                title = $"Play List {playLists.Count + 1}",
                entries = new List<PlayListEntry>(),
            });
            list.index = playLists.Count - 1;
            isDirty = true;
            PlayListSelected(list);
        }

        void RemovePlayList(ReorderableList list) {
            playLists.RemoveAt(list.index);
            list.index = Mathf.Clamp(playListView.index, 0, playLists.Count - 1);
            isDirty = true;
            PlayListSelected(list);
        }

        void ReorderPlayList(ReorderableList list, int oldIndex, int newIndex) {
            var temp = playLists[oldIndex];
            playLists.RemoveAt(oldIndex);
            playLists.Insert(newIndex, temp);
            list.index = newIndex;
            PlayListSelected(list);
            isDirty = true;
        }

        void AddPlayListEntry(ReorderableList list) {
            ReorderableList.defaultBehaviours.DoAddButton(list);
            isDirty = true;
        }

        void RemovePlayListEntry(ReorderableList list) {
            ReorderableList.defaultBehaviours.DoRemoveButton(list);
            isDirty = true;
        }

        void ReorderPlayListEntry(ReorderableList list, int oldIndex, int newIndex) {
            var temp = selectedPlayList.entries[oldIndex];
            selectedPlayList.entries.RemoveAt(oldIndex);
            selectedPlayList.entries.Insert(newIndex, temp);
            list.index = newIndex;
            isDirty = true;
        }

        void UpdatePlayerHandlerInfos() {
            if (loadedCore == frontendHandler.core) return;
            loadedCore = frontendHandler.core;
            firstUnityPlayerIndex = -1;
            firstAvProPlayerIndex = -1;
            using (var coreSerializedObject = new SerializedObject(frontendHandler.core)) {
                var playerHandlersProperty = coreSerializedObject.FindProperty("playerHandlers");
                var handlersCount = playerHandlersProperty.arraySize;
                if (playerHandlerNames == null || playerHandlerNames.Length != handlersCount)
                    playerHandlerNames = new string[handlersCount];
                for (int i = 0; i < handlersCount; i++) {
                    var handler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as VideoPlayerHandler;
                    if (handler == null) {
                        playerHandlerNames[i] = $"Player {i + 1}";
                        continue;
                    }
                    playerHandlerNames[i] = string.IsNullOrEmpty(handler.playerName) ? handler.name : handler.playerName;
                    if (handler.isAvPro) {
                        if (firstAvProPlayerIndex < 0) firstAvProPlayerIndex = i;
                    } else {
                        if (firstUnityPlayerIndex < 0) firstUnityPlayerIndex = i;
                    }
                }
            }
            if (firstUnityPlayerIndex < 0) firstUnityPlayerIndex = 0;
            if (firstAvProPlayerIndex < 0) firstAvProPlayerIndex = 0;
        }

#region Play List Importers

        void HandlePlayListObjectDrop(bool creaeNewPlayList = false) {
            DragAndDrop.AcceptDrag();
            UpdatePlayerHandlerInfos();
            var queue = new Queue<UnityObject>(DragAndDrop.objectReferences);
            while (queue.Count > 0) {
                var obj = queue.Dequeue();
                if (obj is GameObject gameObject) {
                    foreach (var component in gameObject.GetComponents<MonoBehaviour>())
                        queue.Enqueue(component);
                    continue;
                }
                if (obj is MonoBehaviour mb) {
                    var type = obj.GetType();
                    switch (type.Name) {
                        case "FrontendHandler":
                            switch (type.Namespace) {
                                case "JLChnToZ.VRC.VVMW":
                                    AppendPlaylist(obj);
                                    break;
                            }
                            break;
                        case "PlayList":
                            switch (type.Namespace) {
                                case "Yamadev.YamaStream.Script":
                                    ImportPlayListFromYamaPlayer(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                        case "KinelPlaylistGroupManagerScript":
                            switch (type.Namespace) {
                                case "Kinel.VideoPlayer.Scripts":
                                    ImportPlayListGroupFromKienL(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                        case "KinelPlaylistScript":
                            switch (type.Namespace) {
                                case "Kinel.VideoPlayer.Scripts":
                                    ImportPlayListFromKienL(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                        case "Playlist":
                            switch (type.Namespace) {
                                case "ArchiTech":
                                    ImportPlayListFromProTV(mb, creaeNewPlayList);
                                    break;
                                case "HoshinoLabs.IwaSync3":
                                    ImportPlayListFromIwaSync3(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                        case "PlaylistData":
                            switch (type.Namespace) {
                                case "ArchiTech":
                                    ImportPlayListFromProTV(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                        case "JTPlaylist":
                            switch (type.Namespace) {
                                case "JTPlaylist.Udon":
                                    ImportPlayListFromJT(mb, creaeNewPlayList);
                                    break;
                            }
                            break;
                    }
                }
            }
        }
        PlayList GetOrCreatePlayList(string name, bool forceCreate = false) {
            if (playLists.Count > 0 && !forceCreate)
                return playListView.index >= 0 && playListView.index < playLists.Count ?
                    playLists[playListView.index] :
                    playLists[0];
            var playList = new PlayList {
                title = name,
                entries = new List<PlayListEntry>(),
            };
            playLists.Add(playList);
            playListView.index = 0;
            PlayListSelected(playListView);
            return playList;
        }

        void ImportPlayListFromProTV(dynamic proTVPlayList, bool newPlayList = false) {
            var playList = GetOrCreatePlayList("Imported Play List", newPlayList);
            try {
                VRCUrl[] urls = proTVPlayList.urls;
                VRCUrl[] alts = proTVPlayList.alts;
                string[] titles = proTVPlayList.titles;
                for (int i = 0; i < urls.Length; i++) {
                    var url = urls[i];
                    var alt = alts[i];
                    var title = titles[i];
                    playList.entries.Add(new PlayListEntry {
                        title = title,
                        url = url?.Get() ?? string.Empty,
                        urlForQuest = alt.Get() ?? string.Empty,
                        playerIndex = 0,
                    });
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        void ImportPlayListFromIwaSync3(dynamic iwaSync3PlayList, bool newPlayList = false) {
            var playList = GetOrCreatePlayList("Imported Play List", newPlayList);
            try {
                foreach (var track in ((object)iwaSync3PlayList).GetType().GetField("tracks", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(iwaSync3PlayList)) {
                    var trackMode = (int)track.mode;
                    var title = track.title;
                    var url = track.url;
                    playList.entries.Add(new PlayListEntry {
                        title = title,
                        url = url,
                        urlForQuest = string.Empty,
                        playerIndex = trackMode == 1 ? firstAvProPlayerIndex : firstUnityPlayerIndex,
                    });
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        void ImportPlayListGroupFromKienL(dynamic kinelPlayListGroup, bool newPlayList = false) {
            try {
                string[] playListNames = kinelPlayListGroup.playlists;
                Canvas[] playListCanvases = kinelPlayListGroup.playlistCanvas;
                for (int i = 0; i < playListNames.Length; i++) {
                    var playListName = playListNames[i];
                    var playListCanvas = playListCanvases[i];
                    var playList = GetOrCreatePlayList(playListName, true);
                    try {
                        var kinelPlaylistScript = playListCanvas.GetComponentInParent(Type.GetType("KinelPlaylistScript, Assembly-CSharp"));
                        ImportPlayListFromKienL(kinelPlaylistScript, newPlayList, playListName);
                    } catch (Exception ex) {
                        Debug.LogException(ex);
                    }
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        void ImportPlayListFromKienL(dynamic kinelPlayList, bool newPlayList = false, string playListName = null) {
            var playList = GetOrCreatePlayList(null ?? "Imported Play List", newPlayList);
            try {
                foreach (var videoData in kinelPlayList.videoDatas) {
                    var trackMode = (int)videoData.mode;
                    var title = videoData.title;
                    var url = videoData.url;
                    playList.entries.Add(new PlayListEntry {
                        title = title,
                        url = url,
                        urlForQuest = string.Empty,
                        playerIndex = trackMode == 1 ? firstAvProPlayerIndex : firstUnityPlayerIndex,
                    });
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        void ImportPlayListFromYamaPlayer(dynamic yamaPlayerPlayList, bool newPlayList = false) {
            try {
                var playList = GetOrCreatePlayList(yamaPlayerPlayList.PlayListName, newPlayList);
                foreach (var track in yamaPlayerPlayList.Tracks) {
                    var trackMode = (int)track.Mode;
                    var title = track.Title;
                    var url = track.Url;
                    playList.entries.Add(new PlayListEntry {
                        title = title,
                        url = url,
                        urlForQuest = string.Empty,
                        playerIndex = trackMode == 1 ? firstAvProPlayerIndex : firstUnityPlayerIndex,
                    });
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }

        void ImportPlayListFromJT(dynamic jtPlaylist, bool newPlayList = false) {
            var playList = GetOrCreatePlayList("Imported Play List", newPlayList);
            try {
                string[] titles = jtPlaylist.titles;
                VRCUrl[] urls = jtPlaylist.urls;
                bool[] isLives = jtPlaylist.isLive;
                for (int i = 0; i < urls.Length; i++) {
                    var url = urls[i];
                    var title = titles[i];
                    var isLive = isLives[i];
                    playList.entries.Add(new PlayListEntry {
                        title = title,
                        url = url?.Get() ?? string.Empty,
                        urlForQuest = string.Empty,
                        playerIndex = isLive ? firstAvProPlayerIndex : firstUnityPlayerIndex,
                    });
                }
            } catch (Exception ex) {
                Debug.LogException(ex);
            }
        }
#endregion

        async UniTask FetchPlayList(string url) {
            var ytPlaylist = await YtdlpResolver.GetPlayLists(url);
            var playList = GetOrCreatePlayList("Imported Play List");
            foreach (var entry in ytPlaylist)
                playList.entries.Add(new PlayListEntry {
                    title = entry.title,
                    url = entry.url,
                    urlForQuest = string.Empty,
                    playerIndex = 0,
                });
        }

        [Serializable]
        struct PlayList {
            public string title;
            public List<PlayListEntry> entries;
        }

        [Serializable]
        struct PlayListEntry {
            public string title;
            public string url;
            public string urlForQuest;
            public int playerIndex;
        }
    }
}