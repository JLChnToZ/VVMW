using UnityEngine;
using UnityEditor;

#if VPM_RESOLVER_INCLUDED
using System.Text;
using System.Threading.Tasks;
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Resolver;
using SemanticVersioning;
#endif

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureUUID = "e8354bc2ac14e86498c0983daf484661";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        static Texture2D bannerTexture;
        static PackageManagerPackageInfo currentPackageInfo;
        static Font font;
        static string versionString;
        #if VPM_RESOLVER_INCLUDED
        static string availableVersionString;
        #endif
        static GUIStyle versionLabelStyle;
        static GUIContent tempContent, infoContent;

        protected virtual void OnEnable() {
            if (tempContent == null) tempContent = new GUIContent();
            if (string.IsNullOrEmpty(versionString)) {
                currentPackageInfo = PackageManagerPackageInfo.FindForAssembly(GetType().Assembly);
                if (currentPackageInfo != null) {
                    versionString = $"v{currentPackageInfo.version}";
                    #if VPM_RESOLVER_INCLUDED
                    var currentVersion = new Version(currentPackageInfo.version);
                    foreach (var versionString in Resolver.GetAllVersionsOf(currentPackageInfo.name))
                        if (new Version(versionString) > currentVersion) {
                            availableVersionString = versionString;
                            break;
                        }
                    #endif
                } else
                    versionString = "Unknown Version";
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
                tempContent.text = versionString;
                var versionSize = versionLabelStyle.CalcSize(tempContent);
                GUI.Label(new Rect(bannerRect.xMax - versionSize.x, bannerRect.yMin, versionSize.x, versionSize.y), tempContent, versionLabelStyle);
            }
            #if VPM_RESOLVER_INCLUDED
            if (!string.IsNullOrEmpty(availableVersionString)) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    if (infoContent == null) {
                        var temp = EditorGUIUtility.IconContent("console.infoicon.sml");
                        infoContent = new GUIContent(temp.image);
                    }
                    infoContent.text = $"New Version Available! (v{availableVersionString})";
                    EditorGUILayout.LabelField(infoContent, EditorStyles.boldLabel);
                    if (GUILayout.Button("Update", GUILayout.ExpandWidth(false)))
                        ConfirmAndUpdate();
                }
            }
            #endif
            EditorGUILayout.Space();
        }
        
        #if VPM_RESOLVER_INCLUDED
        void ConfirmAndUpdate() {
            var vrcPackage = Repos.GetPackageWithVersionMatch(currentPackageInfo.name, availableVersionString);
            var dependencies = Resolver.GetAffectedPackageList(vrcPackage);
            var sb = new StringBuilder();
            sb.AppendLine("The following packages will be updated:");
            foreach (var dependency in dependencies)
                sb.AppendLine($"- {dependency}");
            if (EditorUtility.DisplayDialog("Update Package", sb.ToString(), "Update", "Cancel"))
                Task.Run(() => new UnityProject(Resolver.ProjectDir).UpdateVPMPackage(vrcPackage));
        }
        #endif 
    }
}