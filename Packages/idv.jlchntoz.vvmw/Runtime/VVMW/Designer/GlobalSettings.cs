using System;
using System.Collections.Generic;
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
        [SerializeField, LocalizedEnum] PlayerDetectOrigin playerDetectOrigin = PlayerDetectOrigin.Head;
        [SerializeField, HideInInspector] List<ActiveRegionManager> activeRegionManagers = new List<ActiveRegionManager>();
        [NonSerialized] bool initialized;

        public LazySwitch MasterScreenModeSwitch => masterScreenModeSwitch;

        public CoreMatchingStrategy DefaultCoreMatchingStrategy => defaultCoreMatchingStrategy;

        public PlayerDetectOrigin PlayerDetectOrigin => playerDetectOrigin;

        void Awake() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (initialized ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) return;
#endif
            initialized = true;
            if (instance != null) {
                Debug.LogError("Multiple GlobalSettings instances detected. This is not supported.", this);
                DestroyImmediate(gameObject);
                return;
            }
            instance = this;
            ConfigureAllScreenModeLazySwitchConfigurators();
            DetectSteragy();
        }

        void OnValidate() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (initialized ||
                EditorApplication.isPlayingOrWillChangePlaymode ||
                !gameObject.scene.IsValid() ||
                PrefabStageUtility.GetCurrentPrefabStage() != null) return;
            EditorApplication.delayCall += Awake;
#endif
        }

        void ConfigureAllScreenModeLazySwitchConfigurators() {
            foreach (var configurator in FindObjectsOfType<ScreenModeLazySwitchConfigurator>(true))
                if (configurator != null)
                    configurator.Connect();
        }

        void DetectSteragy() {
            int steragyAllCount = 0, steragyBoundsCount = 0, steragyNearestCount = 0;
            int useHeadCount = 0, useCameraCount = 0, usePositionCount = 0, useHeightCount = 0;
            foreach (var existing in activeRegionManagers)
                if (existing != null) return;
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
                switch (activeRegionManager.playerDetectOrigin) {
                    case PlayerDetectOrigin.Head:
                        useHeadCount++;
                        break;
                    case PlayerDetectOrigin.ScreenCamera:
                        useCameraCount++;
                        break;
                    case PlayerDetectOrigin.Position:
                        usePositionCount++;
                        break;
                    case PlayerDetectOrigin.PositionWithPlayerHeight:
                        useHeightCount++;
                        break;
                }
                activeRegionManagers.Add(activeRegionManager);
            }
            defaultCoreMatchingStrategy = steragyAllCount >= steragyBoundsCount && steragyAllCount >= steragyNearestCount ?
                CoreMatchingStrategy.All :
                steragyBoundsCount >= steragyNearestCount ?
                CoreMatchingStrategy.Bounds :
                CoreMatchingStrategy.Nearest;
            playerDetectOrigin = useHeadCount >= useCameraCount && useHeadCount >= usePositionCount && useHeadCount >= useHeightCount ?
                PlayerDetectOrigin.Head :
                useCameraCount >= usePositionCount && useCameraCount >= useHeightCount ?
                PlayerDetectOrigin.ScreenCamera :
                usePositionCount >= useHeightCount ?
                PlayerDetectOrigin.Position :
                PlayerDetectOrigin.PositionWithPlayerHeight;
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            EditorUtility.SetDirty(this);
#endif
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        internal static void UpdateAllSubSwitches(LazySwitch masterSwitch, int originalState, int newState) {
            if (newState == originalState) return;
            foreach (var ls in masterSwitch.gameObject.scene.IterateAllComponents<LazySwitch>(true))
                using (var so = new SerializedObject(ls))
                    if (so.FindProperty("masterSwitch").objectReferenceValue == masterSwitch &&
                        UpdateSubSwitch(so, originalState, newState))
                        so.ApplyModifiedProperties();
        }

        internal static bool UpdateSubSwitch(SerializedObject so, int originalState, int newState) {
            var enableState = so.FindProperty("targetObjectEnableMask");
            var groupOffsets = so.FindProperty("targetObjectGroupOffsets");
            return UpdateSubSwitch(enableState, groupOffsets, originalState) | UpdateSubSwitch(enableState, groupOffsets, newState);
        }

        internal static bool UpdateSubSwitch(SerializedProperty enableStates, SerializedProperty groupOffsets, int index) {
            int startIndex = 0;
            int endIndex = enableStates.arraySize;
            int groupOffsetSize = groupOffsets.arraySize;
            if (index <= 0) {
                if (groupOffsetSize > 0) endIndex = Mathf.Min(endIndex, groupOffsets.GetArrayElementAtIndex(0).intValue);
            } else {
                if (groupOffsetSize < index - 1) return false;
                startIndex = groupOffsets.GetArrayElementAtIndex(index - 1).intValue;
                if (groupOffsetSize > index) endIndex = Mathf.Min(endIndex, groupOffsets.GetArrayElementAtIndex(index).intValue);
            }
            if (endIndex <= startIndex) return false;
            for (int i = startIndex; i < endIndex; i++) {
                var element = enableStates.GetArrayElementAtIndex(i);
                element.intValue = element.intValue == 0 ? -1 : 0;
            }
            return true;
        }
#endif
    }
}
