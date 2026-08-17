using System;
using System.Collections.Generic;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
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
    public partial class ColorConfig : MonoBehaviour, ISelfPreProcess {
        static Dictionary<Core, HashSet<ColorConfig>> coreToColorConfigs = new Dictionary<Core, HashSet<ColorConfig>>();
        static Action<Core, HashSet<ColorConfig>> onColorConfigsRemapped;
        /// <summary>
        /// The color palette for all children components.
        /// </summary>
        public Color[] colors;
        [SerializeField, HideInInspector] AbstractAutoConfigurator[] appliedAutoConfigurators;
        [SerializeField, LocalizedLabel] internal bool autoApplyOnBuild = true;
        [NonSerialized] Core mappedCore;
        [NonSerialized] bool hasAwaken;

        internal static event Action<Core, HashSet<ColorConfig>> OnColorConfigsRemapped {
            add {
                onColorConfigsRemapped += value;
                foreach (var kv in coreToColorConfigs)
                    if (kv.Value.Count > 0)
                        value.Invoke(kv.Key, kv.Value);
            }
            remove => onColorConfigsRemapped -= value;
        }

        int IPrioritizedPreProcessor.Priority => 0;

        static bool IsBuildingOrTesting {
            get {
#if UNITY_EDITOR
                return EditorApplication.isPlayingOrWillChangePlaymode || BuildPipeline.isBuildingPlayer;
#else
                return Application.isPlaying;
#endif
            }
        }

        Core FindCore() {
#if !COMPILER_UDONSHARP
            for (var t = transform; t != null; t = t.parent) {
                if (t.TryGetComponent(out Core core)) return core;
                using (PooledObjectExtensions.Get(out List<MonoBehaviour> mbs)) {
                    t.GetComponents(mbs);
                    foreach (var mb in mbs) {
                        if (mb is IVizVidCompoonent vvComponent)
                            return vvComponent.Core;
                    }
                }
            }
#endif
            return null;
        }

        void Awake() {
            if (hasAwaken) return;
            hasAwaken = true;
            destroyCancellationToken.Register(OnDestroying);
#if UNITY_EDITOR
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
#endif
            mappedCore = FindCore();
            AddColorConfigToCore(mappedCore);
        }

        void OnValidate() {Awake();
        }

        void OnDestroying() {
#if UNITY_EDITOR
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
#endif
            if (mappedCore == null) return;
            RemoveColorConfigFromCore(mappedCore);
            mappedCore = null;
        }

        void OnHierarchyChanged() {
            var newMappedCore = FindCore();
            if (newMappedCore == mappedCore) return;
            RemoveColorConfigFromCore(mappedCore);
            mappedCore = newMappedCore;
            AddColorConfigToCore(mappedCore);
        }

        void RemoveColorConfigFromCore(Core core) {
            if (core == null) return;
            if (!coreToColorConfigs.TryGetValue(core, out var colorConfigs)) return;
            if (colorConfigs.Remove(this) && colorConfigs.Count == 0)
                coreToColorConfigs.Remove(core);
            onColorConfigsRemapped?.Invoke(core, colorConfigs);
        }

        void AddColorConfigToCore(Core core) {
            if (core == null) return;
            if (!coreToColorConfigs.TryGetValue(core, out var colorConfigs)) {
                colorConfigs = new HashSet<ColorConfig>();
                coreToColorConfigs.Add(core, colorConfigs);
            }
            colorConfigs.Add(this);
            onColorConfigsRemapped?.Invoke(core, colorConfigs);
        }

        /// <summary>
        /// Configurate colors for all children components of this object that implement <see cref="AbstractAutoConfigurator"/>.
        /// </summary>
        public void ConfigurateColors() {
            var autoConfigurators = GetComponentsInChildren<AbstractAutoConfigurator>(true);
#if UNITY_EDITOR
            if (!IsBuildingOrTesting) Undo.RecordObject(this, "Color Pre Config");
#endif
            foreach (var autoConfigurator in autoConfigurators)
                autoConfigurator.ConfigurateColor();
            appliedAutoConfigurators = autoConfigurators;
#if UNITY_EDITOR
            if (!IsBuildingOrTesting) EditorUtility.SetDirty(this);
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
            if (!IsBuildingOrTesting) Undo.RecordObject(this, "Color Pre Config");
#endif
            appliedAutoConfigurators = autoConfigurators;
#if UNITY_EDITOR
            if (!IsBuildingOrTesting) EditorUtility.SetDirty(this);
#endif
        }

        void ISelfPreProcess.PreProcess() {
#if UNITY_EDITOR
            if (autoApplyOnBuild) ConfigurateColors();
#endif
        }
    }

    /// <summary>
    /// A property attribute that can make a int field show a dropdown list of color presets in <see cref="ColorConfig"/> in inspector.
    /// </summary>
    public class ColorConfigPresetAttribute : PropertyAttribute {}
}