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

        #if VPM_RESOLVER_INCLUDED
        public event Action OnVersionRefreshed;
        #endif

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
            CheckInstallationCallback().Forget();
            #endif
        }

        #if VPM_RESOLVER_INCLUDED
        async UniTask CheckInstallationCallback() {
            await UniTask.SwitchToMainThread();
            OnVersionRefreshed?.Invoke();
        }
        #endif

        public void ResolveInstallation() {
            #if VPM_RESOLVER_INCLUDED
            if (!Repos.UserRepoExists(listingsID) && !Repos.AddRepo(new Uri(listingsURL)))
                return;
            #endif
            ConfirmAndUpdate();
        }

        public void ConfirmAndUpdate() {
            #if VPM_RESOLVER_INCLUDED
            if (string.IsNullOrEmpty(packageName)) {
                Debug.LogError("Unable to find package name.");
                return;
            }
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
            #else
            switch (EditorUtility.DisplayDialogComplex(
                "Unable to Update Package",
                "It seems your project is not managed by VRChat Creator Companion (VCC).\n" +
                "You need to migrate your project to Creator Companion to get automatic updates.",
                "Close", "What is Creator Companion?", "How to migrate?"
            )) {
                case 1: Application.OpenURL("https://vcc.docs.vrchat.com/"); break;
                case 2: Application.OpenURL("https://vcc.docs.vrchat.com/vpm/migrating"); break;
            }
            Debug.LogError("Unable to update package. Please migrate your project to Creator Companion first.");
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