using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
#if VRC_LIGHT_VOLUMES_V2
using VRC.SDKBase;
using VRCLightVolumes;
#endif
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Designer;
using VVMW.ThirdParties.Yttl;

using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    public static class MenuUtil {
        const string createMenuRoot = "GameObject/VizVid/";
        const string packageRoot = "Packages/idv.jlchntoz.vvmw/";
        const string prefabRoot = packageRoot + "Prefabs/";

        static readonly Dictionary<(Type, string), FieldInfo> fieldCache = new Dictionary<(Type, string), FieldInfo>();

        static GameObject SpawnPrefab(string path, bool spawnOnRoot = false) {
            var parent = spawnOnRoot ? null : Selection.activeTransform;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) {
                Debug.LogError($"Cannot find prefab at {path}");
                return null;
            }
            var instanceName = GameObjectUtility.GetUniqueNameForSibling(parent, prefab.name);
            var go = PrefabUtility.InstantiatePrefab(prefab, parent) as GameObject;
            if (go == null) {
                Debug.LogError($"Cannot instantiate prefab at {path}");
                return null;
            }
            go.name = instanceName;
            foreach (var component in go.GetComponentsInChildren<MonoBehaviour>(true)) {
                if (component is Core) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(Core), "audioLink"), false, true);
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(Core), "yttl"), false, true);
                } else if (component is FrontendHandler) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(FrontendHandler), "core"), true, true);
                } else if (component is UIHandler) {
                    Resolve(component);
                } else if (component is OverlayControl) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(OverlayControl), "core"), true, true);
                } else if (component is ResyncButtonConfigurator) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(ResyncButtonConfigurator), "core"), true, true);
                } else if (component is ActiveRegionConfig) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(ActiveRegionConfig), "core"), true, true);
                } else if (component is AutoPlayOnNearV2) {
                    var (core, handler) = Resolve(component);
                    if (handler != null)
                        using (var so = new SerializedObject(handler)) {
                            so.FindProperty("autoPlay").boolValue = false;
                            so.ApplyModifiedProperties();
                        }
                    if (core != null)
                        using (var so = new SerializedObject(core)) {
                            so.FindProperty("synced").boolValue = false;
                            so.ApplyModifiedProperties();
                        }
                } else if (component is StreamLinkAssigner) {
                    Resolve(component, frontendHandlerFieldName: "frontendHandler");
                }
            }
            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeGameObject = go;
            return go;
        }

        static (Core, FrontendHandler) Resolve(Component component, string coreFieldName = "core", string frontendHandlerFieldName = "handler", bool autoCreate = false) {
            var type = component.GetType();
            var frontendHandlerField = GetField(type, frontendHandlerFieldName);
            var coreField = GetField(type, coreFieldName);
            var core = coreField.GetValue(component) as Core;
            var frontendHandler = frontendHandlerField.GetValue(component) as FrontendHandler;
            if (core == null && frontendHandler == null) {
                core = LocatableAttributeDrawer.Locate(component, coreField, false, true) as Core;
                frontendHandler = LocatableAttributeDrawer.Locate(component, frontendHandlerField, false, true) as FrontendHandler;
            }
            if (core == null && frontendHandler == null) {
                if (autoCreate) {
                    frontendHandler = LocatableAttributeDrawer.Locate(component, frontendHandlerField, true, true) as FrontendHandler;
                    core = LocatableAttributeDrawer.Locate(frontendHandler, GetField(typeof(FrontendHandler), "core"), true, true) as Core;
                }
            } else if (core != null && frontendHandler != null) {
                core = LocatableAttributeDrawer.Locate(frontendHandler, GetField(typeof(FrontendHandler), "core"), true, true) as Core;
                coreField.SetValue(component, null);
            }
            return (core, frontendHandler);
        }

        static GameObject SpawnSingletonPrefab<T>(string path) where T : Component {
            T component = FindObjectOfType<T>();
            if (component != null) return component.gameObject;
            return SpawnPrefab(path, true);
        }

        static FieldInfo GetField(Type type, string fieldName) {
            var key = (type, fieldName);
            if (!fieldCache.TryGetValue(key, out var result)) {
                result = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                fieldCache[key] = result;
            }
            return result;
        }

        [MenuItem(createMenuRoot + "Video Player (On-Screen Controls)", false, 49)]
        static void CreateOnScreenControls() => SpawnPrefab(packageRoot + "VVMW (On-Screen Controls).prefab");

        [MenuItem(createMenuRoot + "Video Player (Separated Controls)", false, 50)]
        static void CreateSeparateControls() => SpawnPrefab(packageRoot + "VVMW (Separated Controls).prefab");

        [MenuItem(createMenuRoot + "Video Player (For Single Video Exhibition)", false, 61)]
        static void CreateSingleVideoExhibition() => SpawnPrefab(packageRoot + "VVMW (Single Video Exhibition Setup).prefab");

        [MenuItem(createMenuRoot + "Video Player (For Multiple Video Exhibition)", false, 62)]
        static void CreateMultiVideoExhibition() => SpawnPrefab(packageRoot + "VVMW (Multiple Video Exhibition Setup).prefab");

        [MenuItem(createMenuRoot + "Recommend Setups/YTTL", false, 73)]
        static void CreateYTTL() {
            SpawnSingletonPrefab<YttlManager>(prefabRoot + "Third-Parties/YTTL/YTTL Manager.prefab");
            foreach (var core in SceneManager.GetActiveScene().IterateAllComponents<Core>(true))
                LocatableAttributeDrawer.Locate(core, GetField(typeof(Core), "yttl"), true, true);
        }

        [MenuItem(createMenuRoot + "Modules/VizVid Core", false, 84)]
        static void CreateNoControls() => SpawnPrefab(packageRoot + "VVMW (No Controls).prefab");

        [MenuItem(createMenuRoot + "Modules/On-Screen Controls With Screen", false, 95)]
        static void CreateOnScreenControlsMenu() => SpawnPrefab(prefabRoot + "Screen With Overlay.prefab");

        [MenuItem(createMenuRoot + "Modules/Separated Controls", false, 96)]
        static void CreateSeparateControlsMenu() => SpawnPrefab(prefabRoot + "Default UI.prefab");

        [MenuItem(createMenuRoot + "Modules/Separated Controls (Narrow)", false, 97)]
        static void CreateSeparateNarrowControls() => SpawnPrefab(prefabRoot + "Default UI (Narrow).prefab");

        [MenuItem(createMenuRoot + "Modules/Separated Controls (With Alt. URL Input, Narrow)", false, 98)]
        static void CreateSeparateNarrowControlsDual() => SpawnPrefab(prefabRoot + "Default UI Dual Input (Narrow).prefab");

        [MenuItem(createMenuRoot + "Modules/Overlay Controls", false, 99)]
        static void CreateOverlayControls() {
            if (FindObjectOfType<OverlayControl>() != null &&
                !EditorI18N.Instance.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.Pickups.PickupPanel.multiple_message")) return;
            SpawnPrefab(prefabRoot + "Overlay Control.prefab");
        }

        [MenuItem(createMenuRoot + "Modules/Pickupable Screen", false, 110)]
        static void CreatePickupScreen() => SpawnPrefab(prefabRoot + "Pickup Screen.prefab");

        [MenuItem(createMenuRoot + "Modules/Screen", false, 111)]
        static void CreateScreen() => SpawnPrefab(prefabRoot + "Default Screen.prefab");

        [MenuItem(createMenuRoot + "Modules/Resync Button", false, 122)]
        static void CreateResyncButton() => SpawnPrefab(prefabRoot + "Re-Sync Button.prefab");

        [MenuItem(createMenuRoot + "Modules/Global Resync Button", false, 123)]
        static void CreateGlobalSyncButton() => SpawnPrefab(prefabRoot + "Global Sync Button.prefab");

        [MenuItem(createMenuRoot + "Modules/Stream Key Assigner", false, 124)]
        static void CreateStreamAssigner() => SpawnPrefab(prefabRoot + "Stream Key Assigner.prefab");

        [MenuItem(createMenuRoot + "Modules/Audio Source (Mono)", false, 135)]
        static void CreateMonoAudioSource() {
            var go = SpawnPrefab(prefabRoot + "Default Audio Source.prefab");
            AppendAudioSource(go, true);
        }

        [MenuItem(createMenuRoot + "Modules/Audio Source (Stereo)", false, 136)]
        static void CreateStereoAudioSource() {
            var go = SpawnPrefab(prefabRoot + "Stereo Audio Source.prefab");
            AppendAudioSource(go, true);
        }

        [MenuItem(createMenuRoot + "Modules/Audio Source (5.1 Surround)", false, 137)]
        static void CreateSurroundAudioSource() {
            var go = SpawnPrefab(prefabRoot + "Surround Audio Source.prefab");
            AppendAudioSource(go, true);
        }

        static void AppendAudioSource(GameObject go, bool removeDefaultAudioSource = false) {
            var core = FUtils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core == null) return;
            var audioSources = go.GetComponentsInChildren<AudioSource>(true);
            Undo.RecordObject(core, "Add Audio Source");
            if (core.audioSources == null) {
                core.audioSources = audioSources;
            } else {
                Array.Resize(ref core.audioSources, core.audioSources.Length + audioSources.Length);
                Array.Copy(audioSources, 0, core.audioSources, core.audioSources.Length - audioSources.Length, audioSources.Length);
            }
            EditorUtility.SetDirty(core);
            if (core.playerHandlers == null || core.playerHandlers.Length == 0) return;
            foreach (var handler in core.playerHandlers) {
                var vph = handler as VideoPlayerHandler;
                if (vph == null) continue;
                if (vph.TryGetComponent(out VRCAVProVideoPlayer avpro)) {
                    foreach (var audioSource in audioSources) {
                        if (!audioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker)) continue;
                        using (var so = new SerializedObject(speaker)) {
                            so.FindProperty("videoPlayer").objectReferenceValue = avpro;
                            so.ApplyModifiedProperties();
                        }
                    }
                    continue;
                }
                if (removeDefaultAudioSource && vph.TryGetComponent(out VRCUnityVideoPlayer unity)) {
                    using (var so = new SerializedObject(unity)) {
                        var prop = so.FindProperty("targetAudioSources");
                        for (int i = 0; i < prop.arraySize; i++) {
                            var audioSource = prop.GetArrayElementAtIndex(i).objectReferenceValue as AudioSource;
                            if (audioSource == null || !audioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker)) continue;
                            Undo.DestroyObjectImmediate(speaker);
                        }
                    }
                    continue;
                }
            }
        }

        [MenuItem(createMenuRoot + "Modules/Auto Play On Near (Local Only)", false, 148)]
        static void CreateAutoPlayOnNear() => SpawnPrefab(prefabRoot + "Auto Play On Near.prefab");

#if VRC_LIGHT_VOLUMES_V2
        [MenuItem(createMenuRoot + "Modules/Light Volume for Screen", false, 159)]
        static void CreateLightVolumeForScreen() {
            foreach (var screenObject in Selection.gameObjects)
                CreateLightVolumeForScreen(screenObject);
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        static void CreateLightVolumeForScreen(GameObject screenObject) {
            if (screenObject == null) return;
            var core = TryGetCoreFromScreenTarget(screenObject, out var targetTransform);
            if (core == null) return;
            var adaptor = TryGetAttachedAdaptor(core);
            var lvObject = new GameObject("Light Volume for Screen", typeof(PointLightVolume));
            var lvTransform = lvObject.transform;
            lvTransform.SetParent(targetTransform, false);
            if (targetTransform.TryGetComponent(out Renderer renderer)) {
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
            lv.Dynamic = screenObject.GetComponentInParent<VRC_Pickup>(true) != null;
            lv.SyncUdonScript();

            var array = adaptor.pointLightVolumes;
            if (array == null || array.Length == 0)
                array = new PointLightVolumeInstance[1];
            else
                Array.Resize(ref array, array.Length + 1);
            lvObject.TryGetComponent(out array[^1]);
            adaptor.pointLightVolumes = array;

            EditorUtility.SetDirty(adaptor);
            Undo.RegisterCreatedObjectUndo(lvObject, "Create Light Volume for Screen");
        }

        [MenuItem(createMenuRoot + "Modules/Light Volume for Screen", true, 159)]
        static bool CreateLightVolumeForScreenValidate() {
            var selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject == null) return false;
            var core = TryGetCoreFromScreenTarget(selectedGameObject, out _);
            return core != null;
        }

        static Core TryGetCoreFromScreenTarget(GameObject target, out Transform targetTransform) {
            if (target.TryGetComponent(out ScreenConfigurator screenConfigurator)) {
                targetTransform = screenConfigurator.screenRenderer != null ? screenConfigurator.screenRenderer.transform : target.transform;
                return screenConfigurator.core;
            }
            foreach (var c in FindObjectsByType<Core>(FindObjectsSortMode.None))
                foreach (var screenTarget in c.screenTargets)
                    if (screenTarget is Component component && component.gameObject == target) {
                        targetTransform = target.transform;
                        return c;
                    }
            targetTransform = null;
            return null;
        }

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
#endif

        [MenuItem(createMenuRoot + "Modules/Active Region", false, 170)]
        static void CreateActiveRegion() {
            var go = SpawnPrefab(prefabRoot + "Active Region.prefab");
        }
    }
}
