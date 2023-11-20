using System.Reflection;
using UnityEngine;
using UnityEditor;

#if VPM_RESOLVER_INCLUDED
using System;
using System.Text;
using Cysharp.Threading.Tasks;
using VRC.PackageManagement.Core;
using VRC.PackageManagement.Core.Types;
using VRC.PackageManagement.Core.Types.Packages;
using VRC.PackageManagement.Resolver;

using SemanticVersion = SemanticVersioning.Version;
#endif

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class PackageSelfUpdater {
        static GUIContent infoContent;
        readonly string listingsID, listingsURL;
        readonly string packageName, packageDisplayName, packageVersion;
        string availableVersion;
        bool isInstalledManually;

        public string PackageName => packageDisplayName ?? "Unknown Package";

        public string CurrentVersion => packageVersion;

        public string AvailableVersion => availableVersion;

        public bool IsInstalledManually => isInstalledManually;

        public event Action OnVersionRefreshed;

        public PackageSelfUpdater(Assembly assembly, string listingsID, string listingsURL) :
            this(PackageManagerPackageInfo.FindForAssembly(assembly), listingsID, listingsURL) { }

        public PackageSelfUpdater(PackageManagerPackageInfo packageInfo, string listingsID, string listingsURL) {
            if (packageInfo != null) {
                packageName = packageInfo.name;
                packageDisplayName = packageInfo.displayName;
                packageVersion = packageInfo.version;
            }
            availableVersion = "";
            this.listingsID = listingsID;
            this.listingsURL = listingsURL;
            #if !VPM_RESOLVER_INCLUDED
            isInstalledManually = true;
            #endif
        }

        public void CheckInstallationInBackground() {
            #if VPM_RESOLVER_INCLUDED
            UniTask.RunOnThreadPool(CheckInstallation).Forget();
            #endif
        }

        public void CheckInstallation() {
            #if VPM_RESOLVER_INCLUDED
            if (string.IsNullOrEmpty(packageName)) {
                isInstalledManually = true;
                return;
            }
            var allVersions = Resolver.GetAllVersionsOf(packageName);
            if (allVersions.Count > 0 && new SemanticVersion(allVersions[0]) > new SemanticVersion(packageVersion))
                availableVersion = allVersions[0];
            var manifest = VPMProjectManifest.Load(Resolver.ProjectDir);
            isInstalledManually = !manifest.locked.ContainsKey(packageName) && !manifest.dependencies.ContainsKey(packageName);
            #endif
            CheckInstallationCallback().Forget();
        }

        async UniTask CheckInstallationCallback() {
            await UniTask.SwitchToMainThread();
            OnVersionRefreshed?.Invoke();
        }

        public void ResolveInstallation() {
            #if VPM_RESOLVER_INCLUDED
            if (!Repos.UserRepoExists(listingsID) && !Repos.AddRepo(new Uri(listingsURL)))
                return;
            ConfirmAndUpdate();
            #endif
        }

        public void ConfirmAndUpdate() {
            if (string.IsNullOrEmpty(packageName)) {
                Debug.LogError("Unable to find package name.");
                return;
            }
            #if VPM_RESOLVER_INCLUDED
            var version = availableVersion;
            if (string.IsNullOrEmpty(version)) {
                var allVersions = Resolver.GetAllVersionsOf(packageName);
                if (allVersions.Count == 0) {
                    Debug.LogError($"Unable to find any version of {packageName}.");
                    return;
                }
                version = allVersions[0];
            }
            var vrcPackage = Repos.GetPackageWithVersionMatch(packageName, version);
            var dependencies = Resolver.GetAffectedPackageList(vrcPackage);
            var sb = new StringBuilder();
            sb.AppendLine("The following packages will be updated:");
            foreach (var dependency in dependencies)
                sb.AppendLine($"- {dependency}");
            if (EditorUtility.DisplayDialog("Update Package", sb.ToString(), "Update", "Cancel"))
                UpdateUnchecked(vrcPackage).Forget();
            #endif
        }

        public void DrawUpdateNotifier() {
            if (isInstalledManually) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    var infoContent = GetInfoContent($"Consider install {packageDisplayName} via Creator Companion to get automatic updates.");
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Resolve", GUILayout.ExpandWidth(false)))
                        ResolveInstallation();
                }
            }
            if (!string.IsNullOrEmpty(availableVersion)) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    var infoContent = GetInfoContent($"New Version Available! (v{availableVersion})");
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button("Update", GUILayout.ExpandWidth(false)))
                        ConfirmAndUpdate();
                }
            }
        }

        #if VPM_RESOLVER_INCLUDED
        async UniTask UpdateUnchecked(IVRCPackage package) {
            await UniTask.Delay(500);
            Resolver.ForceRefresh();
            try {
                EditorUtility.DisplayProgressBar("Updating Package", $"Updating {package.Id}", 0);
                new UnityProject(Resolver.ProjectDir).UpdateVPMPackage(package);
            } finally {
                EditorUtility.ClearProgressBar();
            }
            Resolver.ForceRefresh();
        }
        #endif

        static GUIContent GetInfoContent(string text) {
            if (infoContent == null) {
                var temp = EditorGUIUtility.IconContent("console.infoicon");
                infoContent = new GUIContent(temp.image);
            }
            infoContent.text = text;
            return infoContent;
        }
    }
}