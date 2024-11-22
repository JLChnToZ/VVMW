using UnityEngine;
using UnityEngine.UI;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    /// <summary>
    /// An auto configurator that can configurate color tint for <see cref="Graphic"/> components.
    /// </summary>
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("VizVid/Color Configurator/UI Graphics")]
    public class GraphicAutoConfigurator : AbstractAutoConfigurator {
        [SerializeField, LocalizedLabel] int colorIndex = 0;

        protected override void ConfigurateCore(ColorConfig colorConfig) {
            if (TryGetComponent(out Graphic graphic) && colorIndex >= 0 && colorIndex < colorConfig.colors.Length) {
                #if UNITY_EDITOR
                if (!Application.isPlaying) Undo.RecordObject(graphic, "Graphic Auto Configurator");
                #endif
                graphic.color = colorConfig.colors[colorIndex];
                #if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(graphic);
                #endif
            }
        }
    }
}