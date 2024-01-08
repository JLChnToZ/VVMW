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

using UnityObject = UnityEngine.Object;
using System.Reflection;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(Core))]
    public class CoreEditor : VVMWEditorBase {
        static readonly Dictionary<Type, FieldInfo> controllableTypes = new Dictionary<Type, FieldInfo>();
        readonly Dictionary<Core, UdonSharpBehaviour> autoPlayControllers = new Dictionary<Core, UdonSharpBehaviour>();
        static GUIContent tempContent;
        static readonly string[] materialModeOptions = new [] { "Property Block", "Shared Material", "Cloned Materal" };
        static GUIStyle textFieldDropDownTextStyle, textFieldDropDownStyle;
        SerializedProperty trustedUrlDomainsProperty;
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
        SerializedProperty yttlManagerProperty;
        SerializedProperty defaultTextureProperty;
        SerializedProperty screenTargetsProperty;
        SerializedProperty screenTargetModesProperty;
        SerializedProperty screenTargetIndecesProperty;
        SerializedProperty screenTargetPropertyNamesProperty;
        SerializedProperty screenTargetDefaultTexturesProperty;
        SerializedProperty avProPropertyNamesProperty;
        SerializedProperty realtimeGIUpdateIntervalProperty;
        SerializedReorderableList playerHandlersList, audioSourcesList, targetsList;
        string[] playerNames;
        bool[] playerTypes;
        List<bool> screenTargetVisibilityState;
        bool showTrustUrlList;

        static CoreEditor() {
            AssemblyReloadEvents.afterAssemblyReload += GatherControlledTypes;
            GatherControlledTypes();
        }

        protected override void OnEnable() {
            base.OnEnable();
            trustedUrlDomainsProperty = serializedObject.FindProperty("trustedUrlDomains");
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
            defaultTextureProperty = serializedObject.FindProperty("defaultTexture");
            realtimeGIUpdateIntervalProperty = serializedObject.FindProperty("realtimeGIUpdateInterval");
            targetsList = new SerializedReorderableList(serializedObject.FindProperty("targets"));
            screenTargetVisibilityState = new List<bool>();
            for (int i = 0, count = screenTargetsProperty.arraySize; i < count; i++)
                screenTargetVisibilityState.Add(false);
            TrustedUrlUtils.CopyTrustedUrlsToStringArray(trustedUrlDomainsProperty, TrustedUrlTypes.AVProDesktop);
            GetControlledTypesOnScene();
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            serializedObject.Update();
            DrawAutoPlayField();
            EditorGUILayout.PropertyField(totalRetryCountProperty);
            EditorGUILayout.PropertyField(retryDelayProperty);
            EditorGUILayout.Space();
            playerHandlersList.DoLayoutList();
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(defaultTextureProperty);
            if (defaultTextureProperty.objectReferenceValue == null)
                EditorGUILayout.HelpBox("It is required to set a default texture to display when no video is playing.", MessageType.Error);
            DrawScreenList();
            EditorGUILayout.PropertyField(realtimeGIUpdateIntervalProperty);
            EditorGUILayout.Space();
            audioSourcesList.DoLayoutList();
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
            EditorGUILayout.PropertyField(yttlManagerProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                showTrustUrlList = EditorGUILayout.Foldout(showTrustUrlList, GetTempContent(
                    "Trusted URL List",
                    "The list of trusted URL domains from VRChat. This list is for display proper error message when the video URL is not trusted."
                ), true);
                if (GUILayout.Button("Update from VRChat", GUILayout.ExpandWidth(false)))
                    TrustedUrlUtils.CopyTrustedUrlsToStringArray(trustedUrlDomainsProperty, TrustedUrlTypes.AVProDesktop);
            }
            if (showTrustUrlList)
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                    for (int i = 0, count = trustedUrlDomainsProperty.arraySize; i < count; i++)
                        EditorGUILayout.LabelField(trustedUrlDomainsProperty.GetArrayElementAtIndex(i).stringValue, EditorStyles.miniLabel);
            EditorGUILayout.Space();
            targetsList.DoLayoutList();
            serializedObject.ApplyModifiedProperties();
        }

        void DrawAutoPlayField() {
            if (autoPlayControllers.TryGetValue(target as Core, out var controller)) {
                if (GUILayout.Button($"Edit URLs in {controller.name}"))
                    Selection.activeGameObject = controller.gameObject;
                return;
            }
            int autoPlayPlayerType = autoPlayPlayerTypeProperty.intValue - 1;
            bool isAvPro = playerTypes != null && autoPlayPlayerType >= 0 && autoPlayPlayerType < playerTypes.Length && playerTypes[autoPlayPlayerType];
            TrustedUrlUtils.DrawUrlField(defaultUrlProperty, isAvPro ? TrustedUrlTypes.AVProDesktop : TrustedUrlTypes.UnityVideo);
            if (!string.IsNullOrEmpty(defaultUrlProperty.FindPropertyRelative("url").stringValue)) {
                TrustedUrlUtils.DrawUrlField(defaultQuestUrlProperty, isAvPro ? TrustedUrlTypes.AVProAndroid : TrustedUrlTypes.UnityVideo);
                if (playerNames == null || playerNames.Length != playerHandlersProperty.arraySize)
                    playerNames = new string[playerHandlersProperty.arraySize];
                if (playerTypes == null || playerTypes.Length != playerHandlersProperty.arraySize)
                    playerTypes = new bool[playerHandlersProperty.arraySize];
                for (int i = 0; i < playerNames.Length; i++) {
                    var playerHandler = playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue as VideoPlayerHandler;
                    if (playerHandler == null)
                        playerNames[i] = "null";
                    else {
                        playerNames[i] = string.IsNullOrEmpty(playerHandler.playerName) ? playerHandler.name : playerHandler.playerName;
                        playerTypes[i] = playerHandler.isAvPro;
                    }
                }
                var rect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight);
                var content = GetTempContent(autoPlayPlayerTypeProperty);
                using (new EditorGUI.PropertyScope(rect, content, autoPlayPlayerTypeProperty))
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    rect = EditorGUI.PrefixLabel(rect, content);
                    autoPlayPlayerType = EditorGUI.Popup(rect, autoPlayPlayerType, playerNames);
                    if (changed.changed) autoPlayPlayerTypeProperty.intValue = autoPlayPlayerType + 1;
                }
            }
            EditorGUILayout.PropertyField(loopProperty);
        }

        void DrawPlayerHandlersListHeader(Rect rect) {
            GetTempContent("Auto Find");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            EditorGUI.LabelField(rect, "Video Player Handlers");
            if (GUI.Button(buttonRect, tempContent, miniButtonStyle)) {
                var handlers = (target as Core).GetComponentsInChildren<VideoPlayerHandler>(true);
                playerHandlersProperty.arraySize = handlers.Length;
                for (int i = 0; i < handlers.Length; i++)
                    playerHandlersProperty.GetArrayElementAtIndex(i).objectReferenceValue = handlers[i];
            }
        }

        void DrawAudioSourcesListHeader(Rect rect) {
            GetTempContent("Setup Speakers");
            var miniButtonStyle = EditorStyles.miniButton;
            var size = miniButtonStyle.CalcSize(tempContent);
            var buttonRect = new Rect(rect.xMax - size.x, rect.y, size.x, rect.height);
            rect.width -= size.x;
            EditorGUI.LabelField(rect, "Audio Sources");
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
                    if (GUILayout.Button("Remove", GUILayout.ExpandWidth(false))) {
                        Utils.DeleteElement(screenTargetsProperty, i);
                        Utils.DeleteElement(screenTargetModesProperty, i);
                        Utils.DeleteElement(screenTargetIndecesProperty, i);
                        Utils.DeleteElement(screenTargetPropertyNamesProperty, i);
                        Utils.DeleteElement(avProPropertyNamesProperty, i);
                        Utils.DeleteElement(screenTargetDefaultTexturesProperty, i);
                        screenTargetVisibilityState.RemoveAt(i);
                        i--;
                        length--;
                    }
                }
                EditorGUIUtility.labelWidth += 16;
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
                            mode = EditorGUILayout.Popup("Mode", mode - 1, materialModeOptions) + 1;
                            materials = renderer.sharedMaterials;
                            string[] indexNames = new string[materials.Length + 1];
                            indexNames[0] = "All";
                            for (int j = 0; j < materials.Length; j++)
                                if (materials[j] != null)
                                    indexNames[j + 1] = $"({j}) {materials[j].name} ({materials[j].shader.name.Replace("/", ".")})";
                                else
                                    indexNames[j + 1] = $"({j}) null";
                            int selectedIndex = indexProperty.intValue + 1;
                            selectedIndex = EditorGUILayout.Popup("Material", selectedIndex, indexNames) - 1;
                            indexProperty.intValue = selectedIndex;
                            selectedShader = selectedIndex >= 0 && selectedIndex <= materials.Length ? materials[selectedIndex].shader : null;
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
                            DrawShaderPropertiesField(
                                nameProperty, GetTempContent("Video Texture Property Name", "The name of the property in material to set the video texture."),
                                selectedShader, materials, ShaderUtil.ShaderPropertyType.TexEnv
                            );
                            using (var changed = new EditorGUI.ChangeCheckScope()) {
                                useST = EditorGUILayout.Toggle(GetTempContent("Use Scale Offset", "Will use scale offset (_Texture_ST) to adjust the texture if it is flipped upside-down."), useST);
                                if (!useST) DrawShaderPropertiesField(
                                    avProProperty, GetTempContent("AVPro Flag Property Name", "If it is using AVPro player, this property value will set to 1, otherwise 0."),
                                    selectedShader, materials, ShaderUtil.ShaderPropertyType.Float
                                );
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
                var newTarget = EditorGUILayout.ObjectField(GetTempContent("Add Video Screen Target", "Drag renderers, materials, custom render textures, UI raw images here to receive video texture."), null, typeof(UnityObject), true);
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

        static void DrawShaderPropertiesField(SerializedProperty property, GUIContent label, Shader selectedShader, Material[] materials, ShaderUtil.ShaderPropertyType type) {
            if (textFieldDropDownTextStyle == null) textFieldDropDownTextStyle = GUI.skin.FindStyle("TextFieldDropDownText");
            if (textFieldDropDownStyle == null) textFieldDropDownStyle = GUI.skin.FindStyle("TextFieldDropDown");
            var controlRect = EditorGUILayout.GetControlRect(true);
            using (new EditorGUI.PropertyScope(controlRect, label, property)) {
                controlRect = EditorGUI.PrefixLabel(controlRect, label);
                var size = textFieldDropDownStyle.CalcSize(GUIContent.none);
                var textRect = controlRect;
                textRect.xMin -= EditorGUI.indentLevel * 15F;
                textRect.xMax -= size.x;
                property.stringValue = EditorGUI.TextField(textRect, property.stringValue, textFieldDropDownTextStyle);
                var buttonRect = controlRect;
                buttonRect.xMin = buttonRect.xMax - size.x;
                buttonRect.size = size;
                using (new EditorGUI.DisabledScope(selectedShader == null && (materials == null || materials.Length == 0))) {
                    if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive, textFieldDropDownStyle)) {
                        var menu = new GenericMenu();
                        if (selectedShader != null)
                            AppendShaderPropertiesToMenu(menu, selectedShader, property, type);
                        else {
                            var shaderSet = new HashSet<Shader>();
                            for (int j = 0; j < materials.Length; j++) {
                                var material = materials[j];
                                if (material == null) continue;
                                shaderSet.Add(material.shader);
                            }
                            foreach (var shader in shaderSet) {
                                if (menu.GetItemCount() > 0) menu.AddSeparator("");
                                AppendShaderPropertiesToMenu(menu, shader, property, type);
                            }
                        }
                        menu.DropDown(controlRect);
                    }
                }
            }
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
                mainTexturePropertyName = FindMainTexturePropertyName(material);
                avProPropertyName = FindAVProPropertyName(material);
                screenTargetMode = avProPropertyName == null ? 8 : 0;
                defaultTexture = material.GetTexture(mainTexturePropertyName);
            } else if (newTarget is Renderer renderer || (newTarget is GameObject rendererGO && rendererGO.TryGetComponent(out renderer))) {
                newTarget = renderer;
                material = renderer.sharedMaterial;
                mainTexturePropertyName = FindMainTexturePropertyName(material);
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

        static void SetValue(object entry) {
            (SerializedProperty prop, string value) = ((SerializedProperty, string))entry;
            prop.stringValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

        static void AppendShaderPropertiesToMenu(GenericMenu menu, Shader shader, SerializedProperty property, ShaderUtil.ShaderPropertyType type) {
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int j = 0; j < count; j++) {
                if (ShaderUtil.GetPropertyType(shader, j) != type) continue;
                var propertyName = ShaderUtil.GetPropertyName(shader, j);
                menu.AddItem(
                    new GUIContent($"{ShaderUtil.GetPropertyDescription(shader, j)} ({propertyName})".Replace('/', '.')),
                    property.stringValue == propertyName, SetValue, (property, propertyName)
                );
            }
        }

        static string FindMainTexturePropertyName(Material material) {
            if (material != null) {
                var shader = material.shader;
                if (shader == null) return "";
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture && shader.GetPropertyFlags(i).HasFlag(ShaderPropertyFlags.MainTexture))
                        return shader.GetPropertyName(i);
            }
            return "_MainTex";
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

        static GUIContent GetTempContent(SerializedProperty property) => GetTempContent(property.displayName, property.tooltip);

        static GUIContent GetTempContent(string text, string tooltip = "") {
            if (tempContent == null) tempContent = new GUIContent();
            tempContent.text = text;
            tempContent.tooltip = tooltip;
            return tempContent;
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

        [MenuItem("Tools/VizVid/Update Trusted Url List")]
        static void UpdateTrustedUrlList() {
            var cores = new List<Core>(SceneManager.GetActiveScene().IterateAllComponents<Core>());
            using (var so = new SerializedObject(cores.ToArray()))
                TrustedUrlUtils.CopyTrustedUrlsToStringArray(so.FindProperty("trustedUrlDomains"), TrustedUrlTypes.AVProDesktop);
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