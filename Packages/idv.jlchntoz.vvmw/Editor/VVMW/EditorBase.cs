using UnityEngine;
using UnityEditor;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureUUID = "e8354bc2ac14e86498c0983daf484661";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        static Texture2D bannerTexture;
        static Font font;
        static string versionString;
        static GUIStyle versionLabelStyle;

        protected virtual void OnEnable() {
            if (string.IsNullOrEmpty(versionString)) {
                var packageInfo = PackageManagerPackageInfo.FindForAssembly(GetType().Assembly);
                versionString = packageInfo != null ? $"v{packageInfo.version}" : "Unknown Version";
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
                var versionSize = versionLabelStyle.CalcSize(new GUIContent(versionString));
                GUI.Label(new Rect(bannerRect.xMax - versionSize.x, bannerRect.yMin, versionSize.x, versionSize.y), versionString, versionLabelStyle);
            }
            EditorGUILayout.Space();
        }
    }
}