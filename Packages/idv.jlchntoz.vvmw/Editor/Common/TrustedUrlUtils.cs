using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using VRC.Core;
using UdonSharp;
using UdonSharpEditor;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW.Editors {
    public sealed class TrustedUrlUtils {
        static readonly Dictionary<TrustedUrlTypes, TrustedUrlUtils> instances = new Dictionary<TrustedUrlTypes, TrustedUrlUtils>();
        static readonly AsyncLazy getTrustedUrlsTask = UniTask.Lazy(GetTrustedUrlsLazy);
        static GUIContent tempContent, warningContent;
        readonly Dictionary<string, bool> trustedDomains = new Dictionary<string, bool>();
        readonly Dictionary<string, string> messageCache = new Dictionary<string, string>();
        readonly HashSet<string> supportedProtocols;
        List<string> trustedUrls;

        static TrustedUrlUtils() {
            var stringComparer = StringComparer.OrdinalIgnoreCase;
            var supportedProtocolsCurl = new HashSet<string>(new [] {
                "http", "https",
            }, stringComparer);
            // https://www.renderheads.com/content/docs/AVProVideo/articles/supportedmedia.html
            // https://learn.microsoft.com/en-us/windows/win32/medfound/supported-protocols
            var supportedProtocolsMF = new HashSet<string>(new [] {
                "http", "https", "rtsp", "rtspt", "rtspu", "rtmp", "rtmps",
            }, stringComparer);
            // https://exoplayer.dev/supported-formats.html
            var supportedProtocolsExo = new HashSet<string>(new [] {
                "http", "https", "rtsp", "rtmp",
            }, stringComparer);
            instances[TrustedUrlTypes.UnityVideo] = new TrustedUrlUtils(supportedProtocolsCurl);
            instances[TrustedUrlTypes.AVProDesktop] = new TrustedUrlUtils(supportedProtocolsMF);
            instances[TrustedUrlTypes.AVProAndroid] = new TrustedUrlUtils(supportedProtocolsExo);
            instances[TrustedUrlTypes.ImageUrl] = new TrustedUrlUtils(supportedProtocolsCurl);
            instances[TrustedUrlTypes.StringUrl] = new TrustedUrlUtils(supportedProtocolsCurl);
        }

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

        static async UniTask GetTrustedUrlsLazy() {
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
            if (vrcsdkConfig.HasKey("urlList")) {
                var trustedUrls = vrcsdkConfig.GetList("urlList");
                instances[TrustedUrlTypes.UnityVideo].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProDesktop].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProAndroid].trustedUrls = trustedUrls;
            }
            if (vrcsdkConfig.HasKey("imageHostUrlList"))
                instances[TrustedUrlTypes.ImageUrl].trustedUrls = vrcsdkConfig.GetList("imageHostUrlList");
            if (vrcsdkConfig.HasKey("stringHostUrlList"))
                instances[TrustedUrlTypes.StringUrl].trustedUrls = vrcsdkConfig.GetList("stringHostUrlList");
        }

        public static void CopyTrustedUrlsToStringArray(SerializedProperty stringArray, TrustedUrlTypes urlType) =>
            CopyTrustedUrlsToStringArrayAsync(stringArray, urlType, true).Forget();

        static async UniTask CopyTrustedUrlsToStringArrayAsync(SerializedProperty stringArray, TrustedUrlTypes urlType, bool applyChanges = true) {
            await getTrustedUrlsTask.Task;
            var urlList = instances[urlType].trustedUrls;
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

        public static void DrawUrlField(SerializedProperty urlProperty, TrustedUrlTypes urlType, params GUILayoutOption[] options) {
            var contentRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            DrawUrlField(urlProperty, urlType, contentRect);
        }

        public static void DrawUrlField(SerializedProperty urlProperty, TrustedUrlTypes urlTypes, Rect rect, GUIContent content = null) {
            if (content == null) content = GetContent(urlProperty.displayName, urlProperty.tooltip);
            if (urlProperty.propertyType == SerializedPropertyType.Generic) // VRCUrl
                urlProperty = urlProperty.FindPropertyRelative("url");
            var url = urlProperty.stringValue;
            using (new EditorGUI.PropertyScope(rect, content, urlProperty))
                urlProperty.stringValue = DrawUrlField(url, urlTypes, rect, content);
        }

        public static string DrawUrlField(string url, TrustedUrlTypes urlType, Rect rect, string propertyLabel = null, string propertyTooltip = null) =>
            DrawUrlField(url, urlType, rect, GetContent(propertyLabel, propertyTooltip));

        public static string DrawUrlField(string url, TrustedUrlTypes urlType, Rect rect, GUIContent content) {
            var instnace = instances[urlType];
            var invalidMessage = instnace.GetValidateMessage(url);
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
                    instnace.messageCache.Remove(url);
                    url = newUrl;
                }
            }
            return url;
        }

        TrustedUrlUtils(HashSet<string> supportedProtocols) {
            this.supportedProtocols = supportedProtocols;
        }

        string GetValidateMessage(string url) {
            if (!messageCache.TryGetValue(url, out var invalidMessage)) {
                invalidMessage = "";
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    if (!supportedProtocols.Contains(uri.Scheme))
                        invalidMessage = $"{uri.Scheme} is not a supported protocol.";
                    else if (trustedUrls == null)
                        getTrustedUrlsTask.Task.Forget(); // Force to fetch trusted urls.
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
            return invalidMessage;
        }
    }

    [CustomPropertyDrawer(typeof(TrustUrlCheckAttribute))]
    public class VRCUrlTrustCheckDrawer : PropertyDrawer {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            if (fieldInfo.FieldType != typeof(VRCUrl))
                return EditorGUI.GetPropertyHeight(property, label);
            return EditorGUIUtility.singleLineHeight;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (fieldInfo.FieldType != typeof(VRCUrl)) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            var attr = attribute as TrustUrlCheckAttribute;
            TrustedUrlUtils.DrawUrlField(property, attr.type, position, label);
        }
    }
}