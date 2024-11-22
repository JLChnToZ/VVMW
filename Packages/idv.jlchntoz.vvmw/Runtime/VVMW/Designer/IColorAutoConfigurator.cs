using UnityEngine;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW.Designer {
    /// <summary>
    /// An abstract auto configurator that can configurate color tint for components.
    /// </summary>
    [EditorOnly, ExecuteInEditMode]
    public abstract class AbstractAutoConfigurator : MonoBehaviour {
        ColorConfig colorConfig;
        protected virtual void Awake() => FindColorConfig();
        protected virtual void OnTransformParentChanged() => FindColorConfig();

        /// <summary>
        /// Configurate color tint for this component.
        /// </summary>
        public virtual void ConfigurateColor() {
            FindColorConfig();
            if (colorConfig != null) ConfigurateCore(colorConfig);
        }

        /// <summary>
        /// Configurate color tint for this component.
        /// </summary>
        /// <param name="colorConfig">The color config that this component should use.</param>
        /// <remarks>
        /// Override this method to configurate color tint for this component.
        /// </remarks>
        protected abstract void ConfigurateCore(ColorConfig colorConfig);

        void FindColorConfig() {
            if (colorConfig == null || !transform.IsChildOf(colorConfig.transform))
                colorConfig = GetComponentInParent<ColorConfig>();
        }
    }

}
