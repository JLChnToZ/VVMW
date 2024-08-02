using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class EditorOnlyAttributePreprocessor : IPreprocessor {
        public int CallbackOrder => 100;

        public void OnPreprocess(Scene scene) {
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