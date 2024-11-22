using UnityEngine;
using JLChnToZ.VRC.Foundation;
#if UNITY_EDITOR
using UnityEngine.UI;
using TMPro;
using UnityEditor;

using PackageManagerPackageInfo = UnityEditor.PackageManager.PackageInfo;
#endif

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// A component that display the version of the package.
    /// </summary>
    [EditorOnly]
    [AddComponentMenu("VizVid/Common/Version Display")]
    [TMProMigratable]
    public partial class VersionDisplay : MonoBehaviour {
        /// <summary>
        /// The format of the version display.
        /// </summary>
        public string format = "V{0}";
    }

    #if UNITY_EDITOR
    public partial class VersionDisplay : ISelfPreProcess {
        static PackageManagerPackageInfo packageInfo;

        int IPrioritizedPreProcessor.Priority => -10;

        void ISelfPreProcess.PreProcess() {
            if (packageInfo == null) {
                packageInfo = PackageManagerPackageInfo.FindForAssembly(GetType().Assembly);
                if (packageInfo == null) {
                    Debug.LogWarning("[VersionDisplay] Cannot find package info.");
                    return;
                }
            }
            Component component;
            if (TryGetComponent(out Text text))
                component = text;
            else if (TryGetComponent(out TMP_Text tmpText))
                component = tmpText;
            else {
                Debug.LogWarning($"[VersionDisplay] {name} does not have a Text or TextMeshProUGUI component.", this);
                return;
            }
            using (var so = new SerializedObject(component)) {
                var sp = so.FindProperty("m_Text") ?? so.FindProperty("m_text");
                if (sp == null) {
                    Debug.LogWarning($"[VersionDisplay] {name} does not have a Text component.", this);
                    return;
                }
                sp.stringValue = string.Format(format, packageInfo.version);
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
    #endif
}