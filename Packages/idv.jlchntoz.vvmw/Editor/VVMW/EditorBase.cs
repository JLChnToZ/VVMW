using UnityEngine;
using UnityEditor;

#if VPM_RESOLVER_INCLUDED
using System.Text;
using Cysharp.Threading.Tasks;
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Core.Types.Packages;
using VRC.PackageManagement.Resolver;
using SemanticVersioning;

using Uri = System.Uri;
#endif

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureUUID = "e8354bc2ac14e86498c0983daf484661";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        const string listingsID = "idv.jlchntoz.xtlcdn-listing";
        const string listingsURL = "https://xtlcdn.github.io/vpm/index.json";
        static Texture2D bannerTexture;
        static PackageManagerPackageInfo currentPackageInfo;
        static Font font;
        static string versionString;
        #if VPM_RESOLVER_INCLUDED
        static string availableVersionString;
        static bool isInstalledManually;
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
                    UniTask.RunOnThreadPool(CheckInstallation).Forget();
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
            if (isInstalledManually) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    var infoContent = GetInfoContent($"Consider install {currentPackageInfo.displayName} via Creator Companion to get automatic updates.");
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Resolve", GUILayout.ExpandWidth(false)))
                        ResolveInstallation();
                }
            }
            if (!string.IsNullOrEmpty(availableVersionString)) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    var infoContent = GetInfoContent($"New Version Available! (v{availableVersionString})");
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Update", GUILayout.ExpandWidth(false)))
                        ConfirmAndUpdate();
                }
            }
            #endif
            EditorGUILayout.Space();
        }
        
        #if VPM_RESOLVER_INCLUDED
        static GUIContent GetInfoContent(string text) {
            if (infoContent == null) {
                var temp = EditorGUIUtility.IconContent("console.infoicon");
                infoContent = new GUIContent(temp.image);
            }
            infoContent.text = text;
            return infoContent;
        }

        static void CheckInstallation() {
            var allVersions = Resolver.GetAllVersionsOf(currentPackageInfo.name);
            if (allVersions.Count > 0 && new Version(allVersions[0]) > new Version(currentPackageInfo.version))
                availableVersionString = versionString;
            var manifest = VPMProjectManifest.Load(Resolver.ProjectDir);
            isInstalledManually = !manifest.locked.ContainsKey(currentPackageInfo.name) && !manifest.dependencies.ContainsKey(currentPackageInfo.name);
        }

        static void ResolveInstallation() {
            if (!Repos.UserRepoExists(listingsID) && !Repos.AddRepo(new Uri(listingsURL)))
                return;
            ConfirmAndUpdate();
        }

        static void ConfirmAndUpdate() {
            var versionString = availableVersionString;
            if (string.IsNullOrEmpty(versionString)) {
                var allVersions = Resolver.GetAllVersionsOf(currentPackageInfo.name);
                if (allVersions.Count == 0) {
                    Debug.LogError($"Unable to find any version of {currentPackageInfo.name}.");
                    return;
                }
                versionString = allVersions[0];
            }
            var vrcPackage = Repos.GetPackageWithVersionMatch(currentPackageInfo.name, versionString);
            var dependencies = Resolver.GetAffectedPackageList(vrcPackage);
            var sb = new StringBuilder();
            sb.AppendLine("The following packages will be updated:");
            foreach (var dependency in dependencies)
                sb.AppendLine($"- {dependency}");
            if (EditorUtility.DisplayDialog("Update Package", sb.ToString(), "Update", "Cancel"))
                UniTask.RunOnThreadPool(() => new UnityProject(Resolver.ProjectDir).UpdateVPMPackage(vrcPackage)).Forget();
        }
        #endif 
    }
}