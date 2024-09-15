using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Designer;
using JLChnToZ.VRC.VVMW.Editors;
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
                    if (!LocatableAttributeDrawer.Locate(component, GetField(typeof(UIHandler), "handler"), false, true))
                        LocatableAttributeDrawer.Locate(component, GetField(typeof(UIHandler), "core"), true, true);
                } else if (component is OverlayControl) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(OverlayControl), "core"), true, true);
                } else if (component is ResyncButtonConfigurator) {
                    LocatableAttributeDrawer.Locate(component, GetField(typeof(ResyncButtonConfigurator), "core"), true, true);
                } else if (component is AutoPlayOnNear) {
                    var handler = LocatableAttributeDrawer.Locate(component, GetField(typeof(AutoPlayOnNear), "handler"), true, true) as FrontendHandler;
                    if (handler != null) {
                        using (var so = new SerializedObject(handler)) {
                            so.FindProperty("autoPlay").boolValue = false;
                            so.ApplyModifiedProperties();
                        }
                        LocatableAttributeDrawer.Locate(handler, GetField(typeof(FrontendHandler), "core"), true, true);
                        var core = handler.core;
                        using (var so = new SerializedObject(core)) {
                            so.FindProperty("synced").boolValue = false;
                            so.ApplyModifiedProperties();
                        }
                    }
                }
            }
            Undo.RegisterCreatedObjectUndo(go, $"Create {go.name}");
            Selection.activeGameObject = go;
            return go;
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

        [MenuItem(createMenuRoot + "Video Player (Core only)", false, 49)]
        static void CreateNoControls() => SpawnPrefab(packageRoot + "VVMW (No Controls).prefab");

        [MenuItem(createMenuRoot + "Video Player (Separated Controls)", false, 49)]
        static void CreateSeparateControls() => SpawnPrefab(packageRoot + "VVMW (Separated Controls).prefab");

        [MenuItem(createMenuRoot + "Video Player (On-Screen Controls)", false, 49)]
        static void CreateOnScreenControls() => SpawnPrefab(packageRoot + "VVMW (On-Screen Controls).prefab");

        [MenuItem(createMenuRoot + "YTTL", false, 49)]
        static void CreateYTTL() {
            SpawnSingletonPrefab<YttlManager>(prefabRoot + "Third-Parties/YTTL/YTTL Manager.prefab");
            foreach (var core in SceneManager.GetActiveScene().IterateAllComponents<Core>(true))
                LocatableAttributeDrawer.Locate(core, GetField(typeof(Core), "yttl"), true, true);
        }

        [MenuItem(createMenuRoot + "Additional Controls/Screen", false, 49)]
        static void CreateScreen() {
            var go = SpawnPrefab(prefabRoot + "Default Screen.prefab");
            var core = FUtils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.GetComponent<Renderer>());
        }

        [MenuItem(createMenuRoot + "Additional Controls/Pickupable Screen", false, 49)]
        static void CreatePickupScreen() {
            var go = SpawnPrefab(prefabRoot + "Pickup Screen.prefab");
            var core = FUtils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.transform.Find("Pickup_ScalingPanel/ScreenScaling/Screen").GetComponent<Renderer>());
        }

        [MenuItem(createMenuRoot + "Additional Controls/Audio Source", false, 49)]
        static void CreateAudioSource() {
            var go = SpawnPrefab(prefabRoot + "Default Audio Source.prefab");
            var core = FUtils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.GetComponent<AudioSource>());
        }

        [MenuItem(createMenuRoot + "Additional Controls/Separated Controls", false, 49)]
        static void CreateSeparateContols() => SpawnPrefab(prefabRoot + "Default UI.prefab");

        [MenuItem(createMenuRoot + "Additional Controls/Separated Controls (Narrow)", false, 49)]
        static void CreateSeparateNarrowContols() => SpawnPrefab(prefabRoot + "Default UI (Narrow).prefab");

        [MenuItem(createMenuRoot + "Additional Controls/On-Screen Controls With Screen", false, 49)]
        static void CreateOnScreenContols() {
            var go = SpawnPrefab(prefabRoot + "Screen With Overlay.prefab");
            var core = FUtils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.transform.Find("Screen").GetComponent<Renderer>());
        }

        [MenuItem(createMenuRoot + "Additional Controls/Overlay Controls", false, 49)]
        static void CreateOverlayControls() {
            if (FindObjectOfType<OverlayControl>() != null &&
                !EditorI18N.Instance.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.Pickups.PickupPanel.multiple_message")) return;
            SpawnPrefab(prefabRoot + "Overlay Control.prefab");
        }

        [MenuItem(createMenuRoot + "Additional Controls/Resync Button", false, 49)]
        static void CreateResyncButton() => SpawnPrefab(prefabRoot + "Re-Sync Button.prefab");

        [MenuItem(createMenuRoot + "Additional Controls/Global Resync Button", false, 49)]
        static void CreateGlobalSyncButton() => SpawnPrefab(prefabRoot + "Global Sync Button.prefab");

        [MenuItem(createMenuRoot + "Additional Controls/Auto Play On Near (Local Only)", false, 49)]
        static void CreateAutoPlayOnNear() => SpawnPrefab(prefabRoot + "Auto Play On Near.prefab");
    }
}