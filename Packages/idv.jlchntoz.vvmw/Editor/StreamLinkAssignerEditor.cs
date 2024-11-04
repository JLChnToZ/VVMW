using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(StreamLinkAssigner))]
    public class StreamLinkAssignerEditor : VVMWEditorBase {
        const string SAFE_CHARS = "23456789ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
        readonly HashSet<string> usedKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        SerializedProperty coreProperty,
            frontendHandlerProperty,
            streamKeyTemplateProperty,
            streamUrlTemplateProperty,
            altStreamUrlTemplateProperty,
            currentUserOnlyProperty,
            streamLinksProperty,
            altStreamLinksProperty,
            streamKeysProperty,
            playerIndexProperty,
            autoInterruptProperty,
            autoPlayProperty,
            inputFieldToCopyProperty,
            regenerateButtonProperty,
            playButtonProperty,
            corePlayerHandlersProperty;
        int generateKeyCount, uniqueIdLength;
        bool folded;
        RNGCryptoServiceProvider rng;
        byte[] keyBytes;
        SerializedObject coreSO;
        EditorI18N i18N;

        protected override void OnEnable() {
            base.OnEnable();
            i18N = EditorI18N.Instance;
            coreProperty = serializedObject.FindProperty("core");
            frontendHandlerProperty = serializedObject.FindProperty("frontendHandler");
            streamKeyTemplateProperty = serializedObject.FindProperty("streamKeyTemplate");
            streamUrlTemplateProperty = serializedObject.FindProperty("streamUrlTemplate");
            altStreamUrlTemplateProperty = serializedObject.FindProperty("altStreamUrlTemplate");
            currentUserOnlyProperty = serializedObject.FindProperty("currentUserOnly");
            streamLinksProperty = serializedObject.FindProperty("streamLinks");
            altStreamLinksProperty = serializedObject.FindProperty("altStreamLinks");
            streamKeysProperty = serializedObject.FindProperty("streamKeys");
            playerIndexProperty = serializedObject.FindProperty("playerIndex");
            autoInterruptProperty = serializedObject.FindProperty("autoInterrupt");
            autoPlayProperty = serializedObject.FindProperty("autoPlay");
            inputFieldToCopyProperty = serializedObject.FindProperty("inputFieldToCopy");
            regenerateButtonProperty = serializedObject.FindProperty("regenerateButton");
            playButtonProperty = serializedObject.FindProperty("playButton");
            generateKeyCount = streamKeysProperty.arraySize;
            uniqueIdLength = 5;
            usedKeys.Clear();
        }

        protected override void OnDisable() {
            base.OnDisable();
            coreSO?.Dispose();
            coreSO = null;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            var serializedObject = this.serializedObject;
            serializedObject.Update();
            EditorGUILayout.PropertyField(coreProperty);
            EditorGUILayout.PropertyField(frontendHandlerProperty);
            EditorGUILayout.Space();
            ResolveCoreSerializedObject();
            if (corePlayerHandlersProperty != null) {
                int selected = playerIndexProperty.intValue - 1;
                if (CoreEditor.DrawPlayerDropdown(corePlayerHandlersProperty, playerIndexProperty, ref selected, "JLChnToZ.VRC.VVMW.StreamLinkAssigner.playerIndex"))
                    playerIndexProperty.intValue = selected + 1;
            } else
                EditorGUILayout.PropertyField(playerIndexProperty);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(streamKeyTemplateProperty);
            EditorGUILayout.PropertyField(streamUrlTemplateProperty);
            EditorGUILayout.PropertyField(altStreamUrlTemplateProperty);
            generateKeyCount = EditorGUILayout.IntField(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.keyCount"), generateKeyCount);
            uniqueIdLength = EditorGUILayout.IntField(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.uniqueIdLength"), uniqueIdLength);
            folded = EditorGUILayout.Foldout(folded, i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.streamKeysAndUrls"), true);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generate")) && (
                    streamKeysProperty.arraySize == 0 ||
                    i18N.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generate"))) {
                    GenerateKeys();
                }
                using (new EditorGUI.DisabledScope(streamKeysProperty.arraySize == 0)) {
                    if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generateUrls"))) GenerateUrls();
                    if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.clear")) &&
                        i18N.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.StreamLinkAssigner.clear")) {
                        ClearStreamKeys();
                    }
                }
            }
            if (folded) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(streamKeysProperty, true);
                EditorGUILayout.PropertyField(streamLinksProperty, true);
                EditorGUILayout.PropertyField(altStreamLinksProperty, true);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(currentUserOnlyProperty);
            EditorGUILayout.PropertyField(autoInterruptProperty);
            EditorGUILayout.PropertyField(autoPlayProperty);
            EditorGUILayout.Space();
            EditorGUILayout.PropertyField(inputFieldToCopyProperty);
            EditorGUILayout.PropertyField(regenerateButtonProperty);
            EditorGUILayout.PropertyField(playButtonProperty);
            serializedObject.ApplyModifiedProperties();
        }

        Core GetCore() {
            Core core;
            var frontendHandler = frontendHandlerProperty.objectReferenceValue as FrontendHandler;
            if (frontendHandler) {
                core = frontendHandler.core;
                if (core) return core;
            }
            core = coreProperty.objectReferenceValue as Core;
            return core;
        }

        void ResolveCoreSerializedObject() {
            var core = GetCore();
            if (!core) {
                coreSO?.Dispose();
                coreSO = null;
                corePlayerHandlersProperty = null;
                return;
            } else if (coreSO == null || coreSO.targetObject != core) {
                coreSO?.Dispose();
                coreSO = new SerializedObject(core);
                corePlayerHandlersProperty = coreSO.FindProperty("playerHandlers");
            }
        }

        void GenerateKeys() {
            using (rng = new RNGCryptoServiceProvider()) {
                streamKeysProperty.arraySize = generateKeyCount;
                streamLinksProperty.arraySize = generateKeyCount;
                altStreamLinksProperty.arraySize = generateKeyCount;
                usedKeys.Clear();
                var streamUrl = streamUrlTemplateProperty.stringValue;
                var altStremUrl = altStreamUrlTemplateProperty.stringValue;
                if (string.IsNullOrEmpty(altStremUrl)) altStremUrl = streamUrl;
                for (int i = 0; i < generateKeyCount; i++) {
                    var finalKey = GenerateKey(out var generatedKey);
                    if (finalKey == null) return;
                    streamKeysProperty.GetArrayElementAtIndex(i).stringValue = finalKey;
                    streamLinksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue =
                        string.Format(streamUrl, finalKey, generatedKey);
                    altStreamLinksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue =
                        string.Format(altStremUrl, finalKey, generatedKey);
                }
            }
        }

        string GenerateKey(out string generatedKey) {
            int retryCount = 0;
            if (keyBytes == null || keyBytes.Length != uniqueIdLength)
                keyBytes = new byte[uniqueIdLength];
            do {
                rng.GetBytes(keyBytes);
                var sb = new StringBuilder();
                for (int j = 0; j < uniqueIdLength; j++) {
                    var index = keyBytes[j] % SAFE_CHARS.Length;
                    sb.Append(SAFE_CHARS[index]);
                }
                generatedKey = sb.ToString();
                retryCount++;
                if (retryCount % 3 == 0) {
                    // Try to reseed RNGCryptoServiceProvider every 3 retries
                    rng.Dispose();
                    rng = new RNGCryptoServiceProvider();
                }
                if (retryCount % 5 == 0) {
                    // Try to increase uniqueIdLength every 5 retries
                    uniqueIdLength++;
                    keyBytes = new byte[uniqueIdLength];
                }
                if (retryCount > 10) {
                    // Give up after 10 retries
                    EditorUtility.ClearProgressBar();
                    Debug.LogError("Failed to generate stream key.");
                    return null;
                }
            } while (!usedKeys.Add(generatedKey));
            return string.Format(streamKeyTemplateProperty.stringValue, generatedKey);
        }

        void GenerateUrls() {
            int count = streamKeysProperty.arraySize;
            streamLinksProperty.arraySize = count;
            altStreamLinksProperty.arraySize = count;
            var streamUrl = streamUrlTemplateProperty.stringValue;
            var altStremUrl = altStreamUrlTemplateProperty.stringValue;
            if (string.IsNullOrEmpty(altStremUrl)) altStremUrl = streamUrl;
            for (int i = 0; i < count; i++) {
                streamLinksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue =
                    string.Format(streamUrl, streamKeysProperty.GetArrayElementAtIndex(i).stringValue);
                altStreamLinksProperty.GetArrayElementAtIndex(i).FindPropertyRelative("url").stringValue =
                    string.Format(altStremUrl, streamKeysProperty.GetArrayElementAtIndex(i).stringValue);
            }
        }

        void ClearStreamKeys() {
            streamKeysProperty.arraySize = 0;
            streamLinksProperty.arraySize = 0;
            altStreamLinksProperty.arraySize = 0;
        }

    }
}