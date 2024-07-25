using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build.Reporting;
using VRC.Udon;
using UdonSharp;
using UdonSharpEditor;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(RateLimitResolver))]
    public class RateLimitResolverEditor : VVMWEditorBase {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            EditorGUILayout.HelpBox(
                "This component is for debouncing (ensures it won't be called too frequently across video player instances) URL load requests.",
                MessageType.Info
            );
        }
    }

    // Resolve to single instance of RateLimitResolver in scene.
    internal class RateLimitResolverPreprocessor : UdonSharpPreProcessor {
        RateLimitResolver unifiedResolver;

        public override void OnProcessScene(Scene scene, BuildReport report) {
            var resolvers = new List<RateLimitResolver>(scene.IterateAllComponents<RateLimitResolver>());
            unifiedResolver = null;
            if (resolvers.Count == 0) return;
            for (int i = 0; i < resolvers.Count; i++) {
                var resolver = resolvers[i];
                if (unifiedResolver == null && !resolver.CompareTag("EditorOnly")) {
                    unifiedResolver = resolver;
                    unifiedResolver.transform.SetParent(null);
                    continue;
                }
                var go = resolver.gameObject;
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(resolver);
                if (udon != null) DestroyImmediate(udon);
                DestroyImmediate(resolver);
                if (go.GetComponents<Component>().Length <= 1) DestroyImmediate(go);
            }
            base.OnProcessScene(scene, report);
        }

        protected override void ProcessEntry(Type type, UdonSharpBehaviour usharp, UdonBehaviour udon) {
            var fieldInfos = GetFields<RateLimitResolver>(type);
            foreach (var field in fieldInfos)
                field.SetValue(usharp, unifiedResolver);
            UdonSharpEditorUtility.CopyProxyToUdon(usharp);
        }
    }

}