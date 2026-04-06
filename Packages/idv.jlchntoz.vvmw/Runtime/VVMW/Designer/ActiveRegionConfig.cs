using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    [EditorOnly, ExecuteInEditMode]
    [AddComponentMenu("VizVid/Active Region Config")]
    public class ActiveRegionConfig : MonoBehaviour, IVizVidCompoonent {
        static readonly ConditionalWeakTable<Core, List<ActiveRegionConfig>> regionConfigTable =
            new ConditionalWeakTable<Core, List<ActiveRegionConfig>>();
        [SerializeField, Locatable(
            InstaniatePrefabPath = "Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab",
            InstaniatePrefabPosition = LocatableAttribute.InstaniatePrefabHierachyPosition.Before
        ), BindUdonSharpEvent, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        internal Core core;
        [SerializeField, LocalizedLabel] internal Bounds bounds;
        [SerializeField, LocalizedLabel] internal bool staticRegion = true;
        [NonSerialized] Core lastCore;

        Core IVizVidCompoonent.Core => core;

        public static void RefreshAll() {
            regionConfigTable.Clear();
            var allConfigs = FindObjectsOfType<ActiveRegionConfig>(true);
            foreach (var config in allConfigs) {
                if (!regionConfigTable.TryGetValue(config.core, out var list)) {
                    list = new List<ActiveRegionConfig>();
                    regionConfigTable.Add(config.core, list);
                }
                list.Add(config);
            }
        }

        public static IEnumerable<ActiveRegionConfig> GetRegionConfigs(Core core) {
            if (regionConfigTable.TryGetValue(core, out var list)) return list;
            return Array.Empty<ActiveRegionConfig>();
        }

        void Awake() => OnValidate();

        void OnValidate() {
            if (core == lastCore) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += UpdateValue;
#endif
        }

        void UpdateValue() {
            if (core == lastCore) return;
            if (lastCore != null) {
                if (regionConfigTable.TryGetValue(lastCore, out var list)) {
                    list.Remove(this);
                    if (list.Count == 0) regionConfigTable.Remove(lastCore);
                }
            }
            if (core != null) {
                if (!regionConfigTable.TryGetValue(core, out var list)) {
                    list = new List<ActiveRegionConfig>();
                    regionConfigTable.Add(core, list);
                }
                list.Add(this);
            }
            lastCore = core;
        }
    }
}