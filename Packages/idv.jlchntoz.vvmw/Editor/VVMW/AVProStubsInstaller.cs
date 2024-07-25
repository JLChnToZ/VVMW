using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using VRC.SDK3.Editor;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW {
    [InitializeOnLoad]
    public static class AVProStubsInstaller {
        static AVProStubsInstaller() {
            VRCSdkControlPanel.OnSdkPanelEnable += AddBuildHook;
        }

        [MenuItem("Tools/VizVid/Install AVPro Stubs")]
        static void InstallAVProStubsMenu() => InstallAVProStubs(false, true);

        static void InstallAVProStubs(bool silent, bool forced) {
            if (Type.GetType("RenderHeads.Media.AVProVideo.MediaPlayer, AVProVideo.Runtime", false) != null) {
                if (!silent) Debug.Log("[VVMW] Required type signature already exists, skipping installation.");
                return;
            }
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(AVProStubsInstaller).Assembly);
            if (packageInfo != null) {
                var targetPath = Path.GetFullPath(Path.Join(packageInfo.resolvedPath, "Samples~/AVProStubs"));
                foreach (var sample in Sample.FindByPackage(packageInfo.name, packageInfo.version))
                    if (string.Equals(Path.GetFullPath(sample.resolvedPath), targetPath, StringComparison.OrdinalIgnoreCase)) {
                        sample.Import(forced ? Sample.ImportOptions.OverridePreviousImports : Sample.ImportOptions.None);
                        return;
                    }
            }
            if (!silent) Debug.LogError("[VVMW] VizVid is not properly installed, please reinstall it.");
        }

        static void AddBuildHook(object sender, EventArgs e) {
            if (VRCSdkControlPanel.TryGetBuilder(out IVRCSdkWorldBuilderApi builder))
                builder.OnSdkBuildStart += OnBuildStarted;
        }

        static void OnBuildStarted(object sender, object target) => InstallAVProStubs(true, false);
    }
}