using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [AddComponentMenu("VizVid/Color Configurator/List Entry")]
    public class ListEntryColorAutoConfigurator : AbstractAutoConfigurator {
        [SerializeField] int normalColorIndex = -1;
        [SerializeField] int selectedColorIndex = -1;
        protected override void ConfigurateCore(ColorConfig colorConfig) {
            if (TryGetComponent(out ListEntry listEntry)) {
                #if UNITY_EDITOR
                using (var so = new SerializedObject(listEntry)) {
                    if (normalColorIndex >= 0 && normalColorIndex < colorConfig.colors.Length)
                        so.FindProperty("normalColor").colorValue = colorConfig.colors[normalColorIndex];
                    if (selectedColorIndex >= 0 && selectedColorIndex < colorConfig.colors.Length)
                        so.FindProperty("selectedColor").colorValue = colorConfig.colors[selectedColorIndex];
                    so.ApplyModifiedProperties();
                }
                #endif
            }
        }
    }
}