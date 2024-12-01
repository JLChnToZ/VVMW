using UnityEngine;
using System.Collections.Generic;
using JLChnToZ.VRC.Foundation;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    /// <summary>
    /// An editor component that can configurate colors for all children components that implement <see cref="AbstractAutoConfigurator"/>.
    /// </summary>
    [EditorOnly]
    [ExecuteInEditMode]
    [AddComponentMenu("VizVid/Color Configurator/Color Config")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#how-to-change-color")]
    public class ColorConfig : MonoBehaviour {
        /// <summary>
        /// The color palette for all children components.
        /// </summary>
        public Color[] colors;
        [SerializeField, HideInInspector] AbstractAutoConfigurator[] appliedAutoConfigurators;

        /// <summary>
        /// Configurate colors for all children components of this object that implement <see cref="AbstractAutoConfigurator"/>.
        /// </summary>
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

        /// <summary>
        /// Check and configurate colors for all components on scene that implement <see cref="AbstractAutoConfigurator"/>.
        /// </summary>
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