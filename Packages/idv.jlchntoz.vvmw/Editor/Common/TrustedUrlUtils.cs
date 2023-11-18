using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using VRC.Core;
using UdonSharp;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW {
    public static class TrustedUrlUtils {
        static readonly AsyncLazy<List<string>> getTrustedUrlsTask = UniTask.Lazy(GetTrustedUrlsLazy);
        static readonly Dictionary<string, bool> trustedDomains = new Dictionary<string, bool>();
        static readonly Dictionary<string, string> messageCache = new Dictionary<string, string>();
        static GUIContent tempContent, warningContent;
        static List<string> trustedUrls;
        
        public static UniTask<List<string>> TrustedUrls => getTrustedUrlsTask.Task;

        static GUIContent GetContent(string label, string tooltip = null) {
            if (tempContent == null) tempContent = new GUIContent();
            tempContent.text = label;
            tempContent.tooltip = tooltip;
            return tempContent;
        }

        static GUIContent GetWarningContent(string tooltip) {
            if (warningContent == null) {
                warningContent = new GUIContent {
                    image = EditorGUIUtility.IconContent("console.warnicon.sml").image
                };
            }
            warningContent.tooltip = tooltip;
            return warningContent;
        }

        static async UniTask<List<string>> GetTrustedUrlsLazy() {
            if (trustedUrls == null) {
                var vrcsdkConfig = ConfigManager.RemoteConfig;
                if (!vrcsdkConfig.IsInitialized()) {
                    Debug.Log("[VVMW] VRCSDK config is not initialized, initializing...");
                    var initState = new UniTaskCompletionSource();
                    vrcsdkConfig.Init(
                        () => initState.TrySetResult(),
                        () => initState.TrySetException(new Exception("Failed to initialize VRCSDK config."))
                    );
                    await initState.Task;
                }
                if (!vrcsdkConfig.HasKey("urlList")) {
                    Debug.LogWarning("[VVMW] Failed to fetch trusted url list.");
                    return null;
                }
                trustedUrls = vrcsdkConfig.GetList("urlList");
            }
            return trustedUrls;
        }

        public static void CopyTrustedUrlsToStringArray(SerializedProperty stringArray) =>
            CopyTrustedUrlsToStringArrayAsync(stringArray, true).Forget();

        static async UniTask CopyTrustedUrlsToStringArrayAsync(SerializedProperty stringArray, bool applyChanges = true) {
            var urlList = await TrustedUrls;
            stringArray.arraySize = urlList.Count;
            for (int i = 0; i < urlList.Count; i++) {
                var url = urlList[i];
                if (url.StartsWith("*.")) url = url.Substring(2);
                stringArray.GetArrayElementAtIndex(i).stringValue = url;
            }
            if (!applyChanges) return;
            var so = stringArray.serializedObject;
            so.ApplyModifiedProperties();
            so.Update();
            foreach (var target in so.targetObjects)
                if (target is UdonSharpBehaviour usharp)
                    UdonSharpEditorUtility.CopyProxyToUdon(usharp);
        }

        public static void DrawUrlField(SerializedProperty urlProperty, params GUILayoutOption[] options) {
            var contentRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            DrawUrlField(urlProperty, contentRect);
        }

        public static void DrawUrlField(SerializedProperty urlProperty, Rect rect) {
            var content = GetContent(urlProperty.displayName, urlProperty.tooltip);
            if (urlProperty.propertyType == SerializedPropertyType.Generic)
                urlProperty = urlProperty.FindPropertyRelative("url");
            var url = urlProperty.stringValue;
            using (new EditorGUI.PropertyScope(rect, content, urlProperty))
                urlProperty.stringValue = DrawUrlField(url, rect, content);
        }

        public static string DrawUrlField(string url, Rect rect, string propertyLabel = null, string propertyTooltip = null) =>
            DrawUrlField(url, rect, GetContent(propertyLabel, propertyTooltip));

        public static string DrawUrlField(string url, Rect rect, GUIContent content) {
            if (!messageCache.TryGetValue(url, out var invalidMessage)) {
                invalidMessage = "";
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    if (trustedUrls == null) TrustedUrls.Forget(); // Force to fetch trusted urls.
                    else { // Check domains.
                        var domainName = uri.Host;
                        if (!trustedDomains.TryGetValue(domainName, out var trusted)) {
                            trusted = false;
                            foreach (var trustedUrl in trustedUrls)
                                if (trustedUrl.StartsWith("*.")) {
                                    if (domainName.EndsWith(trustedUrl.Substring(2), StringComparison.OrdinalIgnoreCase)) {
                                        trusted = true;
                                        break;
                                    }
                                } else if (string.Equals(trustedUrl, domainName, StringComparison.OrdinalIgnoreCase)) {
                                    trusted = true;
                                    break;
                                }
                            trustedDomains[domainName] = trusted;
                        }
                        if (!trusted) invalidMessage = "This domain is not trusted by VRChat clients.\nUsers will not be able to access this URL without enabling \"Allow Untrusted URLs\".";
                    }
                } else if (!string.IsNullOrEmpty(url))
                    invalidMessage = "This URL is invalid.";
                messageCache[url] = invalidMessage;
            }
            var rect2 = rect;
            if (!string.IsNullOrEmpty(invalidMessage)) {
                var warnContent = GetWarningContent(invalidMessage);
                var labelStyle = EditorStyles.miniLabel;
                var warnSize = labelStyle.CalcSize(warnContent);
                var warnRect = new Rect(rect2.xMax - warnSize.x, rect2.y, warnSize.x, rect2.height);
                rect2.width -= warnSize.x;
                GUI.Label(warnRect, warnContent, labelStyle);
            }
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                var newUrl = EditorGUI.TextField(rect2, content, url);
                if (changed.changed) {
                    messageCache.Remove(url);
                    url = newUrl;
                }
            }
            return url;
        }
    }
}