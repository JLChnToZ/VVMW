using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEditor;
using UdonSharpEditor;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(Core))]
    public class CoreEditor : Editor {
        static GUIContent tempContent;
        static readonly string[] materialModeOptions = new [] { "Property Block", "Shared Material", "Cloned Materal" };
        SerializedProperty playerHandlersProperty;
        SerializedProperty audioSourcesProperty;
        SerializedProperty defaultUrlProperty;
        SerializedProperty defaultQuestUrlProperty;
        SerializedProperty autoPlayPlayerTypeProperty;
        SerializedProperty syncedProperty;
        SerializedProperty totalRetryCountProperty;
        SerializedProperty retryDelayProperty;
        SerializedProperty defaultVolumeProperty;
        SerializedProperty defaultMutedProperty;
        SerializedProperty loopProperty;
        SerializedProperty audioLinkProperty;
        SerializedProperty targetsProperty;
        SerializedProperty defaultTextureProperty;
        SerializedProperty screenTargetsProperty;
        SerializedProperty screenTargetModesProperty;
        SerializedProperty screenTargetIndecesProperty;
        SerializedProperty screenTargetPropertyNamesProperty;
        SerializedProperty screenTargetDefaultTexturesProperty;
        SerializedProperty avProPropertyNamesProperty;
        ReorderableListUtils playerHandlersList, audioSourcesList, targetsList;
        string[] playerNames;
        List<bool> screenTargetVisibilityState;

        void OnEnable() {
            playerHandlersProperty = serializedObject.FindProperty("playerHandlers");
            playerHandlersList = new ReorderableListUtils(playerHandlersProperty);
            playerHandlersList.list.drawHeaderCallback = DrawPlayerHandlersListHeader;
            audioSourcesProperty = serializedObject.FindProperty("audioSources");
            audioSourcesList = new ReorderableListUtils(audioSourcesProperty);
            audioSourcesList.list.drawHeaderCallback = DrawAudioSourcesListHeader;
            defaultUrlProperty = serializedObject.FindProperty("defaultUrl");
            defaultQuestUrlProperty = serializedObject.FindProperty("defaultQuestUrl");
            autoPlayPlayerTypeProperty = serializedObject.FindProperty("autoPlayPlayerType");
            syncedProperty = serializedObject.FindProperty("synced");
            totalRetryCountProperty = serializedObject.FindProperty("totalRetryCount");
            retryDelayProperty = serializedObject.FindProperty("retryDelay");
            defaultVolumeProperty = serializedObject.FindProperty("defaultVolume");
            defaultMutedProperty = serializedObject.FindProperty("defaultMuted");
            loopProperty = serializedObject.FindProperty("loop");
            audioLinkProperty = serializedObject.FindProperty("audioLink");
            screenTargetsProperty = serializedObject.FindProperty("screenTargets");
            screenTargetModesProperty = serializedObject.FindProperty("screenTargetModes");
            screenTargetIndecesProperty = serializedObject.FindProperty("screenTargetIndeces");
            screenTargetPropertyNamesProperty = serializedObject.FindProperty("screenTargetPropertyNames");
            screenTargetDefaultTexturesProperty = serializedObject.FindProperty("screenTargetDefaultTextures");
            avProPropertyNamesProperty = serializedObject.FindProperty("avProPropertyNames");
            defaultTextureProperty = serializedObject.FindProperty("defaultTexture");
            targetsProperty = serializedObject.FindProperty("targets");
            targetsList = new ReorderableListUtils(targetsProperty);
            screenTargetVisibilityState = new List<bool>();
            for (int i = 0, count = screenTargetsProperty.arraySize; i < count; i++)
                screenTargetVisibilityState.Add(false);
        }

        public override void OnInspectorGUI() {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();
            EditorGUILayout.PropertyField(defaultUrlProperty);
            if (!string.IsNullOrEmpty(defaultUrlProperty.FindPropertyRelative("url").stringValue)) {
                EditorGUILayout.PropertyField(defaultQuestUrlProperty);
                if (playerNames == null || playerNames.Length != playerHandlersProperty.arraySize)
                    playerNames = new string[playerHandlersProperty.arraySize];
                for (int i = 0; i < playerNames.Length; i++) {
                    var playerHandler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as VideoPlayerHandler;
                    if (playerHandler == null)
                        playerNames[i] = "null";
                    else if (string.IsNullOrEmpty(playerHandler.playerName))
                        playerNames[i] = playerHandler.name;
                    else
                        playerNames[i] = playerHandler.playerName;
                }
                int selectedIndex = autoPlayPlayerTypeProperty.intValue - 1;
                var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                var content = GetTempContent(autoPlayPlayerTypeProperty);
                using (new EditorGUI.PropertyScope(rect, content, autoPlayPlayerTypeProperty))
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    rect = EditorGUI.PrefixLabel(rect, content);
                    selectedIndex = EditorGUI.Popup(rect, selectedIndex, playerNames);
                    if (changed.changed) autoPlayPlayerTypeProperty.intValue = selectedIndex + 1;
                }
            }
            EditorGUILayout.PropertyField(loopProperty);
            EditorGUILayout.PropertyField(totalRetryCountProperty);
            EditorGUILayout.PropertyField(retryDelayProperty);
            EditorGUILayout.Space();
            playerHandlersList.list.DoLayoutList();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(defaultTextureProperty);
            if (defaultTextureProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox("It is required to set a default texture to display when no video is playing.", MessageType.Error);
            DrawScreenList();
            audioSourcesList.list.DoLayoutList();
            var newAudioSource = EditorGUILayout.ObjectField("Add Audio Source", null, typeof(AudioSource), true) as AudioSource;
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
            targetsList.list.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawPlayerHandlersListHeader(Rect rect) {
            GetTempContent("Auto Find");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            if (GUI.Button(buttonRect, tempContent, miniButtonStyle)) {
                var handlers = (target as Core).GetComponentsInChildren<VideoPlayerHandler>(true);
                playerHandlersProperty.arraySize = handlers.Length;
                for (int i = 0; i < handlers.Length; i++)
                    playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue = handlers[i];
            }
            playerHandlersList.DrawHeader(rect);
        }

        void DrawAudioSourcesListHeader(Rect rect) {
            GetTempContent("Setup Speakers");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            if (GUI.Button(buttonRect, tempContent, miniButtonStyle)) {
                Undo.IncrementCurrentGroup();
                int undoGroup = Undo.GetCurrentGroup();
                var builtinPlayerHandlers = new List<VideoPlayerHandler>();
                VideoPlayerHandler avProPlayerHandler = null; // only one avpro player handler is supported
                bool hasMultipleAvProPlayerHandler = false;
                for (int i = 0, count = playerHandlersProperty.arraySize; i < count; i++) {
                    var playerHandler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as VideoPlayerHandler;
                    if (playerHandler == null) continue;
                    if (!playerHandler.isAvPro)
                        builtinPlayerHandlers.Add(playerHandler);
                    else if (avProPlayerHandler == null)
                        avProPlayerHandler = playerHandler;
                    else
                        hasMultipleAvProPlayerHandler = true;
                }
                if (audioSourcesProperty.arraySize > 1)
                    EditorUtility.DisplayDialog("Info", "Multiple audio source attached, therefore only the first one will consider primary and apply to Unity (built-in) video player.", "OK");
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
                    EditorUtility.DisplayDialog("Info", "Multiple AVPro video players attached, therefore you will need to manually setup the speakers.", "OK");
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
                Undo.SetCurrentGroupName("Setup Speakers");
                Undo.CollapseUndoOperations(undoGroup);
            }
            audioSourcesList.DrawHeader(rect);
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
                    EditorGUILayout.PropertyField(targetProperty, GetTempContent($"Video Screen Target {i + 1}"));
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
                    if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false)))
                        targetProperty.objectReferenceValue = null;
                }
                EditorGUIUtility.labelWidth += 16;
                if (screenTargetVisibilityState[i])
                    using (new EditorGUI.IndentLevelScope()) {
                        int mode = modeProperty.intValue & 0x7;
                        bool useST = (modeProperty.intValue & 0x8) != 0;
                        bool showMaterialOptions = false;
                        if (targetProperty.objectReferenceValue is Material) {
                            mode = 0;
                            showMaterialOptions = true;
                        } else if (targetProperty.objectReferenceValue is Renderer renderer) {
                            var indexProperty = screenTargetIndecesProperty.GetArrayElementAtIndex(i);
                            if (mode != 1 && mode != 2 && mode != 3) mode = 1;
                            mode = EditorGUILayout.Popup("Mode", mode - 1, materialModeOptions) + 1;
                            var materials = renderer.sharedMaterials;
                            string[] indexNames = new string[materials.Length + 1];
                            indexNames[0] = "All";
                            for (int j = 0; j < materials.Length; j++)
                                if (materials[j] != null)
                                    indexNames[j + 1] = $"({j}) {materials[j].name} ({materials[j].shader.name.Replace("/", ".")})";
                                else
                                    indexNames[j + 1] = $"({j}) null";
                            indexProperty.intValue = EditorGUILayout.Popup("Material", indexProperty.intValue + 1, indexNames) - 1;
                            showMaterialOptions = true;
                        } else if (targetProperty.objectReferenceValue is RawImage) {
                            mode = 4;
                        } else {
                            Utils.DeleteElement(screenTargetsProperty, i);
                            Utils.DeleteElement(screenTargetModesProperty, i);
                            Utils.DeleteElement(screenTargetIndecesProperty, i);
                            Utils.DeleteElement(screenTargetPropertyNamesProperty, i);
                            Utils.DeleteElement(avProPropertyNamesProperty, i);
                            Utils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                            screenTargetVisibilityState.RemoveAt(i);
                            i--;
                            length--;
                            continue;
                        }
                        if (showMaterialOptions) {
                            var nameProperty = screenTargetPropertyNamesProperty.GetArrayElementAtIndex(i);
                            var avProProperty = avProPropertyNamesProperty.GetArrayElementAtIndex(i);
                            EditorGUILayout.PropertyField(nameProperty, GetTempContent("Video Texture Property Name", "The name of the property in material to set the video texture."));
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                useST = EditorGUILayout.Toggle(GetTempContent("Use Scale Offset", "Will use scale offset (_Texture_ST) to adjust the texture if it is flipped upside-down."), useST);
                                if (!useST) EditorGUILayout.PropertyField(avProProperty, GetTempContent("AVPro Flag Property Name", "If it is using AVPro player, this property value will set to 1, otherwise 0."));
                            }
                        }
                        var textureProperty = screenTargetDefaultTexturesProperty.GetArrayElementAtIndex(i);
                        var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                        var label = GetTempContent("Default Texture", "The texture to display when no video is playing. Will use the global default texture if it is null.");
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
                var newTarget = EditorGUILayout.ObjectField(GetTempContent("Add Video Screen Target", "Drag renderers, materials, custom render textures, UI raw images here to receive video texture."), null, typeof(Object), true);
                if (changed.changed && newTarget != null) {
                    if (newTarget is CustomRenderTexture crt)
                        newTarget = crt.material;
                    if (newTarget is Material material) {
                        AppendElement(screenTargetsProperty, material);
                        AppendElement(screenTargetModesProperty, 0);
                        var mainTexturePropertyName = FindMainTexturePropertyName(material);
                        AppendElement(screenTargetPropertyNamesProperty, mainTexturePropertyName);
                        AppendElement(screenTargetDefaultTexturesProperty, material.GetTexture(mainTexturePropertyName));
                    } else if (newTarget is Renderer renderer || (newTarget is GameObject rendererGO && rendererGO.TryGetComponent(out renderer))) {
                        AppendElement(screenTargetsProperty, renderer);
                        AppendElement(screenTargetModesProperty, 1);
                        material = renderer.sharedMaterial;
                        var mainTexturePropertyName = FindMainTexturePropertyName(material);
                        AppendElement(screenTargetPropertyNamesProperty, mainTexturePropertyName);
                        AppendElement(screenTargetDefaultTexturesProperty, material != null ? material.GetTexture(mainTexturePropertyName) : null);
                    } else if (newTarget is RawImage rawImage || (newTarget is GameObject rawImageGO && rawImageGO.TryGetComponent(out rawImage))) {
                        AppendElement(screenTargetsProperty, rawImage);
                        AppendElement(screenTargetModesProperty, 4);
                        AppendElement(screenTargetPropertyNamesProperty, "");
                        AppendElement(screenTargetDefaultTexturesProperty, rawImage.texture);
                    } else goto skip;
                    AppendElement(screenTargetIndecesProperty, -1);
                    AppendElement(avProPropertyNamesProperty, "_IsAVProVideo");
                    screenTargetVisibilityState.Add(true);
                    skip:;
                }
            }
            EditorGUILayout.Space();
        }

        static string FindMainTexturePropertyName(Material material) {
            if (material != null) {
                var shader = material.shader;
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture && shader.GetPropertyFlags(i).HasFlag(ShaderPropertyFlags.MainTexture))
                        return shader.GetPropertyName(i);
            }
            return "_MainTex";
        }

        static GUIContent GetTempContent(SerializedProperty property) => GetTempContent(property.displayName, property.tooltip);

        static GUIContent GetTempContent(string text, string tooltip = "") {
            if (tempContent == null) tempContent = new GUIContent();
            tempContent.text = text;
            tempContent.tooltip = tooltip;
            return tempContent;
        }

        static void AppendElement(SerializedProperty property, Object value) {
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
    }
}