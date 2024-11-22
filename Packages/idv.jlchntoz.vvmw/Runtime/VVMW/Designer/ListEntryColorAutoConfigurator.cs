using UnityEngine;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    /// <summary>
    /// An auto configurator that can configurate color tint for <see cref="ListEntry"/> components.
    /// </summary>
    [AddComponentMenu("VizVid/Color Configurator/List Entry")]
    public class ListEntryColorAutoConfigurator : AbstractAutoConfigurator {
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.normalColorIndex")]
        [SerializeField] int normalColorIndex = -1;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.selectedColorIndex")]
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