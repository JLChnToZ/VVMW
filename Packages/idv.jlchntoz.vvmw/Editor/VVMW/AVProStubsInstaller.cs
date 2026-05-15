using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if VRC_SDK_VRCSDK3
using VRC.SDK3.Editor;
#else
using VRC.SDKBase.Editor;
#endif

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
            if (packageInfo == null) {
                if (!silent) Debug.LogError("[VVMW] VizVid is not properly installed, please reinstall it.");
                return;
            }
            var dirStack = new Stack<(string, string, string)>();
            dirStack.Push((Path.Join(packageInfo.resolvedPath, "Samples~/AVProStubs"), "Assets", "AvProVideo"));
            while (dirStack.TryPop(out var pair)) {
                var (srcDir, dstBase, dstName) = pair;
                var dstDir = Path.Join(dstBase, dstName);
                if (!Directory.Exists(dstDir)) AssetDatabase.CreateFolder(dstBase, dstName);
                foreach (var filePath in Directory.GetFiles(srcDir)) {
                    var fileName = Path.GetFileName(filePath);
                    var destFilePath = Path.Join(dstDir, fileName);
                    if (File.Exists(destFilePath) && !forced) {
                        if (!silent) Debug.LogWarning($"[VVMW] File {destFilePath} already exists, skipping.");
                        continue;
                    }
                    File.Copy(filePath, destFilePath, true);
                }
                foreach (var subDir in Directory.GetDirectories(srcDir))
                    dirStack.Push((subDir, dstDir, Path.GetFileName(subDir)));
            }
            AssetDatabase.Refresh();
        }

        static void AddBuildHook(object sender, EventArgs e) {
#if VRC_SDK_VRCSDK3
            if (VRCSdkControlPanel.TryGetBuilder(out IVRCSdkWorldBuilderApi builder))
#else
            if (VRCSdkControlPanel.TryGetBuilder(out IVRCSdkBuilderApi builder))
#endif
                builder.OnSdkBuildStart += OnBuildStarted;
        }

        static void OnBuildStarted(object sender, object target) => InstallAVProStubs(true, false);
    }
}