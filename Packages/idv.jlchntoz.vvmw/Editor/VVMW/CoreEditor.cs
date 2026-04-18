using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Designer;
using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(Core))]
    public class CoreEditor : VVMWEditorBase {
        readonly Dictionary<Core, UdonSharpBehaviour> autoPlayControllers = new Dictionary<Core, UdonSharpBehaviour>();
        readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        static readonly string[] materialModeOptions = new string[3];
        static string[] playerNames;
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
        SerializedProperty muteOnOutOfRangeProperty;
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
        SerializedReorderableList playerHandlersList, audioSourcesList, targetsList;
        List<bool> screenTargetVisibilityState;
        Editor autoPlayControllerEditor;
        Editor[] playerHandlerEditors;
        bool errorHandlingFoldout, playerHandlersFoldout, moduleSettingsFoldout, otherSettingsFoldout, extraSettingsFoldout;

        protected override void OnEnable() {
            base.OnEnable();
            playerHandlersProperty = serializedObject.FindProperty("playerHandlers");
            playerHandlersList = new SerializedReorderableList(playerHandlersProperty) {
                drawHeaderCallback = DrawPlayerHandlersListHeader,
            };
            audioSourcesProperty = serializedObject.FindProperty("audioSources");
            audioSourcesList = new SerializedReorderableList(audioSourcesProperty) {
                drawHeaderCallback = DrawAudioSourcesListHeader,
            };
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
            muteOnOutOfRangeProperty = serializedObject.FindProperty("muteOnOutOfRange");
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
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (autoPlayControllerEditor) DestroyImmediate(autoPlayControllerEditor);
            if (playerHandlerEditors != null)
                foreach (var editor in playerHandlerEditors)
                    if (editor) DestroyImmediate(editor);
        }

        public override void DrawEmbeddedInspectorGUI() {
            var autoPlayControllerEditor = GetAutoPlayControllerEditor();
            if (autoPlayControllerEditor != null)
                autoPlayControllerEditor.serializedObject.Update();
            DrawCommonSettings(autoPlayControllerEditor);
            HorizontalLine();
            DrawDefaultBehaviourSettings(autoPlayControllerEditor);
            HorizontalLine();
            DrawAdvancedSettings(autoPlayControllerEditor);
            if (autoPlayControllerEditor != null)
                autoPlayControllerEditor.serializedObject.ApplyModifiedProperties();
        }

        void DrawCommonSettings(VVMWEditorBase controllerEditor) {
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.commonSettings"), EditorStyles.boldLabel);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor)
                frontendHandlerEditor.DrawCommonSettings();
            else if (controllerEditor != null)
                controllerEditor.DrawEmbeddedInspectorGUI();
        }

        void DrawDefaultBehaviourSettings(VVMWEditorBase controllerEditor) {
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.defaultBehaviourSettings"), EditorStyles.boldLabel);
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor) {
                frontendHandlerEditor.DrawAutoPlaySettings();
                frontendHandlerEditor.DrawDefaultPlaylist();
            } else {
                frontendHandlerEditor = null;
                DrawAutoPlayField();
            }
            EditorGUILayout.PropertyField(defaultVolumeProperty);
            EditorGUILayout.PropertyField(defaultMutedProperty);
            EditorGUILayout.PropertyField(muteOnOutOfRangeProperty);
            if (frontendHandlerEditor != null)
                frontendHandlerEditor.DrawRepeatShuffleProperty();
            else {
                EditorGUILayout.PropertyField(loopProperty);
                EditorGUILayout.PropertyField(autoPlayDelayProperty);
                if (autoPlayDelayProperty.floatValue < 0) autoPlayDelayProperty.floatValue = 0;
            }
        }

        void DrawAutoPlayField() {
            int autoPlayPlayerType = autoPlayPlayerTypeProperty.intValue - 1;
            var playerType = playerTypes != null && autoPlayPlayerType >= 0 && autoPlayPlayerType < playerTypes.Length ? playerTypes[autoPlayPlayerType] : PlayerType.Unknown;
            TrustedUrlUtils.DrawUrlField(defaultUrlProperty, playerType.ToTrustUrlType(BuildTarget.StandaloneWindows64));
            if (!string.IsNullOrEmpty(defaultUrlProperty.FindPropertyRelative("url").stringValue)) {
                TrustedUrlUtils.DrawUrlField(defaultQuestUrlProperty, playerType.ToTrustUrlType(BuildTarget.Android));
                if (DrawPlayerDropdown(playerHandlersProperty, autoPlayPlayerTypeProperty, ref autoPlayPlayerType))
                    autoPlayPlayerTypeProperty.intValue = autoPlayPlayerType + 1;
            }
        }

        void DrawAdvancedSettings(VVMWEditorBase controllerEditor) {
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.advancedSettings"), EditorStyles.boldLabel);
            DrawErrorHandlingSettings();
            DrawPlayerHandlers();
            DrawModuleSettings();
            DrawOtherSettings();
            DrawExtraSettings(controllerEditor);
            targetsList.DoLayoutList();
        }

        void DrawErrorHandlingSettings() {
            errorHandlingFoldout = EditorGUILayout.Foldout(errorHandlingFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.errorHandlingSettings"), true);
            if (!errorHandlingFoldout) return;
            EditorGUILayout.PropertyField(totalRetryCountProperty);
            EditorGUILayout.PropertyField(fallbackRetryCountProperty);
            EditorGUILayout.PropertyField(retryDelayProperty);
            EditorGUILayout.PropertyField(timeDriftDetectThresholdProperty);
        }

        void DrawModuleSettings() {
            moduleSettingsFoldout = EditorGUILayout.Foldout(moduleSettingsFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.moduleSettings"), true);
            if (!moduleSettingsFoldout) return;
            DrawScreenList();
            audioSourcesList.DoLayoutList();
            var newAudioSource = EditorGUILayout.ObjectField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.audioSources:add"), null, typeof(AudioSource), true) as AudioSource;
            if (newAudioSource != null) {
                bool hasExisting = false;
                for (int i = 0, count = audioSourcesProperty.arraySize; i < count; i++)
                    if (audioSourcesProperty.GetArrayElementAtIndex(i).objectReferenceValue == newAudioSource) {
                        hasExisting = true;
                        break;
                    }
                if (!hasExisting) {
                    var index = audioSourcesProperty.arraySize++;
                    audioSourcesProperty.GetArrayElementAtIndex(index).objectReferenceValue = newAudioSource;
                }
            }
            EditorGUILayout.PropertyField(audioLinkProperty);
            EditorGUILayout.PropertyField(yttlManagerProperty);
            EditorGUILayout.PropertyField(broadcastScreenTextureProperty);
            if (broadcastScreenTextureProperty.boolValue)
                EditorGUILayout.PropertyField(broadcastScreenTextureNameProperty);
            EditorGUILayout.PropertyField(realtimeGIUpdateIntervalProperty);
        }

        void DrawOtherSettings() {
            otherSettingsFoldout = EditorGUILayout.Foldout(otherSettingsFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.otherSettings"), true);
            if (!otherSettingsFoldout) return;
            EditorGUILayout.PropertyField(urlInputFilterProperty);
            EditorGUILayout.PropertyField(defaultTextureProperty);
            if (defaultTextureProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.defaultTexture:empty_message"), MessageType.Error);
            EditorGUILayout.PropertyField(syncedProperty);
#if VRC_ENABLE_PLAYER_PERSISTENCE
            EditorGUILayout.PropertyField(enablePersistenceProperty);
#endif
        }

        void DrawExtraSettings(VVMWEditorBase controllerEditor) {
            extraSettingsFoldout = EditorGUILayout.Foldout(extraSettingsFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.extraSettings"), true);
            if (!extraSettingsFoldout) return;
            if (controllerEditor is FrontendHandlerEditor frontendHandlerEditor)
                frontendHandlerEditor.DrawExtraSettings();
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
            using (new EditorGUI.PropertyScope(rect, content, autoPlayPlayerTypeProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                rect = EditorGUI.PrefixLabel(rect, content);
                autoPlayPlayerType = EditorGUI.Popup(rect, autoPlayPlayerType, playerNames);
                if (changed.changed) return true;
            }
            return false;
        }

        void DrawPlayerHandlers() {
            bool expanded = playerHandlersProperty.isExpanded;
            using (var change = new EditorGUI.ChangeCheckScope()) {
                expanded = EditorGUILayout.Foldout(expanded, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.playerHandlers"), true);
                if (change.changed) playerHandlersProperty.isExpanded = expanded;
            }
            if (expanded)
                playerHandlersList.DoLayoutList();
            else {
                int count = playerHandlersProperty.arraySize;
                if (playerHandlerEditors == null || playerHandlerEditors.Length < count)
                    playerHandlerEditors = new Editor[count];
                using (new EditorGUI.IndentLevelScope())
                for (int i = 0, drawnCount = 0; i < count; i++) {
                    var playerHandlerProperty = playerHandlersProperty.GetArrayElementAtIndex(i);
                    var playerHandler = playerHandlerProperty.objectReferenceValue as AbstractMediaPlayerHandler;
                    if (!playerHandler) continue;
                    if (editorTypes.TryGetValue(playerHandler.GetType(), out var editorType))
                        CreateCachedEditor(playerHandler, editorType, ref playerHandlerEditors[i]);
                    if (!(playerHandlerEditors[i] is VVMWEditorBase playerHandlerEditor)) continue;
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

        void DrawAudioSourcesListHeader(Rect rect) {
            var tempContent = i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.setupSpeakers");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            EditorGUI.LabelField(rect, i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.audioSources"));
            if (GUI.Button(buttonRect, tempContent, miniButtonStyle)) {
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
        }

        void DrawScreenList() {
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
                EditorGUIUtility.labelWidth -= 16;
                using (new EditorGUILayout.HorizontalScope()) {
                    screenTargetVisibilityState[i] = EditorGUILayout.Toggle(screenTargetVisibilityState[i], EditorStyles.foldout, GUILayout.Width(13));
                    var screenConfigurator = ScreenConfigurator.GetInstance(
                        targetProperty.objectReferenceValue as Renderer,
                        screenTargetIndecesProperty.GetArrayElementAtIndex(i).intValue
                    );
                    var label = i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTarget", i + 1);
                    bool deleteElement = false;
                    if (screenConfigurator && screenConfigurator.core == target) {
                        using (new EditorGUI.DisabledScope(true))
                            EditorGUILayout.ObjectField(label, screenConfigurator, typeof(ScreenConfigurator), true);
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
                        EditorGUILayout.PropertyField(targetProperty, label);
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
                        else if (value is Renderer) { }
                        else if (value is Material) { }
                        else if (value is RawImage) { }
                        else targetProperty.objectReferenceValue = null;
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
                EditorGUIUtility.labelWidth += 16;
                if (i >= 0 && screenTargetVisibilityState[i])
                    using (new EditorGUI.IndentLevelScope())
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
                        } else  if (targetProperty.objectReferenceValue is RenderTexture) {
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
                                screenTargetPropertyNamesProperty.GetArrayElementAtIndex(i),
                                avProPropertyNamesProperty.GetArrayElementAtIndex(i),
                                ref useST, selectedShader, materials
                            );
                        DrawScreenTextureOptions(
                            screenTargetDefaultTexturesProperty.GetArrayElementAtIndex(i),
                            defaultTextureProperty
                        );
                        SetScreenMode(modeProperty, mode, useST, blitFlags);
                    }
            }
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newTarget = EditorGUILayout.ObjectField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTarget:add"), null, typeof(UnityObject), true);
                if (changed.changed && newTarget != null) {
                    if (newTarget is ScreenConfigurator sc){
                        using (var scso = new SerializedObject(sc)) {
                            scso.FindProperty("core").objectReferenceValue = target;
                            scso.ApplyModifiedProperties();
                        }
                        serializedObject.Update();
                    } else if (AppendScreen(
                        newTarget, new ScreenProperties(
                        screenTargetsProperty,
                        screenTargetModesProperty,
                        screenTargetIndecesProperty,
                        screenTargetPropertyNamesProperty,
                        screenTargetDefaultTexturesProperty,
                        avProPropertyNamesProperty,
                        rtScreenTargetSTsProperty
                    ))) screenTargetVisibilityState.Add(true);
                }
            }
            EditorGUILayout.Space();
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
        }

        public static void DrawScreenTextureOptions(
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
                if (changed.changed) textureProperty.objectReferenceValue = texture;
            }
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
                if (newTarget is AudioSource)
                    AppendElement(so.FindProperty("audioSources"), newTarget);
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