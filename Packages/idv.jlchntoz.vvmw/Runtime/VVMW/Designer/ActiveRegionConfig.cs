using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.SceneManagement;
#endif

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    [EditorOnly, ExecuteInEditMode]
    [AddComponentMenu("VizVid/Active Region Config")]
    public class ActiveRegionConfig : MonoBehaviour, IVizVidCompoonent {
        const string activeRegionManagerPrefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/Active Region Manager.prefab";
        static ActiveRegionManager activeRegionManager;
        static readonly ConditionalWeakTable<Core, HashSet<ActiveRegionConfig>> regionConfigTable =
            new ConditionalWeakTable<Core, HashSet<ActiveRegionConfig>>();
        [SerializeField, Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        ), LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        internal Core core;
        [SerializeField, LocalizedLabel] internal Bounds bounds;
        [SerializeField, LocalizedLabel] internal bool staticRegion = true;
        [SerializeField, LocalizedLabel] internal bool checkActive = true;
        [SerializeField, LocalizedLabel] internal bool useWorldSpaceBounds;
        [NonSerialized] Core lastCore;

        Core IVizVidCompoonent.Core => core;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        [InitializeOnLoadMethod]
        [DidReloadScripts]
#endif
        public static void RefreshAll() {
            regionConfigTable.Clear();
            foreach (var config in FindObjectsOfType<ActiveRegionConfig>(true))
                config.Register();
        }

        static bool IsObjectDestroyed(UnityObject obj) => obj == null;

        public static IReadOnlyCollection<ActiveRegionConfig> GetRegionConfigs(Core core, bool checkDestroyed = true) {
            if (regionConfigTable.TryGetValue(core, out var set)) {
                if (checkDestroyed) set.RemoveWhere(IsObjectDestroyed);
                return set;
            }
            return Array.Empty<ActiveRegionConfig>();
        }

        void Awake() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            destroyCancellationToken.Register(Unregister);
            OnValidate();
#endif
        }

        void OnValidate() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (core == lastCore) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
            EditorApplication.delayCall += UpdateValue;
#endif
        }

        void UpdateValue() {
            if (core == lastCore) return;
            EnsureActiveRegionManagerExists();
            if (lastCore != null) {
                if (regionConfigTable.TryGetValue(lastCore, out var set)) {
                    set.Remove(this);
                    if (set.Count == 0) regionConfigTable.Remove(lastCore);
                }
            }
            Register();
            lastCore = core;
        }

        void Register() {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            if (core == null || !this.IsAvailableOnRuntime()) return;
            if (!regionConfigTable.TryGetValue(core, out var set)) {
                set = new HashSet<ActiveRegionConfig>();
                regionConfigTable.Add(core, set);
            }
            set.Add(this);
#endif
        }

        void Unregister() {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            if (core == null) return;
            if (regionConfigTable.TryGetValue(core, out var set)) {
                set.Remove(this);
                if (set.Count == 0) regionConfigTable.Remove(core);
            }
#endif
        }

        void EnsureActiveRegionManagerExists() {
#if !COMPILER_UDONSHARP && UNITY_EDITOR
            if (activeRegionManager != null) return;
            activeRegionManager = FindObjectOfType<ActiveRegionManager>();
            if (activeRegionManager != null) return;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(activeRegionManagerPrefabPath);
            if (prefab == null) {
                Debug.LogError($"Failed to load Active Region Manager prefab at path: {activeRegionManagerPrefabPath}");
                return;
            }
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) {
                Debug.LogError("Failed to instantiate Active Region Manager prefab.");
                return;
            }
            
            if (!instance.TryGetComponent(out activeRegionManager)) {
                Undo.DestroyObjectImmediate(instance);
                Debug.LogError("The instantiated Active Region Manager prefab does not contain an ActiveRegionManager component.");
                return;
            }
#endif
        }
    }
}
