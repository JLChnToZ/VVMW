using UnityEngine;

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [ExecuteInEditMode]
    [AddComponentMenu("VizVid/Color Configurator/Color Config")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-change-color")]
    public class ColorConfig : MonoBehaviour {
        public Color[] colors;

        public void ConfigurateColors() {
            var autoConfigurators = GetComponentsInChildren<AbstractAutoConfigurator>(true);
            foreach (var autoConfigurator in autoConfigurators)
                autoConfigurator.ConfigurateColor();
        }
    }
}