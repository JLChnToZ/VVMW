using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEditor;
using UnityEngine.SceneManagement;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal sealed class VersionDisplayPreprocesser : IPreprocessor {
        public int CallbackOrder => -1;

        public void OnPreprocess(Scene scene) {
            var packageInfo = PackageManagerPackageInfo.FindForAssembly(typeof(VersionDisplay).Assembly);
            if (packageInfo == null) {
                Debug.LogWarning("[VersionDisplay] Cannot find package info.");
                return;
            }
            foreach (var versionDisplay in scene.IterateAllComponents<VersionDisplay>()) {
                Component component;
                if (versionDisplay.TryGetComponent(out Text text))
                    component = text;
                else if (versionDisplay.TryGetComponent(out TMP_Text tmpText))
                    component = tmpText;
                else {
                    Debug.LogWarning($"[VersionDisplay] {versionDisplay.name} does not have a Text or TextMeshProUGUI component.", versionDisplay);
                    continue;
                }
                using (var so = new SerializedObject(component)) {
                    var sp = so.FindProperty("m_Text") ?? so.FindProperty("m_text");
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