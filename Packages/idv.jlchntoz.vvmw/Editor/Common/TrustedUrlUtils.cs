using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using VRC.Core;
using VRC.SDKBase;
using VRC.SDK3.Editor;
using JLChnToZ.VRC.VVMW.I18N;

namespace JLChnToZ.VRC.VVMW.Editors {
    [InitializeOnLoad]
    public sealed class TrustedUrlUtils {
        public static event Action OnTrustedUrlsReady;
        static readonly Dictionary<TrustedUrlTypes, TrustedUrlUtils> instances = new Dictionary<TrustedUrlTypes, TrustedUrlUtils>();
        static AsyncLazy getTrustedUrlsTask = UniTask.Lazy(GetTrustedUrlsLazy);
        static GUIContent warningContent;
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
            instances[TrustedUrlTypes.AVProIOS] = new TrustedUrlUtils(supportedProtocolsCurl);
            instances[TrustedUrlTypes.ImageUrl] = new TrustedUrlUtils(supportedProtocolsCurl);
            instances[TrustedUrlTypes.StringUrl] = new TrustedUrlUtils(supportedProtocolsCurl);
            if (EditorPrefs.HasKey("VRCSDK_videoHostUrlList")) {
                var trustedUrls = new List<string>(EditorPrefs.GetString("VRCSDK_videoHostUrlList").Split('\n'));
                instances[TrustedUrlTypes.UnityVideo].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProDesktop].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProAndroid].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProIOS].trustedUrls = trustedUrls;
            }
            if (EditorPrefs.HasKey("VRCSDK_imageHostUrlList"))
                instances[TrustedUrlTypes.ImageUrl].trustedUrls = new List<string>(EditorPrefs.GetString("VRCSDK_imageHostUrlList").Split('\n'));
            if (EditorPrefs.HasKey("VRCSDK_stringHostUrlList"))
                instances[TrustedUrlTypes.StringUrl].trustedUrls = new List<string>(EditorPrefs.GetString("VRCSDK_stringHostUrlList").Split('\n'));
            VRCSdkControlPanel.OnSdkPanelEnable += AddBuildHook;
        }

        static void AddBuildHook(object sender, EventArgs e) {
            if (VRCSdkControlPanel.TryGetBuilder(out IVRCSdkWorldBuilderApi builder))
                builder.OnSdkBuildStart += OnBuildStarted;
            getTrustedUrlsTask.Task.Forget();
        }

        static void OnBuildStarted(object sender, object target) => getTrustedUrlsTask.Task.Forget();

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
                try {
                    await initState.Task;
                } catch (Exception ex) {
                    getTrustedUrlsTask = UniTask.Lazy(GetTrustedUrlsLazy); // Retry on next time.
                    throw ex;
                }
            }
            if (vrcsdkConfig.HasKey("urlList")) {
                var trustedUrls = vrcsdkConfig.GetList("urlList");
                instances[TrustedUrlTypes.UnityVideo].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProDesktop].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProAndroid].trustedUrls = trustedUrls;
                instances[TrustedUrlTypes.AVProIOS].trustedUrls = trustedUrls;
                EditorPrefs.SetString("VRCSDK_videoHostUrlList", string.Join("\n", trustedUrls));
            }
            if (vrcsdkConfig.HasKey("imageHostUrlList")) {
                var trustedUrls = vrcsdkConfig.GetList("imageHostUrlList");
                instances[TrustedUrlTypes.ImageUrl].trustedUrls = trustedUrls;
                EditorPrefs.SetString("VRCSDK_imageHostUrlList", string.Join("\n", trustedUrls));
            }
            if (vrcsdkConfig.HasKey("stringHostUrlList")) {
                var trustedUrls = vrcsdkConfig.GetList("stringHostUrlList");
                instances[TrustedUrlTypes.StringUrl].trustedUrls = trustedUrls;
                EditorPrefs.SetString("VRCSDK_stringHostUrlList", string.Join("\n", trustedUrls));
            }
            OnTrustedUrlsReady?.Invoke();
        }

        public static void CopyTrustedUrls(TrustedUrlTypes urlType, ref string[] trustedUrls) {
            var urlList = instances[urlType].trustedUrls;
            if (urlList == null) return;
            if (trustedUrls == null || trustedUrls.Length != urlList.Count)
                trustedUrls = new string[urlList.Count];
            urlList.CopyTo(trustedUrls);
        }

        public static void DrawUrlField(SerializedProperty urlProperty, TrustedUrlTypes urlType, params GUILayoutOption[] options) {
            var contentRect = EditorGUILayout.GetControlRect(true, EditorGUIUtility.singleLineHeight, options);
            DrawUrlField(urlProperty, urlType, contentRect);
        }

        public static void DrawUrlField(SerializedProperty urlProperty, TrustedUrlTypes urlTypes, Rect rect, GUIContent content = null) {
            if (content == null) content = Utils.GetTempContent(urlProperty.displayName, urlProperty.tooltip);
            if (urlProperty.propertyType == SerializedPropertyType.Generic) // VRCUrl
                urlProperty = urlProperty.FindPropertyRelative("url");
            var url = urlProperty.stringValue;
            using (new EditorGUI.PropertyScope(rect, content, urlProperty))
                urlProperty.stringValue = DrawUrlField(url, urlTypes, rect, content);
        }

        public static string DrawUrlField(string url, TrustedUrlTypes urlType, Rect rect, string propertyLabel = null, string propertyTooltip = null) =>
            DrawUrlField(url, urlType, rect, Utils.GetTempContent(propertyLabel, propertyTooltip));

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
                if (url == null) url = "";
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
            if (url == null) return "";
            if (!messageCache.TryGetValue(url, out var invalidMessage)) {
                var i18n = EditorI18N.Instance;
                invalidMessage = "";
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri)) {
                    if (!supportedProtocols.Contains(uri.Scheme))
                        invalidMessage = i18n.GetOrDefault("TrustedUrlUtils.scheme_not_supported", uri.Scheme);
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
                        if (!trusted) invalidMessage = i18n.GetOrDefault("TrustedUrlUtils.url_not_trusted");
                    }
                } else if (!string.IsNullOrEmpty(url))
                    invalidMessage = i18n.GetOrDefault("TrustedUrlUtils.url_invalid");
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