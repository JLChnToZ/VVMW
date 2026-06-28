using System;
using System.Collections.Generic;
using UnityEngine;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using VRC.SDKBase;
#endif
#if VRC_LIGHT_VOLUMES_V2
using VRCLightVolumes;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#screen-configurator")]
    [AddComponentMenu("VizVid/Components/Screen Configurator")]
    public class ScreenConfigurator : MonoBehaviour, IVizVidCompoonent {
        static readonly List<MonoBehaviour> monoBehaviours = new List<MonoBehaviour>();
        static readonly Dictionary<(Renderer, int), ScreenConfigurator> instances = new Dictionary<(Renderer, int), ScreenConfigurator>();
        [SerializeField, Locatable, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] internal Core core;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.videoScreenTarget")] internal Renderer screenRenderer;
        [SerializeField] int targetMode = 1;
        [SerializeField] int targetIndex = -1;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetPropertyNames")] string targetPropertyName = "_MainTex";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.avProPropertyNames")] string avProPropertyName = "_IsAVProVideo";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetDefaultTextures")] Texture defaultTexture;
#if VRC_LIGHT_VOLUMES_V3
        [SerializeField, LocalizedLabel] internal PointLightVolume pointLightVolume;
#endif
        Renderer previousRenderer;
        Core previousCore;
        int lastIndex;
        [SerializeField, HideInInspector] internal int coreIndex = -1;
        [NonSerialized] bool firstRun, isPendingApply;

        Core IVizVidCompoonent.Core => core;

        public Renderer Renderer {
            get {
                var result = screenRenderer;
                if (result) return result;
                result = GetComponentInChildren<Renderer>();
                if (result) return result;
                result = GetComponentInChildren<Renderer>(true);
                return result;
            }
        }

        internal bool IsPrefabEditingMode {
            get {
#if UNITY_EDITOR
                return PrefabStageUtility.GetPrefabStage(gameObject) != null || !gameObject.scene.IsValid();
#else
                return false;
#endif
            }
        }

        public static ScreenConfigurator GetInstance(Renderer renderer, int index = -1) {
            if (renderer && instances.TryGetValue((renderer, index), out var instance))
                return instance;
            return null;
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
#if VRC_LIGHT_VOLUMES_V2
        static LightVolumeAdaptor TryGetAttachedAdaptor(Core core, bool createIfNotFound = true) {
            foreach (var adaptor in FindObjectsByType<LightVolumeAdaptor>(FindObjectsSortMode.None))
                if (adaptor.core == core) {
                    if (createIfNotFound) Undo.RegisterCompleteObjectUndo(adaptor, "Update Light Volume Adaptor");
                    return adaptor;
                }
            if (!createIfNotFound) return null;
            var lvSetup = FindAnyObjectByType<LightVolumeSetup>();
            if (lvSetup == null) {
                var go = new GameObject("Light Volume Manager", typeof(LightVolumeSetup));
                go.TryGetComponent(out lvSetup);
                lvSetup.SyncUdonScript();
                Undo.RegisterCreatedObjectUndo(go, "Create Light Volume Manager");
            }
            var newAdaptorObject = new GameObject("Light Volume Adaptor", typeof(LightVolumeAdaptor));
            newAdaptorObject.TryGetComponent(out LightVolumeAdaptor newAdaptor);
            GameObjectUtility.SetParentAndAlign(newAdaptorObject, core.gameObject);
            newAdaptor.core = core;
            EditorUtility.SetDirty(newAdaptor);
            Undo.RegisterCreatedObjectUndo(newAdaptorObject, "Create Light Volume Adaptor");
            return newAdaptor;
        }

        public static PointLightVolume CreateLightVolume(Transform target, Core core) {
            var adaptor = TryGetAttachedAdaptor(core);
            var lv = CreateLightVolume(target);
            ref var array = ref adaptor.pointLightVolumes;
            if (array == null || array.Length == 0)
                array = new PointLightVolumeInstance[1];
            else
                Array.Resize(ref array, array.Length + 1);
            lv.TryGetComponent(out array[^1]);
            Undo.RecordObject(adaptor, "Assign Light Volume for Screen");
            return lv;
        }

        static PointLightVolume CreateLightVolume(Transform target) {
            var lvObject = new GameObject(GameObjectUtility.GetUniqueNameForSibling(target, "Light Volume for Screen"), typeof(PointLightVolume));
            var lvTransform = lvObject.transform;
            lvTransform.SetParent(target, false);
            if (target.TryGetComponent(out Renderer renderer)) {
                var bounds = renderer.localBounds;
                lvTransform.localPosition = bounds.center;
                lvTransform.localScale = bounds.size;
            } else {
                lvTransform.localPosition = Vector3.zero;
                lvTransform.localScale = Vector3.one;
            }
            lvTransform.localRotation = Quaternion.Euler(0, 180, 0);
            lvObject.TryGetComponent(out PointLightVolume lv);
            lv.Type = PointLightVolume.LightType.AreaLight;
            lv.Dynamic = target.GetComponentInParent<VRC_Pickup>(true) != null;
            lv.SyncUdonScript();
            Undo.RegisterCreatedObjectUndo(lvObject, "Create Light Volume for Screen");
            return lv;
        }

        public void CreateLightVolume() {
#if VRC_LIGHT_VOLUMES_V3
            if (pointLightVolume == null) {
                TryGetAttachedAdaptor(core);
                Undo.RecordObject(this, "Assign Light Volume for Screen");
                pointLightVolume = CreateLightVolume(screenRenderer.transform);
                if (PrefabUtility.IsPartOfPrefabInstance(this)) PrefabUtility.RecordPrefabInstancePropertyModifications(this);
            }
            EnsureLightVolumeCookie(pointLightVolume, screenRenderer.name);
#else
            CreateLightVolume(transform, core);
#endif
        }
#endif

#if VRC_LIGHT_VOLUMES_V3
        internal static void EnsureLightVolumeCookie(PointLightVolume pointLightVolume, string name) {
            var shader = Shader.Find("Hidden/JLChnToZ/VideoBlit (VRCLightVolumes Cookie)");
            if (shader != null && (!(pointLightVolume.Cookie is Material material) || material == null || material.shader != shader)) {
                material = new Material(shader) { name = $"{name} Cookie Material" };
                MaterialUtil.SaveMaterialAsAsset(material, "", "");
                Undo.RecordObject(pointLightVolume, "Assign Cookie Material for Light Volume");
                pointLightVolume.Cookie = material;
                if (PrefabUtility.IsPartOfPrefabInstance(pointLightVolume)) PrefabUtility.RecordPrefabInstancePropertyModifications(pointLightVolume);
            }
        }
#endif

        static void RemoveIndexFromArray<T>(ref T[] array, int index) {
            if (index < 0 || index >= array.Length) return;
            var newArray = new T[array.Length - 1];
            Array.Copy(array, 0, newArray, 0, index);
            if (index < array.Length - 1) Array.Copy(array, index + 1, newArray, index, array.Length - index - 1);
            array = newArray;
        }

        static void RemoveFromCore(Core core, Renderer renderer) {
            if (core == null) return;
            int index = Array.IndexOf(core.screenTargets, renderer);
            if (index < 0) return;
            Undo.RecordObject(core, "Screen Configurator");
            RemoveIndexFromArray(ref core.screenTargets, index);
            RemoveIndexFromArray(ref core.screenTargetModes, index);
            RemoveIndexFromArray(ref core.screenTargetIndeces, index);
            RemoveIndexFromArray(ref core.screenTargetPropertyNames, index);
            RemoveIndexFromArray(ref core.avProPropertyNames, index);
            RemoveIndexFromArray(ref core.screenTargetDefaultTextures, index);
            SavePrefabModifications(core);
        }

        void Awake() {
            if (Application.isPlaying || IsPrefabEditingMode) return;
            if (firstRun) return;
            firstRun = true;
            previousRenderer = screenRenderer;
            if (core != null) previousCore = core;
            var renderer = Renderer;
            if (renderer == null) return;
            lastIndex = targetIndex;
            instances[(renderer, targetIndex)] = this;
            if (core != null) return;
            var cores = FindObjectsOfType<Core>(true);
            foreach (var c in cores) {
                if (c.screenTargets == null) continue;
                coreIndex = Array.IndexOf(c.screenTargets, renderer);
                if (coreIndex < 0) continue;
                Undo.RecordObject(this, "Screen Configurator");
                core = c;
            }
            if (core == null) {
                core = GetComponentInParent<Core>(true);
                if (core == null) {
                    if (cores.Length > 0) core = cores[0];
                    else return;
                }
                // Defer to avoid race condition with U# initial deserialization.
                EditorApplication.delayCall += DeferAddCore;
            }
            previousCore = core;
        }

        void OnValidate() {
            Awake();
            if (screenRenderer && lastIndex != targetIndex) {
                if (instances.TryGetValue((screenRenderer, lastIndex), out var instance) && instance == this)
                    instances.Remove((screenRenderer, lastIndex));
                lastIndex = targetIndex;
                instances[(screenRenderer, targetIndex)] = this;
            }
            if (Application.isPlaying || IsPrefabEditingMode) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this)) return;
            if (isPendingApply) return;
            isPendingApply = true;
            // Defer to prevent multiple updates during a single frame,
            // also avoids race condition with U# initial deserialization.
            EditorApplication.delayCall += DeferUpdateCore;
        }

        void DeferAddCore() {
            if (this == null) return; // This may happen if the object is destroyed after delayCall is scheduled.
            var renderer = Renderer;
            if (renderer == null) return;
            if (!AddToCoreIfEmpty(renderer)) AppendToCore(renderer);
        }

        void DeferUpdateCore() {
            isPendingApply = false;
            if (this == null) return; // This may happen if the object is destroyed after delayCall is scheduled.
            GetComponents(monoBehaviours);
            foreach (var mb in monoBehaviours)
                if (mb is IVizVidCompoonent compoonent) {
                    core = compoonent.Core;
                    break;
                }
            if (previousCore != core) {
                RemoveFromCore(previousCore, previousRenderer);
                previousCore = core;
            }
            if (core == null) return;
            var renderer = Renderer;
            if (renderer == null) return;
            try {
                if (renderer != screenRenderer) {
                    Undo.RecordObject(this, "Screen Configurator");
                    screenRenderer = renderer;
                }
                if (AddToCoreIfEmpty(renderer)) {
                    previousRenderer = renderer;
                    return;
                }
                if (previousRenderer == null) previousRenderer = renderer;
                coreIndex = Array.IndexOf(core.screenTargets, previousRenderer);
                if (coreIndex < 0) {
                    AppendToCore(renderer);
                    return;
                }
                if (
                    targetMode != core.screenTargetModes[coreIndex] ||
                    targetIndex != core.screenTargetIndeces[coreIndex] ||
                    targetPropertyName != core.screenTargetPropertyNames[coreIndex] ||
                    avProPropertyName != core.avProPropertyNames[coreIndex] ||
                    defaultTexture != core.screenTargetDefaultTextures[coreIndex]
                ) {
                    Undo.RecordObject(this, "Screen Configurator");
                    targetMode = core.screenTargetModes[coreIndex];
                    targetIndex = core.screenTargetIndeces[coreIndex];
                    targetPropertyName = core.screenTargetPropertyNames[coreIndex];
                    avProPropertyName = core.avProPropertyNames[coreIndex];
                    defaultTexture = core.screenTargetDefaultTextures[coreIndex];
                }
                if (previousRenderer != renderer) {
                    Undo.RecordObject(core, "Screen Configurator");
                    core.screenTargets[coreIndex] = renderer;
                }
            } finally {
                if (previousRenderer != renderer || lastIndex != targetIndex) {
                    if (instances.TryGetValue((previousRenderer, lastIndex), out var instance) && instance == this)
                        instances.Remove((previousRenderer, lastIndex));
                    if (renderer) instances[(renderer, targetIndex)] = this;
                    previousRenderer = renderer;
                }
                Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
            }
        }

        bool AddToCoreIfEmpty(Renderer renderer) {
            if (core.screenTargets != null && core.screenTargets.Length > 0)
                return false;
            Undo.RecordObject(core, "Screen Configurator");
            coreIndex = 0;
            core.screenTargets = new[] { renderer };
            core.screenTargetModes = new[] { targetMode };
            core.screenTargetIndeces = new[] { targetIndex };
            core.screenTargetPropertyNames = new[] { targetPropertyName };
            core.avProPropertyNames = new[] { avProPropertyName };
            core.screenTargetDefaultTextures = new[] { defaultTexture };
            SaveCoreModifications();
            return true;
        }

        void AppendToCore(Renderer renderer) {
            coreIndex = core.screenTargets.Length;
            int size = coreIndex + 1;
            Undo.RecordObject(core, "Screen Configurator");
            Array.Resize(ref core.screenTargets, size);
            core.screenTargets[coreIndex] = renderer;
            Array.Resize(ref core.screenTargetModes, size);
            core.screenTargetModes[coreIndex] = targetMode;
            Array.Resize(ref core.screenTargetIndeces, size);
            core.screenTargetIndeces[coreIndex] = targetIndex;
            Array.Resize(ref core.screenTargetPropertyNames, size);
            core.screenTargetPropertyNames[coreIndex] = targetPropertyName;
            Array.Resize(ref core.avProPropertyNames, size);
            core.avProPropertyNames[coreIndex] = avProPropertyName;
            Array.Resize(ref core.screenTargetDefaultTextures, size);
            core.screenTargetDefaultTextures[coreIndex] = defaultTexture;
            SaveCoreModifications();
        }

        static void SavePrefabModifications(UnityEngine.Object obj) {
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        void SaveCoreModifications() => SavePrefabModifications(core);
#endif
    }
}