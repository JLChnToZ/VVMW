using System;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(StreamLinkAssigner))]
    public class StreamLinkAssignerEditor : VVMWEditorBase {
        const string SAFE_CHARS = "23456789ABCDEFGHJKMNPQRSTUVWXYZabcdefghjkmnpqrstuvwxyz";
        static readonly string[] randomPrefixes = new[] {
            "apple", "banana", "cherry", "date", "elder", "fig", "grape", "honey", "ice", "juice",
            "kiwi", "lemon", "mango", "nectar", "orange", "pear", "quince", "rasp", "straw",
            "tanger", "vanilla", "water", "xylitol", "yogurt", "zest", "almond", "berry", "candy",
            "dew", "egg", "flour", "grain", "honey", "ice", "jam", "kale", "lime", "mint", "nut",
            "oat", "pea", "quinoa", "rice", "salt", "tea", "umami", "vine", "wheat", "yam",
        };
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
        string sampleKey, samplePCUrl, sampleQuestUrl;

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
            InitGenerator();
        }

        void InitGenerator() {
            usedKeys.Clear();
            generateKeyCount = streamKeysProperty.arraySize;
            if (generateKeyCount == 0) generateKeyCount = 100;
            uniqueIdLength = 5;
            if (PrefabUtility.IsPartOfPrefabAsset(target)) return;
            serializedObject.Update();
            if (string.IsNullOrWhiteSpace(streamKeyTemplateProperty.stringValue)) {
                var sb = new StringBuilder();
                for (int i = UnityEngine.Random.Range(0, 4); i >= 0; i--) {
                    sb.Append(randomPrefixes[UnityEngine.Random.Range(0, randomPrefixes.Length)]);
                    sb.Append('-');
                }
                sb.Append("{0}");
                streamKeyTemplateProperty.stringValue = sb.ToString();
            }
            if (string.IsNullOrWhiteSpace(streamUrlTemplateProperty.stringValue))
                streamUrlTemplateProperty.stringValue = "rtspt://example.com/live/{0}";
            if (string.IsNullOrWhiteSpace(altStreamUrlTemplateProperty.stringValue))
                altStreamUrlTemplateProperty.stringValue = "rtsp://example.com/live/{0}";
            serializedObject.ApplyModifiedProperties();
        }

        protected override void OnDisable() {
            base.OnDisable();
            coreSO?.Dispose();
            coreSO = null;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
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
            bool isEmpty = streamKeysProperty.arraySize == 0;
            using (new EditorGUILayout.VerticalScope(GUI.skin.box)) {
                EditorGUILayout.LabelField(i18N.GetLocalizedContent("HEADER:StreamKeyGenerator"), EditorStyles.boldLabel);
                using (var changeCheck = new EditorGUI.ChangeCheckScope()) {
                    EditorGUILayout.PropertyField(streamKeyTemplateProperty);
                    DrawUrlField(streamUrlTemplateProperty, TrustedUrlTypes.AVProDesktop, i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.streamUrlTemplate"));
                    DrawUrlField(altStreamUrlTemplateProperty, TrustedUrlTypes.AVProAndroid, i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.altStreamUrlTemplate"));
                    generateKeyCount = EditorGUILayout.IntField(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.keyCount"), generateKeyCount);
                    uniqueIdLength = EditorGUILayout.IntField(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.uniqueIdLength"), uniqueIdLength);
                    if (changeCheck.changed || sampleKey == null || samplePCUrl == null || sampleQuestUrl == null) {
                        sampleKey = string.Format(streamKeyTemplateProperty.stringValue, new string('X', uniqueIdLength));
                        samplePCUrl = string.Format(streamUrlTemplateProperty.stringValue, sampleKey);
                        sampleQuestUrl = string.IsNullOrWhiteSpace(altStreamUrlTemplateProperty.stringValue) ? samplePCUrl :
                            string.Format(altStreamUrlTemplateProperty.stringValue, sampleKey);
                    }
                }
                EditorGUILayout.HelpBox(string.Format(
                    i18n.GetOrDefault("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generate_message"),
                    generateKeyCount, samplePCUrl, sampleQuestUrl
                ), MessageType.Info);
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generate")) && (
                        isEmpty ||
                        i18N.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generate"))) {
                        GenerateKeys();
                    }
                    using (new EditorGUI.DisabledScope(isEmpty)) {
                        if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.generateUrls"))) GenerateUrls();
                        if (GUILayout.Button(i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.clear")) &&
                            i18N.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.StreamLinkAssigner.clear")) {
                            ClearStreamKeys();
                        }
                    }
                }
            }
            folded = EditorGUILayout.Foldout(folded, i18N.GetLocalizedContent("JLChnToZ.VRC.VVMW.StreamLinkAssigner.streamKeysAndUrls", streamKeysProperty.arraySize), true);
            if (folded) {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(streamKeysProperty, true);
                EditorGUILayout.PropertyField(streamLinksProperty, true);
                EditorGUILayout.PropertyField(altStreamLinksProperty, true);
                EditorGUI.indentLevel--;
            }
            if (isEmpty) EditorGUILayout.HelpBox(i18N.GetOrDefault("JLChnToZ.VRC.VVMW.StreamLinkAssigner.notgenerated_message"), MessageType.Warning);
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
                if (string.IsNullOrWhiteSpace(altStremUrl)) altStremUrl = streamUrl;
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
            if (string.IsNullOrWhiteSpace(altStremUrl)) altStremUrl = streamUrl;
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

        static void DrawUrlField(SerializedProperty serializedProperty, TrustedUrlTypes trustedUrlTypes, GUIContent content = null) {
            var controlRect = EditorGUILayout.GetControlRect();
            using (var scope = new EditorGUI.PropertyScope(controlRect, content, serializedProperty))
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var urlStr = serializedProperty.stringValue;
                urlStr = TrustedUrlUtils.DrawUrlField(urlStr, trustedUrlTypes, controlRect, scope.content);
                if (changed.changed) serializedProperty.stringValue = urlStr;
            }
        }

    }
}