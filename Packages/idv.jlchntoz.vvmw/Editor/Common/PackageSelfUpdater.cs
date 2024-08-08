using System.Reflection;
using UnityEngine;
using UnityEditor;
using JLChnToZ.VRC.VVMW.I18N;

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
        static EditorI18N i18n;
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
            if (i18n == null) i18n = EditorI18N.Instance;
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
            foreach (var dependency in dependencies)
                sb.AppendLine($"- {dependency}");
            if (i18n.DisplayLocalizedDialog2("PackageSelfUpdater.update_confirm", sb))
                UpdateUnchecked(vrcPackage).Forget();
            #else
            switch (i18n.DisplayLocalizedDialog3("PackageSelfUpdater.update_message_no_vcc")) {
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
                    var infoContent = GetInfoContent("PackageSelfUpdater.update_message", packageDisplayName);
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button(i18n.GetLocalizedContent("PackageSelfUpdater.update_message:confirm"), GUILayout.ExpandWidth(false)))
                        ResolveInstallation();
                }
            }
            if (!string.IsNullOrEmpty(availableVersion)) {
                EditorGUILayout.Space();
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox)) {
                    var infoContent = GetInfoContent("PackageSelfUpdater.update_available", availableVersion);
                    EditorGUILayout.LabelField(infoContent, EditorStyles.wordWrappedLabel);
                    if (GUILayout.Button(i18n.GetLocalizedContent("PackageSelfUpdater.update_available:confirm"), GUILayout.ExpandWidth(false)))
                        ConfirmAndUpdate();
                }
            }
        }

        #if VPM_RESOLVER_INCLUDED
        async UniTask UpdateUnchecked(IVRCPackage package) {
            await UniTask.Delay(500);
            Resolver.ForceRefresh();
            try {
                EditorUtility.DisplayProgressBar(
                    i18n.GetOrDefault("PackageSelfUpdater.update_progress:title"),
                    string.Format(i18n.GetOrDefault("PackageSelfUpdater.update_progress:content"), package.Id),
                    0
                );
                new UnityProject(Resolver.ProjectDir).UpdateVPMPackage(package);
            } finally {
                EditorUtility.ClearProgressBar();
            }
            Resolver.ForceRefresh();
        }
        #endif

        static GUIContent GetInfoContent(string text, params object[] args) {
            if (infoContent == null) infoContent = EditorGUIUtility.IconContent("console.infoicon");
            var content = i18n.GetLocalizedContent(text, args);
            content.image = infoContent.image;
            return content;
        }
    }
}