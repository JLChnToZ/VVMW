using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEditor.Events;
using UdonSharpEditor;
using JLChnToZ.VRC.VVMW.Editors;

namespace JLChnToZ.VRC.VVMW.Designer {
    internal sealed class ResyncButtonPreprocessor : IPreprocessor {
        public int CallbackOrder => -1;

        public void OnPreprocess(Scene scene) {
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