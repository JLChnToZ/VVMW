using System;
using UnityEngine;
using UdonSharp;
using VRC.SDKBase;
using VRC.SDK3.Data;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using System.Collections.Generic;
using JLChnToZ.VRC.VVMW.Designer;
#endif

namespace JLChnToZ.VRC.VVMW {
    /// <summary>
    /// Manages the active state of cores based on players' positions.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("VizVid/Components/Active Region Manager")]
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public partial class ActiveRegionManager : UdonSharpEventSender {
        [SerializeField, LocalizedLabel, LocalizedEnum] internal CoreMatchingStrategy coreControlStrategy = CoreMatchingStrategy.All;
        [SerializeField, HideInInspector, BindUdonSharpEvent] Core[] cores;
        [SerializeField, HideInInspector] Bounds[] coreBounds;
        [SerializeField, HideInInspector] Transform[] coreBoundsReferenceTransforms;
        [SerializeField, HideInInspector] Matrix4x4[] coreBoundsReferenceMatrices;
        [SerializeField, HideInInspector] int[] coreBoundsMatchOffset;
        [SerializeField, HideInInspector] int coreCount, boundsCount, alwaysActiveCoreOffset;
        [NonSerialized] public Core core;
        [NonSerialized] public Core[] matchingCores;
        [NonSerialized] public int matchingCoreCount;
        [NonSerialized] public bool isMatchingAll;
        DataDictionary lastCores = new DataDictionary();
        bool afterFirstRun, matchingRunning;
        VRCPlayerApi localPlayer;
        bool matchingCoreChanged;

        void OnEnable() {
            localPlayer = Networking.LocalPlayer;
            if (!Utilities.IsValid(localPlayer)) {
                enabled = false;
                return;
            }
            isMatchingAll = coreControlStrategy == CoreMatchingStrategy.All;
            if (!matchingRunning && !isMatchingAll) {
                matchingRunning = true;
                SendCustomEventDelayedFrames(nameof(_UpdateMatching), 0);
            }
            if (afterFirstRun) return;
            afterFirstRun = true;
            if (!Utilities.IsValid(matchingCores) || matchingCores.Length < cores.Length) {
                if (isMatchingAll) {
                    matchingCores = cores;
                    matchingCoreCount = coreCount;
                } else {
                    matchingCores = new Core[coreCount];
                    if (alwaysActiveCoreOffset > 0) {
                        Array.Copy(cores, matchingCores, alwaysActiveCoreOffset);
                        for (int i = 0; i < alwaysActiveCoreOffset; i++) {
                            var core = cores[i];
                            if (!Utilities.IsValid(core)) continue;
                            lastCores[core] = true;
                        }
                    }
                }
            }
            for (int i = 0; i < coreCount; i++) {
                var core = cores[i];
                if (!Utilities.IsValid(core)) continue;
                core.SetActive(Array.IndexOf(matchingCores, core, 0, matchingCoreCount) >= 0);
            }
            if (isMatchingAll) {
                core = null;
                matchingCores = cores;
                matchingCoreCount = coreCount;
                SendEvent("_OnMatchingCoresChanged");
            }
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _UpdateMatching() {
            if (!isActiveAndEnabled) {
                matchingRunning = false;
                return;
            }
            SendCustomEventDelayedSeconds(nameof(_UpdateMatching), 0.1F);
            var headPos = localPlayer.GetTrackingData(VRCPlayerApi.TrackingDataType.Head).position;
            var closestCore = alwaysActiveCoreOffset > 0 ? cores[0] : null;
            float closestDist = float.PositiveInfinity;
            matchingCoreCount = alwaysActiveCoreOffset;
            for (int i = 0, j = matchingCoreCount; i < boundsCount; i++) {
                int matchIndex;
                do {
                    matchIndex = coreBoundsMatchOffset[j];
                } while (i >= matchIndex && ++j < coreCount);
                var bounds = coreBounds[i];
                var refTransform = coreBoundsReferenceTransforms[i];
                var localPos = Utilities.IsValid(refTransform) ?
                    refTransform.InverseTransformPoint(headPos) :
                    coreBoundsReferenceMatrices[i].MultiplyPoint3x4(headPos);
                if (bounds.Contains(localPos)) {
                    closestDist = -1;
                    closestCore = cores[j - 1];
                    AddCoreToMatching(closestCore);
                    continue;
                }
                if (closestDist <= 0) continue;
                float dist = bounds.SqrDistance(localPos);
                if (dist < closestDist) {
                    closestDist = dist;
                    closestCore = cores[j - 1];
                }
            }
            if (Utilities.IsValid(closestCore)) {
                if (matchingCoreCount == 0 && coreControlStrategy == CoreMatchingStrategy.Nearest)
                    AddCoreToMatching(closestCore);
                if (core != closestCore) {
                    core = closestCore;
                    SendEvent("_OnActiveCoreChanged");
                }
            }
            var keys = lastCores.GetKeys();
            for (int i = 0, c = keys.Count; i < c; i++) {
                var core = (Core)keys[i].Reference;
                if (Array.IndexOf(matchingCores, core, 0, matchingCoreCount) >= 0) continue;
                core.SetActive(false);
                lastCores.Remove(core);
                matchingCoreChanged = true;
            }
            if (matchingCoreChanged) SendEvent("_OnMatchingCoresChanged");
            matchingCoreChanged = false;
        }

        void AddCoreToMatching(Core core) {
            matchingCores[matchingCoreCount++] = core;
            if (lastCores.ContainsKey(core)) return;
            lastCores[core] = true;
            core.SetActive(true);
            matchingCoreChanged = true;
        }
    }

#if !COMPILER_UDONSHARP && UNITY_EDITOR
    public partial class ActiveRegionManager : ISingleton<ActiveRegionManager> {
        void ISingleton<ActiveRegionManager>.Merge(ActiveRegionManager[] others) {
            ActiveRegionConfig.RefreshAll();
            alwaysActiveCoreOffset = 0;
            using (PooledObjectExtensions.Get(out List<Bounds> bounds))
            using (PooledObjectExtensions.Get(out List<Core> coreList))
            using (PooledObjectExtensions.Get(out List<int> coreBoundsOffsetList))
            using (PooledObjectExtensions.Get(out List<Transform> boundsRefTransforms))
            using (PooledObjectExtensions.Get(out List<Matrix4x4> boundsRefMatrices)) {
                coreBoundsOffsetList.Add(0);
                foreach (var core in gameObject.scene.IterateAllComponents<Core>()) {
                    int count = 0;
                    foreach (var boundData in ActiveRegionConfig.GetRegionConfigs(core)) {
                        bounds.Add(boundData.bounds);
                        var boundTransform = boundData.transform;
                        boundsRefTransforms.Add(boundData.staticRegion ? null : boundTransform);
                        boundsRefMatrices.Add(boundData.useWorldSpaceBounds ? boundTransform.worldToLocalMatrix : Matrix4x4.identity);
                        count++;
                    }
                    if (count == 0) {
                        coreList.Insert(0, core);
                        coreBoundsOffsetList.Insert(0, 0);
                        alwaysActiveCoreOffset++;
                    } else {
                        coreList.Add(core);
                        coreBoundsOffsetList.Add(coreBoundsOffsetList[^1] + count);
                    }
                }
                coreCount = coreList.Count;
                cores = coreList.ToArray();
                coreBoundsMatchOffset = coreBoundsOffsetList.ToArray();
                coreBounds = bounds.ToArray();
                coreBoundsReferenceTransforms = boundsRefTransforms.ToArray();
                coreBoundsReferenceMatrices = boundsRefMatrices.ToArray();
                boundsCount = coreBounds.Length;
                if (alwaysActiveCoreOffset >= coreCount) coreControlStrategy = CoreMatchingStrategy.All;
                else {
                    var globalSettings = FindObjectOfType<GlobalSettings>(true);
                    if (globalSettings != null) coreControlStrategy = globalSettings.DefaultCoreMatchingStrategy;
                }
            }
        }
    }
#endif

    public enum CoreMatchingStrategy {
        All,
        Bounds,
        Nearest,
    }
}