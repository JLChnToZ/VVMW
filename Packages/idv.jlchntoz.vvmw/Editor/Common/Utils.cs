using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace JLChnToZ.VRC.VVMW {
    public static class Utils {
        public static IEnumerable<T> IterateAllComponents<T>(this Scene scene, bool includeEditorOnly = false) where T : Component {
            var pending = new Stack<Transform>();
            var components = new List<T>();
            var rootGameObjects = scene.GetRootGameObjects();
            for (int i = rootGameObjects.Length - 1; i >= 0; i--) pending.Push(rootGameObjects[i].transform);
            while (pending.Count > 0) {
                var transform = pending.Pop();
                if (transform == null || (!includeEditorOnly && transform.tag == "EditorOnly")) continue;
                for (int i = transform.childCount - 1; i >= 0; i--) pending.Push(transform.GetChild(i));
                components.Clear();
                transform.GetComponents(components);
                foreach (var component in components) if (component != null) yield return component;
            }
        }
    }
}