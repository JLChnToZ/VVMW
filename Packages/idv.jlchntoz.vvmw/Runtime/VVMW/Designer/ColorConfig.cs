using UnityEngine;
using System.Collections.Generic;
using JLChnToZ.VRC.Foundation;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly]
    [ExecuteInEditMode]
    [AddComponentMenu("VizVid/Color Configurator/Color Config")]
    [HelpURL("https://github.com/JLChnToZ/VVMW/blob/main/Packages/idv.jlchntoz.vvmw/README.md#how-to-change-color")]
    public class ColorConfig : MonoBehaviour {
        public Color[] colors;
        [SerializeField, HideInInspector] AbstractAutoConfigurator[] appliedAutoConfigurators;

        public void ConfigurateColors() {
            var autoConfigurators = GetComponentsInChildren<AbstractAutoConfigurator>(true);
            #if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(this, "Color Pre Config");
            #endif
            foreach (var autoConfigurator in autoConfigurators)
                autoConfigurator.ConfigurateColor();
            appliedAutoConfigurators = autoConfigurators;
            #if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
            #endif
        }

        public void CheckAndConfigurateColors() {
            var applied = new HashSet<AbstractAutoConfigurator>();
            if (appliedAutoConfigurators != null)
                foreach (var autoConfigurator in appliedAutoConfigurators)
                    if (autoConfigurator != null) applied.Add(autoConfigurator);
            var autoConfigurators = GetComponentsInChildren<AbstractAutoConfigurator>(true);
            bool hasDirty = false;
            foreach (var autoConfigurator in autoConfigurators)
                if (applied.Add(autoConfigurator)) {
                    autoConfigurator.ConfigurateColor();
                    hasDirty = true;
                }
            if (!hasDirty) return;
            #if UNITY_EDITOR
            if (!Application.isPlaying) Undo.RecordObject(this, "Color Pre Config");
            #endif
            appliedAutoConfigurators = autoConfigurators;
            #if UNITY_EDITOR
            if (!Application.isPlaying) EditorUtility.SetDirty(this);
            #endif
        }
    }
}