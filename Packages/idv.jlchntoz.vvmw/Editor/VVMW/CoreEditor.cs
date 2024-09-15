using System;
using System.Collections.Generic;
using System.Reflection;
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
using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(Core))]
    public class CoreEditor : VVMWEditorBase {
        static readonly Dictionary<Type, FieldInfo> controllableTypes = new Dictionary<Type, FieldInfo>();
        readonly Dictionary<Core, UdonSharpBehaviour> autoPlayControllers = new Dictionary<Core, UdonSharpBehaviour>();
        static readonly string[] materialModeOptions = new string[3];
        SerializedProperty playerHandlersProperty;
        SerializedProperty audioSourcesProperty;
        SerializedProperty defaultUrlProperty;
        SerializedProperty defaultQuestUrlProperty;
        SerializedProperty autoPlayPlayerTypeProperty;
        SerializedProperty syncedProperty;
        SerializedProperty totalRetryCountProperty;
        SerializedProperty retryDelayProperty;
        SerializedProperty autoPlayDelayProperty;
        SerializedProperty defaultVolumeProperty;
        SerializedProperty defaultMutedProperty;
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
        SerializedProperty broadcastScreenTextureProperty;
        SerializedProperty broadcastScreenTextureNameProperty;
        SerializedProperty realtimeGIUpdateIntervalProperty;
        SerializedProperty timeDriftDetectThresholdProperty;
        SerializedReorderableList playerHandlersList, audioSourcesList, targetsList;
        string[] playerNames;
        PlayerType[] playerTypes;
        List<bool> screenTargetVisibilityState;

        static CoreEditor() {
            AssemblyReloadEvents.afterAssemblyReload += GatherControlledTypes;
            GatherControlledTypes();
        }

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
            retryDelayProperty = serializedObject.FindProperty("retryDelay");
            autoPlayDelayProperty = serializedObject.FindProperty("autoPlayDelay");
            defaultVolumeProperty = serializedObject.FindProperty("defaultVolume");
            defaultMutedProperty = serializedObject.FindProperty("defaultMuted");
            loopProperty = serializedObject.FindProperty("loop");
            audioLinkProperty = serializedObject.FindProperty("audioLink");
            yttlManagerProperty = serializedObject.FindProperty("yttl");
            screenTargetsProperty = serializedObject.FindProperty("screenTargets");
            screenTargetModesProperty = serializedObject.FindProperty("screenTargetModes");
            screenTargetIndecesProperty = serializedObject.FindProperty("screenTargetIndeces");
            screenTargetPropertyNamesProperty = serializedObject.FindProperty("screenTargetPropertyNames");
            screenTargetDefaultTexturesProperty = serializedObject.FindProperty("screenTargetDefaultTextures");
            avProPropertyNamesProperty = serializedObject.FindProperty("avProPropertyNames");
            broadcastScreenTextureProperty = serializedObject.FindProperty("broadcastScreenTexture");
            broadcastScreenTextureNameProperty = serializedObject.FindProperty("broadcastScreenTextureName");
            defaultTextureProperty = serializedObject.FindProperty("defaultTexture");
            realtimeGIUpdateIntervalProperty = serializedObject.FindProperty("realtimeGIUpdateInterval");
            timeDriftDetectThresholdProperty = serializedObject.FindProperty("timeDriftDetectThreshold");
            targetsList = new SerializedReorderableList(serializedObject.FindProperty("targets"));
            screenTargetVisibilityState = new List<bool>();
            for (int i = 0, count = screenTargetsProperty.arraySize; i < count; i++)
                screenTargetVisibilityState.Add(false);
            GetControlledTypesOnScene();
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            serializedObject.Update();
            DrawAutoPlayField();
            EditorGUILayout.PropertyField(totalRetryCountProperty);
            EditorGUILayout.PropertyField(retryDelayProperty);
            EditorGUILayout.PropertyField(timeDriftDetectThresholdProperty);
            EditorGUILayout.Space();
            playerHandlersList.DoLayoutList();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(defaultTextureProperty);
            if (defaultTextureProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.Core.defaultTexture:empty_message"), MessageType.Error);
            DrawScreenList();
            EditorGUILayout.PropertyField(broadcastScreenTextureProperty);
            if (broadcastScreenTextureProperty.boolValue)
                EditorGUILayout.PropertyField(broadcastScreenTextureNameProperty);
            EditorGUILayout.PropertyField(realtimeGIUpdateIntervalProperty);
            EditorGUILayout.Space();
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
            EditorGUILayout.PropertyField(defaultVolumeProperty);
            EditorGUILayout.PropertyField(defaultMutedProperty);
            EditorGUILayout.PropertyField(syncedProperty);
            EditorGUILayout.PropertyField(audioLinkProperty);
            EditorGUILayout.PropertyField(yttlManagerProperty);
            EditorGUILayout.Space();
            targetsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawAutoPlayField() {
            if (autoPlayControllers.TryGetValue(target as Core, out var controller)) {
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.EditUrlsIn", controller.name)))
                    Selection.activeGameObject = controller.gameObject;
                return;
            }
            int autoPlayPlayerType = autoPlayPlayerTypeProperty.intValue - 1;
            var playerType = playerTypes != null && autoPlayPlayerType >= 0 && autoPlayPlayerType < playerTypes.Length ? playerTypes[autoPlayPlayerType] : PlayerType.Unknown;
            TrustedUrlUtils.DrawUrlField(defaultUrlProperty, playerType.ToTrustUrlType(BuildTarget.StandaloneWindows64));
            if (!string.IsNullOrEmpty(defaultUrlProperty.FindPropertyRelative("url").stringValue)) {
                TrustedUrlUtils.DrawUrlField(defaultQuestUrlProperty, playerType.ToTrustUrlType(BuildTarget.Android));
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
                var content = FUtils.GetTempContent(autoPlayPlayerTypeProperty);
                using (new EditorGUI.PropertyScope(rect, content, autoPlayPlayerTypeProperty))
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    rect = EditorGUI.PrefixLabel(rect, content);
                    autoPlayPlayerType = EditorGUI.Popup(rect, autoPlayPlayerType, playerNames);
                    if (changed.changed) autoPlayPlayerTypeProperty.intValue = autoPlayPlayerType + 1;
                }
            }
            EditorGUILayout.PropertyField(loopProperty);
            EditorGUILayout.PropertyField(autoPlayDelayProperty);
            if (autoPlayDelayProperty.floatValue < 0) autoPlayDelayProperty.floatValue = 0;
        }

        void DrawPlayerHandlersListHeader(Rect rect) {
            var tempContent = i18n.GetLocalizedContent("VVMW.AutoFind");
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
                        so.FindProperty("primaryAudioSource").objectReferenceValue = primaryAudioSource;
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
            while (screenTargetVisibilityState.Count < length)
                screenTargetVisibilityState.Add(false);
            for (int i = 0; i < length; i++) {
                var targetProperty = screenTargetsProperty.GetArrayElementAtIndex(i);
                var modeProperty = screenTargetModesProperty.GetArrayElementAtIndex(i);
                EditorGUIUtility.labelWidth -= 16;
                using (new EditorGUILayout.HorizontalScope()) {
                    screenTargetVisibilityState[i] = EditorGUILayout.Toggle(screenTargetVisibilityState[i], EditorStyles.foldout, GUILayout.Width(13));
                    EditorGUILayout.PropertyField(targetProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTarget", i + 1));
                    var value = targetProperty.objectReferenceValue;
                    if (value is GameObject gameObject) {
                        if (gameObject.TryGetComponent(out Renderer renderer))
                            targetProperty.objectReferenceValue = renderer;
                        else if (gameObject.TryGetComponent(out RawImage rawImage))
                            targetProperty.objectReferenceValue = rawImage;
                        else targetProperty.objectReferenceValue = null;
                    } else if (value is CustomRenderTexture crt)
                        targetProperty.objectReferenceValue = crt.material;
                    else if (value is Renderer) {}
                    else if (value is Material) {}
                    else if (value is RawImage) {}
                    else targetProperty.objectReferenceValue = null;
                    if (GUILayout.Button(i18n.GetLocalizedContent("VVMW.Remove"), GUILayout.ExpandWidth(false))) {
                        FUtils.DeleteElement(screenTargetsProperty, i);
                        FUtils.DeleteElement(screenTargetModesProperty, i);
                        FUtils.DeleteElement(screenTargetIndecesProperty, i);
                        FUtils.DeleteElement(screenTargetPropertyNamesProperty, i);
                        FUtils.DeleteElement(avProPropertyNamesProperty, i);
                        FUtils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                        screenTargetVisibilityState.RemoveAt(i);
                        i--;
                        length--;
                    }
                }
                EditorGUIUtility.labelWidth += 16;
                materialModeOptions[0] = i18n.GetOrDefault("VVMW.Material.PropertyBlock");
                materialModeOptions[1] = i18n.GetOrDefault("VVMW.Material.SharedMaterial");
                materialModeOptions[2] = i18n.GetOrDefault("VVMW.Material.ClonedMaterial");
                if (i >= 0 && screenTargetVisibilityState[i])
                    using (new EditorGUI.IndentLevelScope())
                    using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                        int mode = modeProperty.intValue & 0x7;
                        bool useST = (modeProperty.intValue & 0x8) != 0;
                        bool showMaterialOptions = false;
                        Shader selectedShader = null;
                        Material[] materials = null;
                        if (targetProperty.objectReferenceValue is Material m) {
                            mode = 0;
                            showMaterialOptions = true;
                            selectedShader = m.shader;
                        } else if (targetProperty.objectReferenceValue is Renderer renderer) {
                            var indexProperty = screenTargetIndecesProperty.GetArrayElementAtIndex(i);
                            if (mode != 1 && mode != 2 && mode != 3) mode = 1;
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
                            showMaterialOptions = true;
                        } else if (targetProperty.objectReferenceValue is RawImage) {
                            mode = 4;
                        } else {
                            FUtils.DeleteElement(screenTargetsProperty, i);
                            FUtils.DeleteElement(screenTargetModesProperty, i);
                            FUtils.DeleteElement(screenTargetIndecesProperty, i);
                            FUtils.DeleteElement(screenTargetPropertyNamesProperty, i);
                            FUtils.DeleteElement(avProPropertyNamesProperty, i);
                            FUtils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                            screenTargetVisibilityState.RemoveAt(i);
                            i--;
                            length--;
                            continue;
                        }
                        if (showMaterialOptions) {
                            var nameProperty = screenTargetPropertyNamesProperty.GetArrayElementAtIndex(i);
                            var avProProperty = avProPropertyNamesProperty.GetArrayElementAtIndex(i);
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
                        var textureProperty = screenTargetDefaultTexturesProperty.GetArrayElementAtIndex(i);
                        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                        var label = i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.screenTargetDefaultTextures");
                        using (new EditorGUI.PropertyScope(rect, label, textureProperty))
                        using (var changed = new EditorGUI.ChangeCheckScope()) {
                            var texture = textureProperty.objectReferenceValue;
                            if (texture == null) texture = defaultTextureProperty.objectReferenceValue;
                            texture = EditorGUI.ObjectField(rect, label, texture, typeof(Texture), false);
                            if (changed.changed) textureProperty.objectReferenceValue = texture;
                        }
                        modeProperty.intValue = mode | (useST ? 0x8 : 0);
                    }
            }
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newTarget = EditorGUILayout.ObjectField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Core.videoScreenTarget:add"), null, typeof(UnityObject), true);
                if (changed.changed && newTarget != null) {
                    if (AppendScreen(
                        newTarget,
                        screenTargetsProperty,
                        screenTargetModesProperty,
                        screenTargetIndecesProperty,
                        screenTargetPropertyNamesProperty,
                        screenTargetDefaultTexturesProperty,
                        avProPropertyNamesProperty
                    )) screenTargetVisibilityState.Add(true);
                }
            }
            EditorGUILayout.Space();
        }

        public static bool AddTarget(Core core, UnityObject newTarget, bool recordUndo = true, bool copyToUdon = false) {
            using (var so = new SerializedObject(core)) {
                if (newTarget is AudioSource)
                    AppendElement(so.FindProperty("audioSources"), newTarget);
                else if (!AppendScreen(
                    newTarget,
                    so.FindProperty("screenTargets"),
                    so.FindProperty("screenTargetModes"),
                    so.FindProperty("screenTargetIndeces"),
                    so.FindProperty("screenTargetPropertyNames"),
                    so.FindProperty("screenTargetDefaultTextures"),
                    so.FindProperty("avProPropertyNames")
                )) return false;
                if (recordUndo)
                    so.ApplyModifiedProperties();
                else
                    so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (copyToUdon) UdonSharpEditorUtility.CopyProxyToUdon(core);
            return true;
        }

        static bool AppendScreen(
            UnityObject newTarget,
            SerializedProperty screenTargetsProperty,
            SerializedProperty screenTargetModesProperty,
            SerializedProperty screenTargetIndecesProperty,
            SerializedProperty screenTargetPropertyNamesProperty,
            SerializedProperty screenTargetDefaultTexturesProperty,
            SerializedProperty avProPropertyNamesProperty
        ) {
            int screenTargetMode;
            Texture defaultTexture;
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
            } else return false;
            AppendElement(screenTargetsProperty, newTarget);
            AppendElement(screenTargetModesProperty, screenTargetMode);
            AppendElement(screenTargetIndecesProperty, -1);
            AppendElement(screenTargetPropertyNamesProperty, mainTexturePropertyName ?? "_MainTex");
            AppendElement(screenTargetDefaultTexturesProperty, defaultTexture);
            AppendElement(avProPropertyNamesProperty, avProPropertyName ?? "_IsAVProVideo");
            return true;
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

        static void GatherControlledTypes() {
            controllableTypes.Clear();
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (var type in assembly.GetTypes()) {
                    if (type.IsAbstract || !type.IsSubclassOf(typeof(UdonSharpBehaviour))) continue;
                    var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (fields.Length == 0) continue;
                    foreach (var field in fields) {
                        if (field.FieldType == typeof(Core) && field.GetCustomAttribute<SingletonCoreControlAttribute>() != null) {
                            controllableTypes[type] = field;
                            break;
                        }
                    }
                }
        }

        void GetControlledTypesOnScene() {
            autoPlayControllers.Clear();
            foreach (var controller in SceneManager.GetActiveScene().IterateAllComponents<UdonSharpBehaviour>())
                if (controllableTypes.TryGetValue(controller.GetType(), out var field) && field.GetValue(controller) is Core coreComponent)
                    autoPlayControllers[coreComponent] = controller;
        }
    }
}