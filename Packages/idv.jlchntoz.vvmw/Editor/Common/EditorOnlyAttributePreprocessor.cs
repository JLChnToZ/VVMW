using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    public class EditorOnlyAttributePreprocessor : IProcessSceneWithReport {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) {
            var hasAttributeCache = new Dictionary<Type, bool>();
            foreach (var behaviour in scene.IterateAllComponents<MonoBehaviour>()) {
                var type = behaviour.GetType();
                if (!hasAttributeCache.TryGetValue(type, out var hasAttribute)) {
                    hasAttribute = type.GetCustomAttribute<EditorOnlyAttribute>() != null;
                    hasAttributeCache[type] = hasAttribute;
                }
                if (hasAttribute) DestroyImmediate(behaviour, true);
            }
        }
    }
}