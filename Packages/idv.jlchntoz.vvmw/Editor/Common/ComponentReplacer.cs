using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    public class ComponentReplacer {
        static readonly Dictionary<Component, List<(Component, string)>> references = new Dictionary<Component, List<(Component, string)>>();
        static readonly Dictionary<Type, Type[]> dependents = new Dictionary<Type, Type[]>();
        readonly List<ComponentReplacer> downstreams = new List<ComponentReplacer>();
        readonly Type componentType;
        readonly GameObject sourceGameObject;
        GameObject temporaryGameObject;
        readonly Component[] componentsInGameObject;
        Component[] componentsInTemporary;
        readonly int componentIndex;

        public static T TryReplaceComponent<T>(Component oldComponent, bool copyContent) where T : Component {
            if (oldComponent == null) return null;
            var gameObject = oldComponent.gameObject;
            var components = gameObject.GetComponents<Component>();
            var index = Array.IndexOf(components, oldComponent);
            if (index < 0) {
                Debug.LogWarning($"Component {oldComponent.GetType()} is not found in the GameObject.");
                return null;
            }
            var replacer = new ComponentReplacer(gameObject, components, index);
            replacer.CloneToTemporary();
            replacer.DestroyDependents();
            var newComponent = gameObject.AddComponent<T>();
            if (copyContent) EditorUtility.CopySerializedIfDifferent(oldComponent, newComponent);
            replacer.RestoreDependents();
            replacer.DestroyTemporary();
            return newComponent;
        }

        static bool IsRequired(Type type, Type checkType) {
            if (!dependents.TryGetValue(type, out var types)) {
                var temp = new List<Type>();
                foreach (var requireComponent in type.GetCustomAttributes<RequireComponent>(true)) {
                    if (requireComponent.m_Type0 != null) temp.Add(requireComponent.m_Type0);
                    if (requireComponent.m_Type1 != null) temp.Add(requireComponent.m_Type1);
                    if (requireComponent.m_Type2 != null) temp.Add(requireComponent.m_Type2);
                }
                dependents[type] = types = temp.ToArray();
            }
            foreach (var t in types)
                if (t.IsAssignableFrom(checkType))
                    return true;
            return false;
        }

        public static void InitAllComponents() {
            #if UNITY_2022_2_OR_NEWER
            int scneCount = SceneManager.loadedSceneCount;
            #else
            int scneCount = SceneManager.sceneCount;
            #endif
            var roots = new List<GameObject>();
            var temp = new List<Component>();
            var stack = new Stack<Transform>();
            for (int i = 0; i < scneCount; i++) {
                SceneManager.GetSceneAt(i).GetRootGameObjects(roots);
                foreach (var root in roots)
                    stack.Push(root.transform);
            }
            while (stack.Count > 0) {
                var current = stack.Pop();
                for (int i = current.childCount - 1; i >= 0; i--)
                    stack.Push(current.GetChild(i));
                current.GetComponents(temp);
                foreach (var c in temp) {
                    if (c == null || c is Transform) continue;
                    using (var so = new SerializedObject(c)) {
                        var sp = so.GetIterator();
                        while (sp.Next(true)) {
                            if (sp.propertyType != SerializedPropertyType.ObjectReference) continue;
                            var target = sp.objectReferenceValue as Component;
                            if (target == null || target == c) continue;
                            if (!references.TryGetValue(target, out var mapping))
                                references[target] = mapping = new List<(Component, string)>();
                            mapping.Add((c, sp.propertyPath));
                        }
                    }
                }
            }
        }

        public static ICollection<(Component, string)> GetReferencedComponents(Component component) =>
            component != null && references.TryGetValue(component, out var mapping) ?
            mapping : Array.Empty<(Component, string)>();

        ComponentReplacer(GameObject sourceGameObject, Component[] components, int index) {
            this.sourceGameObject = sourceGameObject;
            componentsInGameObject = components;
            componentIndex = index;
            var component = components[index];
            componentType = component.GetType();
            foreach (var c in componentsInGameObject) {
                if (c == null || c == component || !IsRequired(c.GetType(), componentType)) continue;
                int i = Array.IndexOf(componentsInGameObject, c);
                if (i >= 0) downstreams.Add(new ComponentReplacer(sourceGameObject, componentsInGameObject, i));
                else Debug.LogWarning($"Component {c.GetType()} is required by {componentType} but not found in the same GameObject.");
            }
        }

        void CloneToTemporary() {
            if (temporaryGameObject != null) return;
            temporaryGameObject = Instantiate(sourceGameObject);
            temporaryGameObject.hideFlags = HideFlags.HideAndDontSave;
            var queue = new Queue<ComponentReplacer>();
            queue.Enqueue(this);
            componentsInTemporary = temporaryGameObject.GetComponents<Component>();
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                foreach (var downstream in current.downstreams) queue.Enqueue(downstream);
                current.componentsInTemporary = componentsInTemporary;
            }
        }

        void DestroyDependents() {
            var stack = new Stack<ComponentReplacer>();
            var queue = new Queue<ComponentReplacer>();
            queue.Enqueue(this);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                stack.Push(current);
                foreach (var downstream in current.downstreams) queue.Enqueue(downstream);
            }
            while (stack.Count > 0) {
                var current = stack.Pop();
                DestroyImmediate(current.componentsInGameObject[current.componentIndex]);
            }
        }

        void RestoreDependents() {
            var stack = new Stack<ComponentReplacer>();
            stack.Push(this);
            while (stack.Count > 0) {
                var current = stack.Pop();
                foreach (var downstream in current.downstreams) stack.Push(downstream);
                var temp = sourceGameObject.AddComponent(current.componentType);
                current.componentsInGameObject[current.componentIndex] = temp;
                if (temp != null) EditorUtility.CopySerializedIfDifferent(current.componentsInTemporary[current.componentIndex], temp);
                if (references.TryGetValue(current.componentsInTemporary[current.componentIndex], out var mapping))
                    foreach (var (component, path) in mapping) {
                        using (var so = new SerializedObject(component)) {
                            var sp = so.FindProperty(path);
                            sp.objectReferenceValue = temp;
                            so.ApplyModifiedProperties();
                        }
                    }
            }
        }

        void DestroyTemporary() {
            if (temporaryGameObject == null) return;
            DestroyImmediate(temporaryGameObject);
            temporaryGameObject = null;
            var queue = new Queue<ComponentReplacer>();
            queue.Enqueue(this);
            while (queue.Count > 0) {
                var current = queue.Dequeue();
                foreach (var downstream in current.downstreams) queue.Enqueue(downstream);
                current.componentsInTemporary = null;
            }
        }
    }
}