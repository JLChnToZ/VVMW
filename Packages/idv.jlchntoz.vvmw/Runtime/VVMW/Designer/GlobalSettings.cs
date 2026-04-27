using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly, ExecuteInEditMode, DisallowMultipleComponent]
    [AddComponentMenu("/VizVid/Components/Global Settings")]
    public class GlobalSettings : MonoBehaviour {
        static GlobalSettings instance;

        public static GlobalSettings Instance => instance;

        [SerializeField] LazySwitch masterScreenModeSwitch;
        [SerializeField, LocalizedEnum] CoreMatchingStrategy defaultCoreMatchingStrategy = CoreMatchingStrategy.All;

        public LazySwitch MasterScreenModeSwitch => masterScreenModeSwitch;

        public CoreMatchingStrategy DefaultCoreMatchingStrategy => defaultCoreMatchingStrategy;

        void Awake() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif
            if (instance != null) {
                Debug.LogError("Multiple GlobalSettings instances detected. This is not supported.", this);
                DestroyImmediate(gameObject);
                return;
            }
            instance = this;
            ConfigureAllScreenModeLazySwitchConfigurators();
            DetectSteragy();
        }

        void ConfigureAllScreenModeLazySwitchConfigurators() {
            foreach (var configurator in FindObjectsOfType<ScreenModeLazySwitchConfigurator>(true))
                if (configurator != null)
                    configurator.Connect();
        }

        void DetectSteragy() {
            int steragyAllCount = 0, steragyBoundsCount = 0, steragyNearestCount = 0;
            foreach (var activeRegionManager in FindObjectsOfType<ActiveRegionManager>(true)) {
                if (activeRegionManager == null) continue;
                switch (activeRegionManager.coreControlStrategy) {
                    case CoreMatchingStrategy.All:
                        steragyAllCount++;
                        break;
                    case CoreMatchingStrategy.Bounds:
                        steragyBoundsCount++;
                        break;
                    case CoreMatchingStrategy.Nearest:
                        steragyNearestCount++;
                        break;
                }
            }
            var newSteragy = steragyAllCount >= steragyBoundsCount && steragyAllCount >= steragyNearestCount ?
                CoreMatchingStrategy.All :
                steragyBoundsCount >= steragyNearestCount ?
                CoreMatchingStrategy.Bounds :
                CoreMatchingStrategy.Nearest;
            if (newSteragy != defaultCoreMatchingStrategy) {
                defaultCoreMatchingStrategy = newSteragy;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
                EditorUtility.SetDirty(this);
#endif
            }
        }
    }
}
