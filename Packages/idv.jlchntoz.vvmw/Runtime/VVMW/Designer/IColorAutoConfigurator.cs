using UnityEngine;

namespace JLChnToZ.VRC.VVMW.Designer {
    public interface IColorAutoConfigurator {
        void ConfigurateColor();
    }

    [EditorOnly, ExecuteInEditMode]
    public abstract class AbstractAutoConfigurator : MonoBehaviour, IColorAutoConfigurator {
        ColorConfig colorConfig;
        protected virtual void Awake() => FindColorConfig();
        protected virtual void OnTransformParentChanged() => FindColorConfig();

        public virtual void ConfigurateColor() {
            FindColorConfig();
            if (colorConfig != null) ConfigurateCore(colorConfig);
        }

        protected abstract void ConfigurateCore(ColorConfig colorConfig);

        void FindColorConfig() {
            if (colorConfig == null || !transform.IsChildOf(colorConfig.transform))
                colorConfig = GetComponentInParent<ColorConfig>();
        }
    }

}
