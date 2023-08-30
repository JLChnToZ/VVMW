using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine.SceneManagement;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal sealed class VersionDisplayPreprocesser : IProcessSceneWithReport {
        public int callbackOrder => -1;

        public void OnProcessScene(Scene scene, BuildReport report) {
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(VersionDisplay).Assembly);
            if (packageInfo == null) {
                Debug.LogWarning("[VersionDisplay] Cannot find package info.");
                return;
            }
            foreach (var versionDisplay in scene.IterateAllComponents<VersionDisplay>()) {
                if (!versionDisplay.TryGetComponent(out Text text)) {
                    Debug.LogWarning($"[VersionDisplay] {versionDisplay.name} does not have a Text component.", versionDisplay);
                    continue;
                }
                using (var so = new SerializedObject(text)) {
                    var sp = so.FindProperty("m_Text");
                    if (sp == null) {
                        Debug.LogWarning($"[VersionDisplay] {versionDisplay.name} does not have a Text component.", versionDisplay);
                        continue;
                    }
                    sp.stringValue = string.Format(versionDisplay.format, packageInfo.version);
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }
        }
    }
}