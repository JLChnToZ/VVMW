using UnityEngine;
using UnityEditor;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    using Utils = JLChnToZ.VRC.Foundation.Editors.Utils;
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureGUID = "e8354bc2ac14e86498c0983daf484661";
        const string iconGUID = "a24ecd1d23cca9e46871bc17dfe3bd46";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        const string listingsID = "idv.jlchntoz.xtlcdn-listing";
        const string listingsURL = "https://xtlcdn.github.io/vpm/index.json";
        static Texture2D bannerTexture;
        static PackageSelfUpdater selfUpdater;
        static Font font;
        static GUIStyle versionLabelStyle;
        protected static EditorI18N i18n;

        public static void UpdateTitle(GUIContent titleContent, string languageKey, bool unsaved = false) {
            var iconPath = AssetDatabase.GUIDToAssetPath(iconGUID);
            if (iconPath != null) {
                var icon = AssetDatabase.LoadAssetAtPath<Texture>(iconPath);
                titleContent.image = icon;
            }
            var text = i18n.GetOrDefault(languageKey);
            if (unsaved) text += "*";
            titleContent.text = text;
        }

        protected virtual void OnEnable() {
            if (selfUpdater == null) {
                selfUpdater = new PackageSelfUpdater(GetType().Assembly, listingsID, listingsURL);
                selfUpdater.CheckInstallationInBackground();
            }
            if (bannerTexture == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(bannerTextureGUID);
                if (!string.IsNullOrEmpty(assetPath)) bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            if (font == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(fontGUID);
                if (!string.IsNullOrEmpty(assetPath)) font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
            }
            #if VPM_RESOLVER_INCLUDED
            selfUpdater.OnVersionRefreshed += Repaint;
            #endif
            i18n = EditorI18N.Instance;
        }

        protected virtual void OnDisable() {
            #if VPM_RESOLVER_INCLUDED
            if (selfUpdater != null) selfUpdater.OnVersionRefreshed -= Repaint;
            #endif
        }

        public override void OnInspectorGUI() {
            if (bannerTexture != null) {
                var rect = GUILayoutUtility.GetRect(0, 0);
                rect.height = 120;
                GUILayout.Space(rect.height);
                var bannerRect = new Rect(
                    rect.x + (rect.width - bannerTexture.width * rect.height / bannerTexture.height) / 2,
                    rect.y,
                    bannerTexture.width * rect.height / bannerTexture.height,
                    rect.height
                );
                GUI.DrawTexture(bannerRect, bannerTexture);
                if (versionLabelStyle == null)
                    versionLabelStyle = new GUIStyle(EditorStyles.whiteLargeLabel) {
                        alignment = TextAnchor.UpperRight,
                        padding = new RectOffset(2, 4, 2, 4),
                        fontStyle = FontStyle.Bold,
                        font = font,
                    };
                var tempContent = Utils.GetTempContent($"v{selfUpdater.CurrentVersion}");
                var versionSize = versionLabelStyle.CalcSize(tempContent);
                GUI.Label(new Rect(bannerRect.xMax - versionSize.x, bannerRect.yMin, versionSize.x, versionSize.y), tempContent, versionLabelStyle);
            }
            EditorGUILayout.Space();
            // EditorI18NEditor.DrawLocaleField();
            selfUpdater.DrawUpdateNotifier();
            EditorGUILayout.Space();
        }
    }
}
