using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Events;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Designer {
    internal sealed class ResyncButtonPreprocessor : IProcessSceneWithReport {
        public int callbackOrder => -1;

        public void OnProcessScene(Scene scene, BuildReport report) {
            foreach (var entry in scene.IterateAllComponents<ResyncButtonConfigurator>()) {
                if (!entry.TryGetComponent(out Button button)) {
                    Debug.LogWarning($"[ResyncButton] No button component in {entry.name}. This should not happen.", entry);
                    continue;
                }
                if (entry.core == null) {
                    Debug.LogWarning($"[ResyncButton] Core component in {entry.name} is not assigned.", entry);
                    continue;
                }
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(entry.core);
                if (udon == null) {
                    Debug.LogWarning($"[ResyncButton] Misconfigurated Core component in {entry.name}.", entry);
                    continue;
                }
                UnityEventTools.AddStringPersistentListener(
                    button.onClick,
                    udon.SendCustomEvent,
                    entry.globalSync ? nameof(Core.GlobalSync) : nameof(Core.LocalSync)
                );
            }
        }
    }
}