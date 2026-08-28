using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Components;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Designer;
using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;
using PooledObjects = JLChnToZ.VRC.Foundation.PooledObjectExtensions;
using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(Core))]
    public class CoreEditor : VVMWEditorBase {
        const string showAllSettingsPrefsKey = "VVMW_CoreEditor_ShowAllSettings";
        const string activeRegionPrefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/Active Region.prefab";
        readonly Dictionary<Core, UdonSharpBehaviour> autoPlayControllers = new Dictionary<Core, UdonSharpBehaviour>();
        readonly Dictionary<AudioSource, (SerializedObject, SerializedObject)> audioSourceComponents = new Dictionary<AudioSource, (SerializedObject, SerializedObject)>();
        readonly Dictionary<ScreenConfigurator, ScreenConfiguratorEditor> screenConfiguratorEditors = new Dictionary<ScreenConfigurator, ScreenConfiguratorEditor>();
        static readonly Dictionary<UnityObject, VideoMaterialEmbeddedEditor> videoMaterialEditors = new Dictionary<UnityObject, VideoMaterialEmbeddedEditor>();
        readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        static readonly string[] materialModeOptions = new string[3];
        static string[] playerNames;
        static readonly GUIContent[] settingsModes = new [] { new GUIContent(), new GUIContent() };
        static PlayerType[] playerTypes;
        SerializedProperty playerHandlersProperty;
        SerializedProperty audioSourcesProperty;
        SerializedProperty defaultUrlProperty;
        SerializedProperty defaultQuestUrlProperty;
        SerializedProperty autoPlayPlayerTypeProperty;
        SerializedProperty syncedProperty;
        SerializedProperty totalRetryCountProperty;
        SerializedProperty fallbackRetryCountProperty;
        SerializedProperty retryDelayProperty;
        SerializedProperty autoPlayDelayProperty;
        SerializedProperty defaultVolumeProperty;
        SerializedProperty defaultMutedProperty;
        SerializedProperty volumeFadeDurationProperty;
        SerializedProperty muteOnOutOfRangeProperty;
        SerializedProperty outOfRangeVolumeProperty;
        SerializedProperty loopProperty;
        SerializedProperty audioLinkProperty;
        SerializedProperty yttlManagerProperty;
        SerializedProperty defaultTextureProperty;
        SerializedProperty screenTargetsProperty;
        SerializedProperty screenTargetModesProperty;
        SerializedProperty screenTargetIndecesProperty;
        SerializedProperty screenTargetPropertyNamesProperty;
        SerializedProperty screenTargetDefaultTexturesProperty;
        SerializedProperty avProPropertyNamesProperty;
        SerializedProperty rtScreenTargetSTsProperty;
        SerializedProperty broadcastScreenTextureProperty;
        SerializedProperty broadcastScreenTextureNameProperty;
        SerializedProperty realtimeGIUpdateIntervalProperty;
        SerializedProperty timeDriftDetectThresholdProperty;
        SerializedProperty urlInputFilterProperty;
#if VRC_ENABLE_PLAYER_PERSISTENCE
        SerializedProperty enablePersistenceProperty;
#endif
        SerializedReorderableList playerHandlersList, targetsList;
        SerializedProperty lowLatencyModeProperty;
        List<bool> screenTargetVisibilityState;
        Editor autoPlayControllerEditor, colorConfigEditor;
        Editor[] playerHandlerEditors;
        [SerializeField] bool backendsToldout, videoScreenTargetsFoldout, audioRelatedFoldout;
        bool showAllSettings = false;

        static bool TryGetComponent<T>(UnityObject obj, out T component) where T : Component {
            if (obj is T t) {
                component = t;
                return true;
            }
            if (obj is GameObject go)
                return go.TryGetComponent(out component);
            if (obj is Component comp)
                return comp.TryGetComponent(out component);
            component = null;
            return false;
        }

        protected override void OnEnable() {
            base.OnEnable();
            showAllSettings = EditorPrefs.GetBool(showAllSettingsPrefsKey, false);
            playerHandlersProperty = serializedObject.FindProperty("playerHandlers");
            playerHandlersList = new SerializedReorderableList(playerHandlersProperty) {
                drawHeaderCallback = DrawPlayerHandlersListHeader,
            };
            audioSourcesProperty = serializedObject.FindProperty("audioSources");
            defaultUrlProperty = serializedObject.FindProperty("defaultUrl");
            defaultQuestUrlProperty = serializedObject.FindProperty("defaultQuestUrl");
            autoPlayPlayerTypeProperty = serializedObject.FindProperty("autoPlayPlayerType");
            syncedProperty = serializedObject.FindProperty("synced");
            totalRetryCountProperty = serializedObject.FindProperty("totalRetryCount");
            fallbackRetryCountProperty = serializedObject.FindProperty("fallbackRetryCount");
            retryDelayProperty = serializedObject.FindProperty("retryDelay");
            autoPlayDelayProperty = serializedObject.FindProperty("autoPlayDelay");
            defaultVolumeProperty = serializedObject.FindProperty("defaultVolume");
            defaultMutedProperty = serializedObject.FindProperty("defaultMuted");
            volumeFadeDurationProperty = serializedObject.FindProperty("volumeFadeDuration");
            muteOnOutOfRangeProperty = serializedObject.FindProperty("muteOnOutOfRange");
            outOfRangeVolumeProperty = serializedObject.FindProperty("outOfRangeVolume");
            loopProperty = serializedObject.FindProperty("loop");
            audioLinkProperty = serializedObject.FindProperty("audioLink");
            yttlManagerProperty = serializedObject.FindProperty("yttl");
            screenTargetsProperty = serializedObject.FindProperty("screenTargets");
            screenTargetModesProperty = serializedObject.FindProperty("screenTargetModes");
            screenTargetIndecesProperty = serializedObject.FindProperty("screenTargetIndeces");
            screenTargetPropertyNamesProperty = serializedObject.FindProperty("screenTargetPropertyNames");
            screenTargetDefaultTexturesProperty = serializedObject.FindProperty("screenTargetDefaultTextures");
            avProPropertyNamesProperty = serializedObject.FindProperty("avProPropertyNames");
            rtScreenTargetSTsProperty = serializedObject.FindProperty("rtScreenTargetSTs");
            broadcastScreenTextureProperty = serializedObject.FindProperty("broadcastScreenTexture");
            broadcastScreenTextureNameProperty = serializedObject.FindProperty("broadcastScreenTextureName");
            defaultTextureProperty = serializedObject.FindProperty("defaultTexture");
            realtimeGIUpdateIntervalProperty = serializedObject.FindProperty("realtimeGIUpdateInterval");
            timeDriftDetectThresholdProperty = serializedObject.FindProperty("timeDriftDetectThreshold");
            urlInputFilterProperty = serializedObject.FindProperty("urlInputFilter");
#if VRC_ENABLE_PLAYER_PERSISTENCE
            enablePersistenceProperty = serializedObject.FindProperty("enablePersistence");
#endif
            targetsList = new SerializedReorderableList(serializedObject.FindProperty("targets"));
            screenTargetVisibilityState = new List<bool>();
            for (int i = 0, count = screenTargetsProperty.arraySize; i < count; i++)
                screenTargetVisibilityState.Add(false);
            GetControlledTypesOnScene();
            ColorConfig.OnColorConfigsRemapped += OnColorConfigsRemapped;
            serializedObject.Update();
            RefreshAVProHandler();
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (autoPlayControllerEditor) DestroyImmediate(autoPlayControllerEditor);
            if (playerHandlerEditors != null)
                foreach (var editor in playerHandlerEditors)
                    if (editor) DestroyImmediate(editor);
            foreach (var audioSourceComponent in audioSourceComponents.Values) {
                audioSourceComponent.Item1?.Dispose();
                audioSourceComponent.Item2?.Dispose();
            }
            audioSourceComponents.Clear();
            ColorConfig.OnColorConfigsRemapped -= OnColorConfigsRemapped;
            if (colorConfigEditor != null) {
                DestroyImmediate(colorConfigEditor);
                colorConfigEditor = null;
            }
            if (lowLatencyModeProperty != null) {
                lowLatencyModeProperty.serializedObject.Dispose();
                lowLatencyModeProperty.Dispose();
                lowLatencyModeProperty = null;
            }
            foreach (var screenConfiguratorEditor in screenConfiguratorEditors.Values)
                if (screenConfiguratorEditor) DestroyImmediate(screenConfiguratorEditor);
            screenConfiguratorEditors.Clear();
        }

        public override void DrawEmbeddedInspectorGUI() {
            if (HandleDragDrop()) return;
            var autoPlayControllerEditor = GetAutoPlayControllerEditor();
            if (autoPlayControllerEditor != null)
                autoPlayControllerEditor.serializedObject.Update();
            i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.simpleSettings", settingsModes[0]);
            i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.advancedSettings", settingsModes[1]);
            using (var change = new EditorGUI.ChangeCheckScope()) {
                showAllSettings = GUILayout.Toolbar(showAllSettings ? 1 : 0, settingsModes) == 1;
                if (change.changed) EditorPrefs.SetBool(showAllSettingsPrefsKey, showAllSettings);
            }
            DrawAudioSettings();
            DrawRepeatShuffleSettings(autoPlayControllerEditor);
            DrawCommonSettings(autoPlayControllerEditor);
            DrawDefaultBehaviourSettings(autoPlayControllerEditor);
            DrawColorConfigSettings();
            DrawVideoSettings();
            DrawModuleSettings();
            if (showAllSettings) {
                DrawErrorHandlingSettings();
                DrawOtherSettings(autoPlayControllerEditor);
            }
            if (autoPlayControllerEditor != null)
                autoPlayControllerEditor.serializedObject.ApplyModifiedProperties();
            HorizontalLine();
            EditorGUILayout.HelpBox(i18n["JLChnToZ.VRC.VVMW.Core.target:add"], MessageType.Info);
        }

        bool HandleDragDrop() {
            var e = Event.current;
            var eventType = e.type;
            UnityObject[] objRefs;
            switch (eventType) {
                case EventType.DragUpdated:
                    objRefs = DragAndDrop.objectReferences;
                    if (objRefs == null || objRefs.Length == 0) break;
                    for (int i = 0; i < objRefs.Length; i++) {
                        var newTarget = objRefs[i];
                        if (TryGetComponent(newTarget, out ScreenConfigurator sc)) {
                            if (sc.core != null && sc.core != target) continue;
                        } else if (newTarget is CustomRenderTexture crt) {
                            if (crt.material == null) continue;
                        } else if (
                            newTarget is Renderer ||
                            newTarget is Material ||
                            newTarget is RawImage ||
                            newTarget is RenderTexture ||
                            newTarget is AudioSource
                        ) {
                        } else if (newTarget is GameObject go) {
                            if (go.TryGetComponent<Renderer>(out _)) { } else if (go.TryGetComponent<RawImage>(out _)) { } else if (go.TryGetComponent<AudioSource>(out _)) { } else continue;
                        } else continue;
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        e.Use();
                        return true;
                    }
                    break;
                case EventType.DragPerform:
                    objRefs = DragAndDrop.objectReferences;
                    if (objRefs == null || objRefs.Length == 0) break;
                    bool used = false;
                    foreach (var objRef in objRefs) {
                        if (AppendScreen(objRef)) {
                            used = true;
                            continue;
                        }
                        if (TryGetComponent(objRef, out AudioSource audioSource) &&
                            AppendAudioSource(target as Core, audioSource, audioSourcesProperty)) {
                            used = true;
                            continue;
                        }
                    }
                    if (used) {
                        DragAndDrop.visualMode = DragAndDropVisualMode.Link;
                        DragAndDrop.AcceptDrag();
                        e.Use();
                        return true;
                    }
                    break;
            }
            return false;
        }

        void DrawCommonSettings(VVMWEditorBase controllerEditor) {
            if (controllerEditor == null) return;
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.commonSettings"), EditorStyles.boldLabel);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor)
                frontendHandlerEditor.DrawCommonSettings(showAllSettings: showAllSettings);
            else
                controllerEditor.DrawEmbeddedInspectorGUI();
        }

        void DrawAudioSettings() {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.audioSettings"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultVolumeProperty);
            EditorGUILayout.PropertyField(defaultMutedProperty);
            if (!showAllSettings) return;
            EditorGUILayout.PropertyField(volumeFadeDurationProperty);
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                EditorGUILayout.PropertyField(muteOnOutOfRangeProperty);
                if (changed.changed && muteOnOutOfRangeProperty.boolValue && !serializedObject.isEditingMultipleObjects) {
                    var core = target as Core;
                    var regionConfigs = ActiveRegionConfig.GetRegionConfigs(core);
                    if (regionConfigs.Count == 0) {
                        if (i18n.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.Core.outOfRangeVolume:requireActiveRegion"))
                            CreateActiveRegion(core);
                        else
                            muteOnOutOfRangeProperty.boolValue = false;
                    }
                }
            }
            if (muteOnOutOfRangeProperty.boolValue)
                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.PropertyField(outOfRangeVolumeProperty);
        }

        static void CreateActiveRegion(Core core) {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(activeRegionPrefabPath);
            if (prefab != null) {
                var go = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (go != null) {
                    go.transform.SetParent(core.transform, false);
                    go.name = prefab.name;
                    if (go.TryGetComponent(out ActiveRegionConfig region)) {
                        region.core = core;
                        region.UpdateValue();
                        EditorGUIUtility.PingObject(region);
                    } else Undo.DestroyObjectImmediate(go);
                }
            }
        }

        void DrawRepeatShuffleSettings(VVMWEditorBase controllerEditor) {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.repeatShuffleSettings"), EditorStyles.boldLabel);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor) {
                frontendHandlerEditor.DrawRepeatShuffleSettings(showAllSettings: showAllSettings);
                return;
            }
            EditorGUILayout.PropertyField(loopProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.FrontendHandler.loopMode.singleLoop"));
            if (!showAllSettings) return;
            EditorGUILayout.PropertyField(autoPlayDelayProperty);
            if (autoPlayDelayProperty.floatValue < 0) autoPlayDelayProperty.floatValue = 0;
        }

        void DrawDefaultBehaviourSettings(VVMWEditorBase controllerEditor) {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.defaultBehaviourSettings"), EditorStyles.boldLabel);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor) {
                frontendHandlerEditor.DrawDefaultPlaylist();
                frontendHandlerEditor.DrawAutoPlaySettings(showAllSettings);
            } else {
                DrawAutoPlayField();
            }
            if (lowLatencyModeProperty != null) {
                var so = lowLatencyModeProperty.serializedObject;
                so.Update();
                EditorGUILayout.PropertyField(lowLatencyModeProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.VideoPlayerHandler.isLowLatency"));
                so.ApplyModifiedProperties();
            }
        }

        void DrawAutoPlayField() {
            int autoPlayPlayerType = autoPlayPlayerTypeProperty.intValue - 1;
            var playerType = playerTypes != null && autoPlayPlayerType >= 0 && autoPlayPlayerType < playerTypes.Length ? playerTypes[autoPlayPlayerType] : PlayerType.Unknown;
            DrawUrlField(defaultUrlProperty, "JLChnToZ.VRC.VVMW.Core.defaultUrl", playerType, BuildTarget.StandaloneWindows64);
            if (!string.IsNullOrEmpty(defaultUrlProperty.FindPropertyRelative("url").stringValue)) {
                DrawUrlField(defaultQuestUrlProperty, "JLChnToZ.VRC.VVMW.Core.defaultQuestUrl", playerType, BuildTarget.Android);
                if (DrawPlayerDropdown(playerHandlersProperty, autoPlayPlayerTypeProperty, ref autoPlayPlayerType, "JLChnToZ.VRC.VVMW.Core.autoPlayPlayerType"))
                    autoPlayPlayerTypeProperty.intValue = autoPlayPlayerType + 1;
            }
        }

        static void DrawUrlField(SerializedProperty urlProperty, string localizaedKey, PlayerType playerType, BuildTarget buildTarget) =>
            TrustedUrlUtils.DrawUrlField(
                urlProperty,
                playerType.ToTrustUrlType(buildTarget),
                EditorGUILayout.GetControlRect(
                    true,
                    EditorGUIUtility.singleLineHeight,
                    Array.Empty<GUILayoutOption>()
                ),
                i18n.GetLocalizedContent(localizaedKey)
            );

        void DrawColorConfigSettings() {
            if (colorConfigEditor == null) return;
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.colorConfig"), EditorStyles.boldLabel);
            var editor = colorConfigEditor as VVMWEditorBase;
            var so = editor.serializedObject;
            so.Update();
            editor.DrawEmbeddedInspectorGUI();
            so.ApplyModifiedProperties();
        }


        void DrawErrorHandlingSettings() {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.errorHandlingSettings"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(totalRetryCountProperty);
            EditorGUILayout.PropertyField(fallbackRetryCountProperty);
            EditorGUILayout.PropertyField(retryDelayProperty);
            EditorGUILayout.PropertyField(timeDriftDetectThresholdProperty);
        }

        void DrawModuleSettings() {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.moduleSettings"), EditorStyles.boldLabel);
            if (showAllSettings) {
                DrawScreenList();
                DrawAudioSourcesSettings();
                DrawPlayerHandlers();
                DrawThirdPartyModuleSettings();
            } else {
                DrawSimpleScreenSettings();
                DrawSimpleAudioSourceSettings();
            }
        }

        void DrawVideoSettings() {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoSettings"), EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(defaultTextureProperty);
            if (!showAllSettings) return;
            EditorGUILayout.PropertyField(broadcastScreenTextureProperty);
            if (broadcastScreenTextureProperty.boolValue)
                using (new EditorGUI.IndentLevelScope())
                    EditorGUILayout.PropertyField(broadcastScreenTextureNameProperty);
            EditorGUILayout.PropertyField(realtimeGIUpdateIntervalProperty);
        }

        void DrawThirdPartyModuleSettings() {
            EditorGUILayout.PropertyField(yttlManagerProperty);
            EditorGUILayout.PropertyField(audioLinkProperty);
        }

        void DrawOtherSettings(VVMWEditorBase controllerEditor) {
            HorizontalLine();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.otherSettings"), EditorStyles.boldLabel);
            if (defaultTextureProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.defaultTexture:empty_message"), MessageType.Error);
            if (!showAllSettings) return;
            EditorGUILayout.PropertyField(syncedProperty);
#if VRC_ENABLE_PLAYER_PERSISTENCE
            EditorGUILayout.PropertyField(enablePersistenceProperty);
#endif  
            EditorGUILayout.PropertyField(urlInputFilterProperty);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor)
                frontendHandlerEditor.DrawExtraSettings();
            targetsList.DoLayoutList();
        }

        internal static bool DrawPlayerDropdown(SerializedProperty playerHandlersProperty, SerializedProperty autoPlayPlayerTypeProperty, ref int autoPlayPlayerType, string localeKey = "") {
            if (playerNames == null || playerNames.Length != playerHandlersProperty.arraySize)
                playerNames = new string[playerHandlersProperty.arraySize];
            if (playerTypes == null || playerTypes.Length != playerHandlersProperty.arraySize)
                playerTypes = new PlayerType[playerHandlersProperty.arraySize];
            for (int i = 0; i < playerNames.Length; i++) {
                var playerHandler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as AbstractMediaPlayerHandler;
                if (playerHandler == null)
                    playerNames[i] = "null";
                else {
                    playerNames[i] = string.IsNullOrEmpty(playerHandler.playerName) ? playerHandler.name : playerHandler.playerName;
                    playerTypes[i] = playerHandler.GetPlayerType();
                }
            }
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var content = string.IsNullOrEmpty(localeKey) ? FUtils.GetTempContent(autoPlayPlayerTypeProperty) : i18n.GetLocalizedContent(localeKey);
            using (var scope = new EditorGUI.PropertyScope(rect, content, autoPlayPlayerTypeProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                rect = EditorGUI.PrefixLabel(rect, scope.content);
                autoPlayPlayerType = EditorGUI.Popup(rect, autoPlayPlayerType, playerNames);
                if (changed.changed) return true;
            }
            return false;
        }

        void DrawPlayerHandlers() {
            backendsToldout = EditorGUILayout.Foldout(backendsToldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.playerHandlers"), true);
            if (!backendsToldout) return;
            using var _ = new EditorGUI.IndentLevelScope();
            int count = playerHandlersProperty.arraySize;
            playerHandlersList.DoLayoutList();
            if (count != playerHandlersProperty.arraySize) {
                count = playerHandlersProperty.arraySize;
                RefreshAVProHandler();
            }
            if (playerHandlerEditors == null || playerHandlerEditors.Length < count)
                playerHandlerEditors = new Editor[count];
            for (int i = 0, drawnCount = 0; i < count; i++) {
                var playerHandlerProperty = playerHandlersProperty.GetArrayElementAtIndex(i);
                var playerHandler = playerHandlerProperty.objectReferenceValue as AbstractMediaPlayerHandler;
                if (!playerHandler) continue;
                if (editorTypes.TryGetValue(playerHandler.GetType(), out var editorType))
                    CreateCachedEditor(playerHandler, editorType, ref playerHandlerEditors[i]);
                if (!(playerHandlerEditors[i] is VVMWEditorBase playerHandlerEditor)) continue;
                bool expanded;
                using (var change = new EditorGUI.ChangeCheckScope()) {
                    expanded = EditorGUILayout.Foldout(playerHandlerProperty.isExpanded, $"{i18n.GetLocalizedContent(playerHandler.playerName)} ({playerHandler.name})", true);
                    if (change.changed) playerHandlerProperty.isExpanded = expanded;
                }
                if (!expanded) continue;
                if (drawnCount++ > 0) EditorGUILayout.Space();
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                    playerHandlerEditor.serializedObject.Update();
                    playerHandlerEditor.DrawEmbeddedInspectorGUI();
                    playerHandlerEditor.serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.Space();
        }

        void DrawPlayerHandlersListHeader(Rect rect) {
            var tempContent = i18n.GetLocalizedContent("Locatable.AutoFind");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            EditorGUI.LabelField(rect, i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.playerHandlers"));
            if (GUI.Button(buttonRect, tempContent, miniButtonStyle)) {
                var handlers = (target as Core).GetComponentsInChildren<AbstractMediaPlayerHandler>(true);
                playerHandlersProperty.arraySize = handlers.Length;
                for (int i = 0; i < handlers.Length; i++)
                    playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue = handlers[i];
            }
        }

        void DrawSimpleAudioSourceSettings() {
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.audioSourceWithCount", audioSourcesProperty.arraySize));
            using (new EditorGUI.IndentLevelScope())
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.setupSpeakers")))
                    SetupSpeakers();
        }

        void DrawAudioSourcesSettings() {
            using (new EditorGUILayout.HorizontalScope()) {
                audioRelatedFoldout = EditorGUILayout.Foldout(audioRelatedFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.audioSources"), true);
                if (!audioRelatedFoldout) return;
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.setupSpeakers"), EditorStyles.miniButton, GUILayout.ExpandWidth(false))) 
                    SetupSpeakers();
            }
            using (new EditorGUI.IndentLevelScope())
                DrawAudioList();
        }

        void SetupSpeakers() {
            Undo.IncrementCurrentGroup();
            int undoGroup = Undo.GetCurrentGroup();
            var builtinPlayerHandlers = new List<AbstractMediaPlayerHandler>();
            AbstractMediaPlayerHandler avProPlayerHandler = null; // only one avpro player handler is supported
            bool hasMultipleAvProPlayerHandler = false;
            for (int i = 0, count = playerHandlersProperty.arraySize; i < count; i++) {
                var playerHandler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as AbstractMediaPlayerHandler;
                if (playerHandler == null) continue;
                if (!playerHandler.IsAvPro)
                    builtinPlayerHandlers.Add(playerHandler);
                else if (avProPlayerHandler == null)
                    avProPlayerHandler = playerHandler;
                else
                    hasMultipleAvProPlayerHandler = true;
            }
            if (audioSourcesProperty.arraySize > 1)
                i18n.DisplayLocalizedDialog1("JLChnToZ.VRC.VVMW.Core.audioSources:multiple_source_message");
            var primaryAudioSource = audioSourcesProperty.arraySize > 0 ? audioSourcesProperty.GetArrayElementAtIndex(0).objectReferenceValue : null;
            foreach (var handler in builtinPlayerHandlers) {
                using (var so = new SerializedObject(handler)) {
                    var property = so.FindProperty("primaryAudioSource");
                    if (property != null) property.objectReferenceValue = primaryAudioSource;
                    so.ApplyModifiedProperties();
                }
                if (handler.TryGetComponent(out VRCUnityVideoPlayer unityVideoPlayer))
                    using (var so = new SerializedObject(unityVideoPlayer)) {
                        var prop = so.FindProperty("targetAudioSources");
                        prop.arraySize = 1;
                        prop.GetArrayElementAtIndex(0).objectReferenceValue = primaryAudioSource;
                        so.ApplyModifiedProperties();
                    }
            }
            if (hasMultipleAvProPlayerHandler)
                i18n.DisplayLocalizedDialog1("JLChnToZ.VRC.VVMW.Core.audioSources:multiple_players_message");
            else if (avProPlayerHandler != null) {
                bool hasAppliedPrimaryAudioSource = false;
                var actualPlayer = avProPlayerHandler.GetComponent<VRCAVProVideoPlayer>();
                for (int i = 0, count = audioSourcesProperty.arraySize; i < count; i++) {
                    var audioSource = audioSourcesProperty.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                    if (audioSource == null || !audioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker)) continue;
                    using (var so = new SerializedObject(speaker)) {
                        so.FindProperty("videoPlayer").objectReferenceValue = actualPlayer;
                        if (so.FindProperty("mode").intValue == 0 && !hasAppliedPrimaryAudioSource) {
                            using (var so2 = new SerializedObject(avProPlayerHandler)) {
                                so2.FindProperty("primaryAudioSource").objectReferenceValue = audioSource;
                                so2.ApplyModifiedProperties();
                            }
                            hasAppliedPrimaryAudioSource = true;
                        }
                        so.ApplyModifiedProperties();
                    }
                }
            }
            Undo.SetCurrentGroupName(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.setupSpeakers"));
            Undo.CollapseUndoOperations(undoGroup);
        }

        void DrawSimpleScreenSettings() {
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTargetsWithCount", screenTargetsProperty.arraySize));
            using (new EditorGUI.IndentLevelScope())
                if (GUILayout.Button(i18n.GetLocalizedContent("ScreenConfigurator.FixupAspectRatio")))
                    using (PooledObjects.Get(out List<Renderer> renderers)) {
                        for (int i = 0, count = screenTargetsProperty.arraySize; i < count; i++) {
                            var targetProperty = screenTargetsProperty.GetArrayElementAtIndex(i);
                            if (targetProperty.objectReferenceValue is Renderer renderer)
                                renderers.Add(renderer);
                        }
                        ScreenMeshUtils.TryFixupAspectRatioInMaterial(renderers);
                    }
        }

        void DrawScreenList() {
            videoScreenTargetsFoldout = EditorGUILayout.Foldout(videoScreenTargetsFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTargets"), true);
            if (!videoScreenTargetsFoldout) return;
            using var _ = new EditorGUI.IndentLevelScope();
            int length = screenTargetsProperty.arraySize;
            if (screenTargetModesProperty.arraySize != length)
                screenTargetModesProperty.arraySize = length;
            if (screenTargetIndecesProperty.arraySize != length)
                screenTargetIndecesProperty.arraySize = length;
            if (screenTargetPropertyNamesProperty.arraySize != length)
                screenTargetPropertyNamesProperty.arraySize = length;
            if (avProPropertyNamesProperty.arraySize != length)
                avProPropertyNamesProperty.arraySize = length;
            if (screenTargetDefaultTexturesProperty.arraySize != length)
                screenTargetDefaultTexturesProperty.arraySize = length;
            if (rtScreenTargetSTsProperty.arraySize != length)
                rtScreenTargetSTsProperty.arraySize = length;
            while (screenTargetVisibilityState.Count < length)
                screenTargetVisibilityState.Add(false);
            for (int i = 0; i < length; i++) {
                var targetProperty = screenTargetsProperty.GetArrayElementAtIndex(i);
                var modeProperty = screenTargetModesProperty.GetArrayElementAtIndex(i);
                var screenConfigurator = ScreenConfigurator.GetInstance(
                    targetProperty.objectReferenceValue as Renderer,
                    screenTargetIndecesProperty.GetArrayElementAtIndex(i).intValue
                );
                using (new EditorGUILayout.HorizontalScope()) {
                    screenTargetVisibilityState[i] = EditorGUILayout.Toggle(screenTargetVisibilityState[i], EditorStyles.foldout, GUILayout.Width(13));
                    bool deleteElement = false;
                    if (screenConfigurator && screenConfigurator.core == target) {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(GUIContent.none, screenConfigurator, typeof(ScreenConfigurator), true);
                        screenConfigurator.GetComponents(behaviours);
                        bool locked = false;
                        foreach (var mb in behaviours)
                            if (mb is IVizVidCompoonent && mb != screenConfigurator) {
                                locked = true;
                                break;
                            }
                        using (new EditorGUI.DisabledScope(locked))
                            if (GUILayout.Button(i18n.GetLocalizedContent("VVMW.Remove"), GUILayout.ExpandWidth(false))) {
                                using (var scso = new SerializedObject(screenConfigurator)) {
                                    scso.FindProperty("core").objectReferenceValue = null;
                                    scso.ApplyModifiedProperties();
                                }
                                serializedObject.Update();
                                deleteElement = true;
                            }
                    } else {
                        EditorGUILayout.PropertyField(targetProperty, GUIContent.none);
                        var value = targetProperty.objectReferenceValue;
                        if (value is GameObject gameObject) {
                            if (gameObject.TryGetComponent(out Renderer renderer))
                                targetProperty.objectReferenceValue = renderer;
                            else if (gameObject.TryGetComponent(out RawImage rawImage))
                                targetProperty.objectReferenceValue = rawImage;
                            else targetProperty.objectReferenceValue = null;
                        } else if (value is CustomRenderTexture crt)
                            targetProperty.objectReferenceValue = crt.material;
                        else if (value is RenderTexture rt)
                            targetProperty.objectReferenceValue = rt;
                        else if (value is Renderer) { } else if (value is Material) { } else if (value is RawImage) { } else targetProperty.objectReferenceValue = null;
                        if (GUILayout.Button(i18n.GetLocalizedContent("VVMW.Remove"), GUILayout.ExpandWidth(false)))
                            deleteElement = true;
                    }
                    if (deleteElement) {
                        FUtils.DeleteElement(screenTargetsProperty, i);
                        FUtils.DeleteElement(screenTargetModesProperty, i);
                        FUtils.DeleteElement(screenTargetIndecesProperty, i);
                        FUtils.DeleteElement(screenTargetPropertyNamesProperty, i);
                        FUtils.DeleteElement(avProPropertyNamesProperty, i);
                        FUtils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                        FUtils.DeleteElement(rtScreenTargetSTsProperty, i);
                        screenTargetVisibilityState.RemoveAt(i);
                        i--;
                        length--;
                    }
                }
                if (i >= 0 && screenTargetVisibilityState[i])
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        ParseScreenMode(modeProperty, out int mode, out bool useST, out int blitFlags);
                        bool showMaterialOptions = false;
                        Shader selectedShader = null;
                        Material[] materials = null;
                        if (targetProperty.objectReferenceValue is Material m) {
                            mode = 0;
                            showMaterialOptions = true;
                            selectedShader = m.shader;
                        } else if (targetProperty.objectReferenceValue is Renderer renderer) {
                            DrawScreenRendererOptions(
                                screenTargetIndecesProperty.GetArrayElementAtIndex(i),
                                renderer, ref mode, out selectedShader, out materials
                            );
                            showMaterialOptions = true;
                        } else if (targetProperty.objectReferenceValue is RawImage) {
                            mode = 4;
                        } else if (targetProperty.objectReferenceValue is RenderTexture) {
                            mode = 5;
                            DrawScreenSTOptions(
                                rtScreenTargetSTsProperty.GetArrayElementAtIndex(i),
                                ref blitFlags
                            );
                        } else {
                            FUtils.DeleteElement(screenTargetsProperty, i);
                            FUtils.DeleteElement(screenTargetModesProperty, i);
                            FUtils.DeleteElement(screenTargetIndecesProperty, i);
                            FUtils.DeleteElement(screenTargetPropertyNamesProperty, i);
                            FUtils.DeleteElement(avProPropertyNamesProperty, i);
                            FUtils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                            FUtils.DeleteElement(rtScreenTargetSTsProperty, i);
                            screenTargetVisibilityState.RemoveAt(i);
                            i--;
                            length--;
                            continue;
                        }
                        if (showMaterialOptions)
                            DrawScreenMaterialOptions(
                                targetProperty,
                                screenTargetPropertyNamesProperty.GetArrayElementAtIndex(i),
                                avProPropertyNamesProperty.GetArrayElementAtIndex(i),
                                ref useST, selectedShader, materials
                            );
                        if (DrawScreenTextureOptions(
                            screenTargetDefaultTexturesProperty.GetArrayElementAtIndex(i),
                            defaultTextureProperty
                        ))
                            ScreenConfigurator.NotifyDefaultTextureChanged(
                                targetProperty.objectReferenceValue as Renderer,
                                screenTargetIndecesProperty.GetArrayElementAtIndex(i).intValue
                            );
                        SetScreenMode(modeProperty, mode, useST, blitFlags);
                        if (screenConfigurator != null) {
                            if (!screenConfiguratorEditors.TryGetValue(screenConfigurator, out var scEditor))
                                screenConfiguratorEditors[screenConfigurator] = scEditor = CreateEditor(screenConfigurator) as ScreenConfiguratorEditor;
                            if (scEditor != null) {
                                scEditor.serializedObject.Update();
                                scEditor.DrawExtraProperties();
                                scEditor.serializedObject.ApplyModifiedProperties();
                            }
                        }
                    }
            }
            EditorGUILayout.Space();
        }

        void DrawAudioList() {
            for (int i = 0, count = audioSourcesProperty.arraySize; i < count; i++) {
                using var audioSourceProperty = audioSourcesProperty.GetArrayElementAtIndex(i);
                var audioSource = audioSourceProperty.objectReferenceValue as AudioSource;
                if (audioSource == null) continue;
                bool expanded = audioSourceProperty.isExpanded;
                using (new EditorGUILayout.HorizontalScope()) {
                    using (var change = new EditorGUI.ChangeCheckScope()) {
                        expanded = EditorGUILayout.Toggle(expanded, EditorStyles.foldout, GUILayout.Width(13));
                        if (change.changed) audioSourceProperty.isExpanded = expanded;
                    }
                    EditorGUILayout.ObjectField(GUIContent.none, audioSource, typeof(AudioSource), true);
                    if (GUILayout.Button(i18n.GetLocalizedContent("VVMW.Remove"), GUILayout.ExpandWidth(false)))
                        audioSourcesProperty.DeleteArrayElementAtIndex(i);
                }
                if (!expanded) continue;
                using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                    if (!audioSourceComponents.TryGetValue(audioSource, out var components)) {
                        if (audioSource.TryGetComponent(out VRCSpatialAudioSource spatialAudioSource))
                            components.Item1 = new SerializedObject(spatialAudioSource);
                        if (audioSource.TryGetComponent(out VRCAVProVideoSpeaker avProVideoSpeaker))
                            components.Item2 = new SerializedObject(avProVideoSpeaker);
                        audioSourceComponents[audioSource] = components;
                    }
                    var (spatialAudioSourceSO, avProVideoSpeakerSO) = components;
                    if (avProVideoSpeakerSO != null) {
                        avProVideoSpeakerSO.Update();
                        using (var property = avProVideoSpeakerSO.FindProperty("mode"))
                            EditorGUILayout.PropertyField(property);
                        avProVideoSpeakerSO.ApplyModifiedProperties();
                    }
                    if (spatialAudioSourceSO != null) {
                        spatialAudioSourceSO.Update();
                        using (var iterator = spatialAudioSourceSO.GetIterator()) {
                            bool enterChildren = true;
                            while (iterator.NextVisible(enterChildren)) {
                                enterChildren = false;
                                if (iterator.name == "m_Script") continue;
                                EditorGUILayout.PropertyField(iterator, true);
                            }
                        }
                        spatialAudioSourceSO.ApplyModifiedProperties();
                    }
                    if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.showAudioProperties")))
                        EditorUtility.OpenPropertyEditor(audioSource);
                }
            }
        }

        public static void ParseScreenMode(SerializedProperty modeProperty, out int mode, out bool useST, out int blitFlags) {
            int rawMode = modeProperty.intValue;
            mode = rawMode & 0x7;
            useST = (rawMode & 0x8) != 0;
            blitFlags = (rawMode & 0xF0) >> 4;
        }

        public static void SetScreenMode(SerializedProperty modeProperty, int mode, bool useST, int blitFlags) {
            modeProperty.intValue = mode | (useST ? 0x8 : 0) | ((blitFlags << 4) & 0xF0);
        }

        public static void DrawScreenRendererOptions(
            SerializedProperty indexProperty,
            Renderer renderer,
            ref int mode,
            out Shader selectedShader,
            out Material[] materials
        ) {
            if (mode != 1 && mode != 2 && mode != 3) mode = 1;
            materialModeOptions[0] = i18n.GetOrDefault("VVMW.Material.PropertyBlock");
            materialModeOptions[1] = i18n.GetOrDefault("VVMW.Material.SharedMaterial");
            materialModeOptions[2] = i18n.GetOrDefault("VVMW.Material.ClonedMaterial");
            mode = EditorGUILayout.Popup(i18n.GetLocalizedContent("VVMW.Mode"), mode - 1, materialModeOptions) + 1;
            materials = renderer.sharedMaterials;
            string[] indexNames = new string[materials.Length + 1];
            indexNames[0] = i18n.GetOrDefault("VVMW.All");
            for (int j = 0; j < materials.Length; j++)
                if (materials[j] != null)
                    indexNames[j + 1] = $"({j}) {materials[j].name} ({materials[j].shader.name.Replace("/", ".")})";
                else
                    indexNames[j + 1] = $"({j}) null";
            int selectedIndex = indexProperty.intValue + 1;
            selectedIndex = EditorGUILayout.Popup(i18n.GetLocalizedContent("VVMW.Material"), selectedIndex, indexNames) - 1;
            indexProperty.intValue = selectedIndex;
            selectedShader = selectedIndex >= 0 && selectedIndex <= materials.Length ? materials[selectedIndex].shader : null;
        }

        public static void DrawScreenMaterialOptions(
            SerializedProperty targetProperty,
            SerializedProperty nameProperty,
            SerializedProperty avProProperty,
            ref bool useST,
            Shader selectedShader,
            Material[] materials
        ) {
            Utils.DrawShaderPropertiesField(
                nameProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.screenTargetPropertyNames"),
                selectedShader, materials, ShaderUtil.ShaderPropertyType.TexEnv
            );
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                useST = EditorGUILayout.Toggle(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.useST"), useST);
                if (!useST) Utils.DrawShaderPropertiesField(
                    avProProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.avProPropertyNames"),
                    selectedShader, materials, ShaderUtil.ShaderPropertyType.Float
                );
            }
            VideoMaterialEmbeddedEditor materialEditor;
            var target = targetProperty.objectReferenceValue;
            bool materialMode = false;
            if (target is Renderer renderer) {
                if (!videoMaterialEditors.TryGetValue(renderer, out materialEditor)) {
                    materialEditor = new VideoMaterialEmbeddedEditor(renderer);
                    videoMaterialEditors[renderer] = materialEditor;
                }
            } else if (target is Material material) {
                if (!videoMaterialEditors.TryGetValue(material, out materialEditor)) {
                    materialEditor = new VideoMaterialEmbeddedEditor(material);
                    videoMaterialEditors[material] = materialEditor;
                }
                materialMode = true;
            } else return;
            if (materialEditor != null) {
                if (materialEditor.TryGetScaleMode(out var scaleMode))
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        scaleMode = (ScaleMode)EditorGUILayout.EnumPopup(i18n.GetLocalizedContent("VideoMaterial.ScaleMode"), scaleMode);
                        if (changed.changed) {
                            materialEditor.ScaleMode = scaleMode;
                            if (materialMode) targetProperty.objectReferenceValue = materialEditor.Materials[0];
                        }
                    }
                if (materialEditor.TryGetEmissionIntensity(out var emissionIntensity))
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        emissionIntensity = EditorGUILayout.Slider(i18n.GetLocalizedContent("VideoMaterial.EmissionIntensity"), materialEditor.EmissionIntensity, 0f, 10f);
                        if (changed.changed) {
                            materialEditor.EmissionIntensity = emissionIntensity;
                            if (materialMode) targetProperty.objectReferenceValue = materialEditor.Materials[0];
                        }
                    }
                if (materialEditor.TryGetMirrorFlip(out var mirrorFlip))
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        mirrorFlip = EditorGUILayout.Toggle(i18n.GetLocalizedContent("UIModified.FlipInMirror"), materialEditor.MirrorFlip);
                        if (changed.changed) {
                            materialEditor.MirrorFlip = mirrorFlip;
                            if (materialMode) targetProperty.objectReferenceValue = materialEditor.Materials[0];
                        }
                    }
                if (materialEditor.TryGetRenderMode(out var renderMode))
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        renderMode = (VRCMirrorModeFlag)EditorGUILayout.EnumFlagsField(i18n.GetLocalizedContent("UIModified.VisibleModes"), renderMode);
                        if (changed.changed) {
                            materialEditor.RenderMode = renderMode;
                            if (materialMode) targetProperty.objectReferenceValue = materialEditor.Materials[0];
                        }
                    }
            }
        }

        public static bool DrawScreenTextureOptions(
            SerializedProperty textureProperty,
            SerializedProperty defaultTextureProperty = null
        ) {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
            var label = i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.screenTargetDefaultTextures");
            using (new EditorGUI.PropertyScope(rect, label, textureProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var texture = textureProperty.objectReferenceValue;
                if (texture == null && defaultTextureProperty != null)
                    texture = defaultTextureProperty.objectReferenceValue;
                texture = EditorGUI.ObjectField(rect, label, texture, typeof(Texture), false);
                if (changed.changed) {
                    textureProperty.objectReferenceValue = texture;
                    return true;
                }
            }
            return false;
        }

        public static void DrawScreenSTOptions(SerializedProperty stProperty, ref int blitFlags) {
            var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight * 2);
            var label = i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.screenTargetST");
            using (new EditorGUI.PropertyScope(rect, label, stProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var st = stProperty.vector4Value;
                var r = new Rect(st.z, st.w, st.x, st.y);
                r = EditorGUI.RectField(rect, label, r);
                if (changed.changed) stProperty.vector4Value = new Vector4(r.width, r.height, r.x, r.y);
            }
            var e = i18n.GetLocalizedEnum(typeof(ScreenTargetBlitMode));
            blitFlags = EditorGUILayout.Popup(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.screenTargetBlitMode"), blitFlags, e.enumNames as GUIContent[]);
        }

        public static bool AddTarget(Core core, UnityObject newTarget, bool recordUndo = true, bool copyToUdon = false) {
            using (var so = new SerializedObject(core)) {
                if (newTarget is AudioSource audio)
                    AppendAudioSource(core, audio, so.FindProperty("audioSources"));
                else if (!AppendScreen(newTarget, new ScreenProperties(so)))
                    return false;
                if (recordUndo)
                    so.ApplyModifiedProperties();
                else
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (copyToUdon) UdonSharpEditorUtility.CopyProxyToUdon(core);
            return true;
        }

        public static bool AddTarget(Core core, Renderer newTarget, int materialIndex = -1, bool recordUndo = true, bool copyToUdon = false) {
            using (var so = new SerializedObject(core)) {
                var material = materialIndex < 0 ? newTarget.sharedMaterial : newTarget.sharedMaterials[materialIndex];
                var mainTexturePropertyName = Utils.FindMainTexturePropertyName(material);
                var avProPropertyName = FindAVProPropertyName(material);
                var screenTargetMode = avProPropertyName == null ? 9 : 1;
                var defaultTexture = material != null ? material.GetTexture(mainTexturePropertyName) : null;
                AppendScreenUnchecked(
                    newTarget, screenTargetMode, materialIndex, mainTexturePropertyName, defaultTexture, avProPropertyName, Vector4.zero,
                    new ScreenProperties(so)
                );
                if (recordUndo)
                    so.ApplyModifiedProperties();
                else
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (copyToUdon) UdonSharpEditorUtility.CopyProxyToUdon(core);
            return true;
        }

        public static int TryDetermineSpeakerChannelMode(AudioSource audioSource) =>
            audioSource != null && audioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker) ?
            (int)speaker.Mode : -1;

        bool AppendScreen(UnityObject newTarget) {
            if (newTarget is ScreenConfigurator sc || (newTarget is GameObject go && go.TryGetComponent(out sc))) {
                using (var scso = new SerializedObject(sc)) {
                    scso.FindProperty("core").objectReferenceValue = target;
                    scso.ApplyModifiedProperties();
                }
                serializedObject.Update();
                return true;
            }
            if (AppendScreen(
                newTarget, new ScreenProperties(
                screenTargetsProperty,
                screenTargetModesProperty,
                screenTargetIndecesProperty,
                screenTargetPropertyNamesProperty,
                screenTargetDefaultTexturesProperty,
                avProPropertyNamesProperty,
                rtScreenTargetSTsProperty
            ))) {
                screenTargetVisibilityState.Add(true);
                return true;
            }
            return false;
        }

        static bool AppendScreen(UnityObject newTarget, ScreenProperties props) {
            int screenTargetMode;
            Texture defaultTexture;
            Vector4 st = Vector4.zero;
            string mainTexturePropertyName = null, avProPropertyName = null;
            if (newTarget is CustomRenderTexture crt)
                newTarget = crt.material;
            if (newTarget is Material material) {
                mainTexturePropertyName = Utils.FindMainTexturePropertyName(material);
                avProPropertyName = FindAVProPropertyName(material);
                screenTargetMode = avProPropertyName == null ? 8 : 0;
                defaultTexture = material.GetTexture(mainTexturePropertyName);
            } else if (newTarget is Renderer renderer || (newTarget is GameObject rendererGO && rendererGO.TryGetComponent(out renderer))) {
                newTarget = renderer;
                material = renderer.sharedMaterial;
                mainTexturePropertyName = Utils.FindMainTexturePropertyName(material);
                avProPropertyName = FindAVProPropertyName(material);
                screenTargetMode = avProPropertyName == null ? 9 : 1;
                defaultTexture = material != null ? material.GetTexture(mainTexturePropertyName) : null;
            } else if (newTarget is RawImage rawImage || (newTarget is GameObject rawImageGO && rawImageGO.TryGetComponent(out rawImage))) {
                newTarget = rawImage;
                screenTargetMode = 4;
                defaultTexture = rawImage.texture;
            } else if (newTarget is RenderTexture) {
                screenTargetMode = 5;
                defaultTexture = null;
                st = new Vector4(1, 1, 0, 0);
            } else return false;
            AppendScreenUnchecked(
                newTarget, screenTargetMode, -1, mainTexturePropertyName, defaultTexture, avProPropertyName, st,
                props
            );
            return true;
        }

        static bool AppendAudioSource(Core core, AudioSource newTarget, SerializedProperty audioSourcesProperty) {
            if (newTarget == null) return false;
            for (int i = 0, count = audioSourcesProperty.arraySize; i < count; i++)
                if (audioSourcesProperty.GetArrayElementAtIndex(i).objectReferenceValue == newTarget)
                    return false;
            var index = audioSourcesProperty.arraySize++;
            audioSourcesProperty.GetArrayElementAtIndex(index).objectReferenceValue = newTarget;
            newTarget.TryGetComponent(out VRCAVProVideoSpeaker speaker);
            var mode = TryDetermineSpeakerChannelMode(newTarget);
            foreach (var handler in core.playerHandlers) {
                if (handler == null || !(handler is VideoPlayerHandler)) continue;
                bool shouldAddPrimaryAudioSource = mode >= 0 && mode < 3;
                if (handler.TryGetComponent(out VRCUnityVideoPlayer builtin) && mode <= 0)
                    using (var so = new SerializedObject(builtin)) {
                        var prop = so.FindProperty("targetAudioSources");
                        if (prop.arraySize == 0) prop.arraySize = 1;
                        var first = prop.GetArrayElementAtIndex(0);
                        if (first.objectReferenceValue == null) {
                            first.objectReferenceValue = newTarget;
                            shouldAddPrimaryAudioSource = true;
                            so.ApplyModifiedProperties();
                        }
                    }
                else if (handler.TryGetComponent(out VRCAVProVideoPlayer avp) && mode >= 0)
                    using (var so = new SerializedObject(speaker)) {
                        var videoPlayerProperty = so.FindProperty("videoPlayer");
                        if (videoPlayerProperty.objectReferenceValue == null) {
                            videoPlayerProperty.objectReferenceValue = avp;
                            so.ApplyModifiedProperties();
                        }
                    }
                if (shouldAddPrimaryAudioSource)
                    using (var so = new SerializedObject(handler)) {
                        switch (mode) {
                            case -1:
                            case 0:
                            case 1:
                                var leftAudioSourceProperty = so.FindProperty("primaryAudioSource");
                                if (TryDetermineSpeakerChannelMode(leftAudioSourceProperty.objectReferenceValue as AudioSource) < 0)
                                    leftAudioSourceProperty.objectReferenceValue = newTarget;
                                break;
                            case 2:
                                var rightAudioSourceProperty = so.FindProperty("primaryAudioSourceR");
                                if (TryDetermineSpeakerChannelMode(rightAudioSourceProperty.objectReferenceValue as AudioSource) < 0)
                                    rightAudioSourceProperty.objectReferenceValue = newTarget;
                                break;
                        }
                        so.ApplyModifiedProperties();
                    }
            }
            return true;
        }

        static void AppendScreenUnchecked(
            UnityObject newTarget,
            int screenTargetMode,
            int index,
            string mainTexturePropertyName,
            Texture defaultTexture,
            string avProPropertyName,
            Vector4 st,
            ScreenProperties props
        ) {
            AppendElement(props.screenTargetsProperty, newTarget);
            AppendElement(props.screenTargetModesProperty, screenTargetMode);
            AppendElement(props.screenTargetIndecesProperty, index);
            AppendElement(props.screenTargetPropertyNamesProperty, mainTexturePropertyName ?? "_MainTex");
            AppendElement(props.screenTargetDefaultTexturesProperty, defaultTexture);
            AppendElement(props.avProPropertyNamesProperty, avProPropertyName ?? "_IsAVProVideo");
            AppendElement(props.rtScreenTargetSTsProperty, st);
        }

        static string FindAVProPropertyName(Material material) {
            if (material == null) return null;
            var shader = material.shader;
            if (shader == null) return null;
            string matchedName = null;
            int count = shader.GetPropertyCount();
            int score = 0;
            for (int i = 0; i < count; i++) {
                var propertyType = shader.GetPropertyType(i);
                int currentScore = 0;
                switch (propertyType) {
                    case ShaderPropertyType.Range:
#if UNITY_2021_1_OR_NEWER
                    case ShaderPropertyType.Float:
#endif
                        currentScore = 1;
                        break;
#if UNITY_2021_1_OR_NEWER
                    case ShaderPropertyType.Int:
#else
                    case ShaderPropertyType.Float:
#endif
                        currentScore = 2;
                        break;
                }
                if (currentScore == 0) continue;
                var name = shader.GetPropertyName(i);
                if (name.StartsWith("_Is", StringComparison.OrdinalIgnoreCase))
                    currentScore++;
                if (name.Contains("AVPro", StringComparison.OrdinalIgnoreCase))
                    currentScore += 2;
                if (currentScore > score && currentScore > 3) {
                    score = currentScore;
                    matchedName = name;
                }
            }
            return matchedName;
        }

        static void AppendElement(SerializedProperty property, UnityObject value) {
            int size = property.arraySize;
            property.arraySize++;
            property.GetArrayElementAtIndex(size).objectReferenceValue = value;
        }

        static void AppendElement(SerializedProperty property, string value) {
            int size = property.arraySize;
            property.arraySize++;
            property.GetArrayElementAtIndex(size).stringValue = value;
        }

        static void AppendElement(SerializedProperty property, int value) {
            int size = property.arraySize;
            property.arraySize++;
            property.GetArrayElementAtIndex(size).intValue = value;
        }

        static void AppendElement(SerializedProperty property, Vector4 value) {
            int size = property.arraySize;
            property.arraySize++;
            property.GetArrayElementAtIndex(size).vector4Value = value;
        }

        void GetControlledTypesOnScene() {
            autoPlayControllers.Clear();
            foreach (var controller in SceneManager.GetActiveScene().IterateAllComponents<UdonSharpBehaviour>())
                if (controllableTypes.TryGetValue(controller.GetType(), out var result) && result.fieldInfo.GetValue(controller) is Core coreComponent)
                    autoPlayControllers[coreComponent] = controller;
        }

        VVMWEditorBase GetAutoPlayControllerEditor() {
            if (!autoPlayControllers.TryGetValue(target as Core, out var controller)) return null;
            if (controllableTypes.TryGetValue(controller.GetType(), out var pair))
                CreateCachedEditor(controller, pair.editorType, ref autoPlayControllerEditor);
            return autoPlayControllerEditor as VVMWEditorBase;
        }

        void OnColorConfigsRemapped(Core core, HashSet<ColorConfig> set) {
            if (core != target) return;
            if (set.Count == 0) {
                DestroyImmediate(colorConfigEditor);
                colorConfigEditor = null;
                return;
            }
            var array = new ColorConfig[set.Count];
            set.CopyTo(array);
            CreateCachedEditor(array, typeof(ColorConfigEditor), ref colorConfigEditor);
        }

        void RefreshAVProHandler() {
            var size = playerHandlersProperty.arraySize;
            if (lowLatencyModeProperty != null) {
                lowLatencyModeProperty.serializedObject.Dispose();
                lowLatencyModeProperty.Dispose();
            }
            if (size == 0) return;
            using (PooledObjects.Get(out List<UnityObject> backends, size)) {
                for (int i = 0; i < size; i++) {
                    using var handlerProperty = playerHandlersProperty.GetArrayElementAtIndex(i);
                    var handler = handlerProperty.objectReferenceValue as MonoBehaviour;
                    if (handler == null || !handler.TryGetComponent(out VRCAVProVideoPlayer avp)) continue;
                    backends.Add(avp);
                }
                lowLatencyModeProperty = backends.Count > 0 ? new SerializedObject(backends.ToArray()).FindProperty("useLowLatency") : null;
            }
        }

        struct ScreenProperties {
            public readonly SerializedProperty screenTargetsProperty;
            public readonly SerializedProperty screenTargetModesProperty;
            public readonly SerializedProperty screenTargetIndecesProperty;
            public readonly SerializedProperty screenTargetPropertyNamesProperty;
            public readonly SerializedProperty screenTargetDefaultTexturesProperty;
            public readonly SerializedProperty avProPropertyNamesProperty;
            public readonly SerializedProperty rtScreenTargetSTsProperty;

            public ScreenProperties(
                SerializedProperty screenTargetsProperty,
                SerializedProperty screenTargetModesProperty,
                SerializedProperty screenTargetIndecesProperty,
                SerializedProperty screenTargetPropertyNamesProperty,
                SerializedProperty screenTargetDefaultTexturesProperty,
                SerializedProperty avProPropertyNamesProperty,
                SerializedProperty rtScreenTargetSTsProperty
            ) {
                this.screenTargetsProperty = screenTargetsProperty;
                this.screenTargetModesProperty = screenTargetModesProperty;
                this.screenTargetIndecesProperty = screenTargetIndecesProperty;
                this.screenTargetPropertyNamesProperty = screenTargetPropertyNamesProperty;
                this.screenTargetDefaultTexturesProperty = screenTargetDefaultTexturesProperty;
                this.avProPropertyNamesProperty = avProPropertyNamesProperty;
                this.rtScreenTargetSTsProperty = rtScreenTargetSTsProperty;
            }

            public ScreenProperties(SerializedObject serializedObject) : this(
                serializedObject.FindProperty("screenTargets"),
                serializedObject.FindProperty("screenTargetModes"),
                serializedObject.FindProperty("screenTargetIndeces"),
                serializedObject.FindProperty("screenTargetPropertyNames"),
                serializedObject.FindProperty("screenTargetDefaultTextures"),
                serializedObject.FindProperty("avProPropertyNames"),
                serializedObject.FindProperty("rtScreenTargetSTs")
            ) { }
        }
    }
}