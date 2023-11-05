using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using VVMW.ThirdParties.Yttl;

using UnityObject = UnityEngine.Object;
using static UnityEngine.Object;
using JLChnToZ.VRC.VVMW.Designer;
using JLChnToZ.VRC.VVMW.Editors;

namespace JLChnToZ.VRC.VVMW {
    public static class MenuUtil {
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

        [MenuItem("GameObject/VizVid/Video Player (Core only)", false, 49)]
        static void CreateNoControls() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/VVMW (No Controls).prefab");

        [MenuItem("GameObject/VizVid/Video Player (Separated Controls)", false, 49)]
        static void CreateSeparateControls() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/VVMW (Separated Controls).prefab");

        [MenuItem("GameObject/VizVid/Video Player (On-Screen Controls)", false, 49)]
        static void CreateOnScreenControls() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/VVMW (On-Screen Controls).prefab");

        [MenuItem("GameObject/VizVid/YTTL", false, 49)]
        static void CreateYTTL() {
            SpawnSingletonPrefab<YttlManager>("Packages/idv.jlchntoz.vvmw/Prefabs/Third-Parties/YTTL/YTTL Manager.prefab");
            foreach (var core in SceneManager.GetActiveScene().IterateAllComponents<Core>(true))
                LocatableAttributeDrawer.Locate(core, GetField(typeof(Core), "yttl"), true, true);
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Screen", false, 49)]
        static void CreateScreen() {
            var go = SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Default Screen.prefab");
            var core = Utils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.GetComponent<Renderer>());
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Pickupable Screen", false, 49)]
        static void CreatePickupScreen() {
            var go = SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Pickup Screen.prefab");
            var core = Utils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.transform.Find("Pickup_ScalingPanel/ScreenScaling/Screen").GetComponent<Renderer>());
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Audio Source", false, 49)]
        static void CreateAudioSource() {
            var go = SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Default Audio Source.prefab");
            var core = Utils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.GetComponent<AudioSource>());
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Separated Controls", false, 49)]
        static void CreateSeparateContols() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Default UI.prefab");

        [MenuItem("GameObject/VizVid/Additional Controls/Separated Controls (Narrow)", false, 49)]
        static void CreateSeparateNarrowContols() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Default UI (Narrow).prefab");

        [MenuItem("GameObject/VizVid/Additional Controls/On-Screen Controls With Screen", false, 49)]
        static void CreateOnScreenContols() {
            var go = SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Screen With Overlay.prefab");
            var core = Utils.FindClosestComponentInHierarchy<Core>(go.transform);
            if (core != null) CoreEditor.AddTarget(core, go.transform.Find("Screen").GetComponent<Renderer>());
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Overlay Controls", false, 49)]
        static void CreateOverlayControls() {
            if (FindObjectOfType<OverlayControl>() != null && !EditorUtility.DisplayDialog(
                "Warning",
                "You already have an Overlay Control in scene, adding another one may cause unexpected behavior, are you sure to continue?",
                "Yes", "No"
            )) return;
            SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Overlay Control.prefab");
        }

        [MenuItem("GameObject/VizVid/Additional Controls/Resync Button", false, 49)]
        static void CreateResyncButton() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Re-Sync Button.prefab");

        [MenuItem("GameObject/VizVid/Additional Controls/Global Resync Button", false, 49)]
        static void CreateGlobalSyncButton() => SpawnPrefab("Packages/idv.jlchntoz.vvmw/Prefabs/Global Sync Button.prefab");
    }
}