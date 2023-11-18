using UnityEngine;
using UnityEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureUUID = "e8354bc2ac14e86498c0983daf484661";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        const string listingsID = "idv.jlchntoz.xtlcdn-listing";
        const string listingsURL = "https://xtlcdn.github.io/vpm/index.json";
        static Texture2D bannerTexture;
        static PackageSelfUpdater selfUpdater;
        static Font font;
        static GUIStyle versionLabelStyle;
        static GUIContent tempContent;

        protected virtual void OnEnable() {
            if (tempContent == null) tempContent = new GUIContent();
            if (selfUpdater == null) {
                selfUpdater = new PackageSelfUpdater(GetType().Assembly, listingsID, listingsURL);
                selfUpdater.CheckInstallationInBackground();
            }
            if (bannerTexture == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(bannerTextureUUID);
                if (!string.IsNullOrEmpty(assetPath)) bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            if (font == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(fontGUID);
                if (!string.IsNullOrEmpty(assetPath)) font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
            }
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
                tempContent.text = $"v{selfUpdater.CurrentVersion}";
                var versionSize = versionLabelStyle.CalcSize(tempContent);
                GUI.Label(new Rect(bannerRect.xMax - versionSize.x, bannerRect.yMin, versionSize.x, versionSize.y), tempContent, versionLabelStyle);
            }
            selfUpdater.DrawUpdateNotifier();
            EditorGUILayout.Space();
        }
    }
}