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
#if VRCLV2_IMPORTED
using VRCLightVolumes;
#if VRCLV3_IMPORTED && !VRCLV3_EARLY_VERSION
using PointLightVolume = VRCLightVolumes.PointLightVolumeInstance;
#endif
#endif
using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Designer {
    [ExecuteInEditMode]
    [EditorOnly]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#screen-configurator")]
    [AddComponentMenu("VizVid/Components/Screen Configurator")]
    public class ScreenConfigurator : MonoBehaviour, IVizVidCompoonent {
        static readonly List<MonoBehaviour> monoBehaviours = new List<MonoBehaviour>();
        static readonly Dictionary<(Renderer, int), ScreenConfigurator> instances = new Dictionary<(Renderer, int), ScreenConfigurator>();
        readonly HashSet<HideIfIdleTextureAvailable> hideIfIdleTextureAvailableComponents = new HashSet<HideIfIdleTextureAvailable>();
        [SerializeField, Locatable, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] internal Core core;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.videoScreenTarget")] internal Renderer screenRenderer;
        [SerializeField] int targetMode = 1;
        [SerializeField] int targetIndex = -1;
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetPropertyNames")] string targetPropertyName = "_MainTex";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.avProPropertyNames")] string avProPropertyName = "_IsAVProVideo";
        [SerializeField, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core.screenTargetDefaultTextures")] Texture defaultTexture;
#if VRCLV3_IMPORTED
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

        internal bool HasIdleTexture => core == null ? defaultTexture != null :
            core.screenTargetDefaultTextures != null && coreIndex >= 0 &&
            coreIndex < core.screenTargetDefaultTextures.Length &&
            (core.screenTargetDefaultTextures[coreIndex] != null || core.defaultTexture != null
#if UNITY_EDITOR
            && AssetDatabase.GetAssetPath(core.defaultTexture) != "Packages/idv.jlchntoz.vvmw/Textures/Sprites/black.png"
#endif
            );

        public static ScreenConfigurator GetInstance(Renderer renderer, int index = -1) {
            if (renderer && instances.TryGetValue((renderer, index), out var instance))
                return instance;
            return null;
        }

        internal void RegisterHideIfIdleTextureAvailable(HideIfIdleTextureAvailable component) {
            if (component == null) return;
            hideIfIdleTextureAvailableComponents.Add(component);
            component.ShouldHide = HasIdleTexture;
        }

        internal void UnregisterHideIfIdleTextureAvailable(HideIfIdleTextureAvailable component) {
            if (component == null) return;
            hideIfIdleTextureAvailableComponents.Remove(component);
            component.ShouldHide = false;
        }

        internal static void NotifyDefaultTextureChanged(Renderer target, int index) {
            if (instances.TryGetValue((target, index), out var instance)) {
                bool hasIdleTexture = instance.HasIdleTexture;
                foreach (var component in instance.hideIfIdleTextureAvailableComponents)
                    if (component != null) component.ShouldHide = hasIdleTexture;
            }
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
#if VRCLV2_IMPORTED
        static LightVolumeAdaptor TryGetAttachedAdaptor(Core core, bool createIfNotFound = true) {
            foreach (var adaptor in FindObjectsByType<LightVolumeAdaptor>(FindObjectsSortMode.None))
                if (adaptor.core == core) {
                    if (createIfNotFound) Undo.RegisterCompleteObjectUndo(adaptor, "Update Light Volume Adaptor");
                    return adaptor;
                }
            if (!createIfNotFound) return null;
#if !VRCLV3_IMPORTED || VRCLV3_EARLY_VERSION
            var lvSetup = FindAnyObjectByType<LightVolumeSetup>();
            if (lvSetup == null) {
                var go = new GameObject("Light Volume Manager", typeof(LightVolumeSetup));
                go.TryGetComponent(out lvSetup);
                lvSetup.SyncUdonScript();
                Undo.RegisterCreatedObjectUndo(go, "Create Light Volume Manager");
            }
#else
            var lvSetup = FindAnyObjectByType<LightVolumeManager>();
            if (lvSetup == null) {
                var go = new GameObject("Light Volume Manager", typeof(LightVolumeManager));
                Undo.RegisterCreatedObjectUndo(go, "Create Light Volume Manager");
            }
#endif
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
#if !VRCLV3_IMPORTED || VRCLV3_EARLY_VERSION
            lv.Type = PointLightVolume.LightType.AreaLight;
            lv.Dynamic = target.GetComponentInParent<VRC_Pickup>(true) != null;
            lv.SyncUdonScript();
#else
            lv.LightType = 2; // AreaLight
            lv.IsDynamic = target.GetComponentInParent<VRC_Pickup>(true) != null;
#endif
            Undo.RegisterCreatedObjectUndo(lvObject, "Create Light Volume for Screen");
            return lv;
        }

        public void CreateLightVolume() {
#if VRCLV3_IMPORTED
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

#if VRCLV3_IMPORTED
        internal static void EnsureLightVolumeCookie(PointLightVolume pointLightVolume, string name) {
            var shader = Shader.Find("Hidden/JLChnToZ/VideoBlit (VRCLightVolumes Cookie)");
            bool valueChanged = false;
            if (shader != null && (!(pointLightVolume.Cookie is Material material) || material == null || material.shader != shader)) {
                material = new Material(shader) { name = $"{name} Cookie Material" };
                MaterialUtil.SaveMaterialAsAsset(material, "", "");
                if (!valueChanged) {
                    Undo.RecordObject(pointLightVolume, "Assign Cookie Material for Light Volume");
                    valueChanged = true;
                }
                pointLightVolume.Cookie = material;
            }
            if (pointLightVolume.Color.maxColorComponent <= 0f) {
                if (!valueChanged) {
                    Undo.RecordObject(pointLightVolume, "Assign Color for Light Volume");
                    valueChanged = true;
                }
                pointLightVolume.Color = Color.white;
            }
            if (valueChanged) {
                if (PrefabUtility.IsPartOfPrefabInstance(pointLightVolume))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(pointLightVolume);
#if !VRCLV3_IMPORTED || VRCLV3_EARLY_VERSION
                pointLightVolume.SyncUdonScript();
#endif
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

        static void SavePrefabModifications(UnityObject obj) {
            if (PrefabUtility.IsPartOfPrefabInstance(obj))
                PrefabUtility.RecordPrefabInstancePropertyModifications(obj);
        }

        void SaveCoreModifications() => SavePrefabModifications(core);
#endif
    }
}