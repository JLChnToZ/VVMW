using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [RequireComponent(typeof(Selectable))]
    [AddComponentMenu("VizVid/Color Configurator/Color Tint")]
    public class ColorTintAutoConfigurator : AbstractAutoConfigurator {
        [SerializeField] int normalColorIndex = -1;
        [SerializeField] int highlightedColorIndex = -1;
        [SerializeField] int pressedColorIndex = -1;
        [SerializeField] int selectedColorIndex = -1;
        [SerializeField] int disabledColorIndex = -1;

        protected override void ConfigurateCore(ColorConfig colorConfig) {
            if (TryGetComponent(out Selectable selectable)) {
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