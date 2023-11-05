using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [RequireComponent(typeof(Graphic))]
    [AddComponentMenu("VizVid/Color Configurator/UI Graphics")]
    public class GraphicAutoConfigurator : AbstractAutoConfigurator {
        [SerializeField] int colorIndex = 0;

        protected override void ConfigurateCore(ColorConfig colorConfig) {
            if (TryGetComponent(out Graphic graphic) && colorIndex >= 0 && colorIndex < colorConfig.colors.Length) {
                graphic.color = colorConfig.colors[colorIndex];
                #if UNITY_EDITOR
                if (!Application.isPlaying) EditorUtility.SetDirty(graphic);
                #endif
            }
        }
    }
}