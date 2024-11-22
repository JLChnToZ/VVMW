using UnityEngine;
using UnityEngine.UI;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    /// <summary>
    /// An auto configurator that can configurate color tint for <see cref="Selectable"/> components.
    /// </summary>
    [RequireComponent(typeof(Selectable))]
    [AddComponentMenu("VizVid/Color Configurator/Color Tint")]
    public class ColorTintAutoConfigurator : AbstractAutoConfigurator {
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.normalColorIndex")]
        [SerializeField] int normalColorIndex = -1;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.highlightedColorIndex")]
        [SerializeField] int highlightedColorIndex = -1;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.pressedColorIndex")]
        [SerializeField] int pressedColorIndex = -1;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.selectedColorIndex")]
        [SerializeField] int selectedColorIndex = -1;
        [LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Designer.GraphicAutoConfigurator.disabledColorIndex")]
        [SerializeField] int disabledColorIndex = -1;

        protected override void ConfigurateCore(ColorConfig colorConfig) {
            if (TryGetComponent(out Selectable selectable)) {
                #if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RecordObject(selectable, "Color Tint Auto Configurator");
                #endif
                var colorBlock = selectable.colors;
                if (normalColorIndex >= 0 && normalColorIndex < colorConfig.colors.Length)
                    colorBlock.normalColor = colorConfig.colors[normalColorIndex];
                if (highlightedColorIndex >= 0 && highlightedColorIndex < colorConfig.colors.Length)
                    colorBlock.highlightedColor = colorConfig.colors[highlightedColorIndex];
                if (pressedColorIndex >= 0 && pressedColorIndex < colorConfig.colors.Length)
                    colorBlock.pressedColor = colorConfig.colors[pressedColorIndex];
                if (selectedColorIndex >= 0 && selectedColorIndex < colorConfig.colors.Length)
                    colorBlock.selectedColor = colorConfig.colors[selectedColorIndex];
                if (disabledColorIndex >= 0 && disabledColorIndex < colorConfig.colors.Length)
                    colorBlock.disabledColor = colorConfig.colors[disabledColorIndex];
                selectable.colors = colorBlock;
                #if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(selectable);
                #endif
            }
        }
    }
}