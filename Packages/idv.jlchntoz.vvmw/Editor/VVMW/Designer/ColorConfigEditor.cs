using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Editors;
using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;

namespace JLChnToZ.VRC.VVMW.Designer {
    [CustomEditor(typeof(ColorConfig))]
    [CanEditMultipleObjects]
    public class ColorConfigEditor : VVMWEditorBase {
        SerializedProperty colorsProperty;
        SerializedProperty autoApplyOnBuildProperty;
        bool addRemoveFoldout;
        bool showApplyAll;

        static Vector4 Color2Chroma(Color c) {
            const float sqrt3Over2 = 0.8660254f; // sqrt(3)/2
            return new Vector4(c.r - 0.5f * (c.g + c.b), sqrt3Over2 * (c.g - c.b), Mathf.Max(c.r, Mathf.Max(c.g, c.b)), c.a);
        }

        static Color Chroma2Color(Vector4 c) {
            const float _2Over3 = 2F / 3F; // 2/3
            const float _n1Over3 = -1F / 3F; // -1/3
            const float sqrt3Over3 = 0.5773503f; // sqrt(3)/3
            var color = new Vector3(
                c.x * _2Over3,
                c.y * sqrt3Over3 + c.x * _n1Over3,
                c.y * -sqrt3Over3 + c.x * _n1Over3
            );
            color += Vector3.one * (c.z - Mathf.Max(color.x, Mathf.Max(color.y, color.z)));
            return new Color(color.x, color.y, color.z, c.w);
        }

        protected override void OnEnable() {
            base.OnEnable();
            colorsProperty = serializedObject.FindProperty("colors");
            autoApplyOnBuildProperty = serializedObject.FindProperty("autoApplyOnBuild");
        }

        public override void DrawEmbeddedInspectorGUI() {
            int count = colorsProperty.arraySize;
            DrawChromaEditor(count);
            EditorGUILayout.Space();
            for (int i = 0; i < count; i++) {
                using var colorProperty = colorsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(colorProperty, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorN", i + 1));
            }
            EditorGUILayout.PropertyField(autoApplyOnBuildProperty);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.apply"))) {
                    foreach (var colorConfig in targets)
                        (colorConfig as ColorConfig).ConfigurateColors();
                }
                if (showApplyAll && GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.apply_all")) &&
                    i18n.DisplayLocalizedDialog2("JLChnToZ.VRC.VVMW.Designer.ColorConfig.apply_all")) {
                    var colorConfigs = FindObjectsOfType<ColorConfig>();
                    foreach (var colorConfig in colorConfigs) {
                        if (colorConfig != target)
                            using (var so = new SerializedObject(colorConfig)) {
                                so.CopyFromSerializedPropertyIfDifferent(colorsProperty);
                                so.ApplyModifiedProperties();
                            }
                        colorConfig.ConfigurateColors();
                    }
                }
            }
        }

        void DrawChromaEditor(int count) {
            if (colorsProperty.hasMultipleDifferentValues) return;
            using (PooledObjectExtensions.Get(out List<Vector4> chromas, colorsProperty.arraySize)) {
                var averageChroma = Vector4.zero;
                for (int i = 0; i < count; i++) {
                    using var colorProperty = colorsProperty.GetArrayElementAtIndex(i);
                    if (colorProperty.hasMultipleDifferentValues) return;
                    var color = colorProperty.colorValue;
                    var chroma = Color2Chroma(color);
                    chromas.Add(chroma);
                    averageChroma += chroma;
                }
                averageChroma /= count;
                averageChroma.z = 1;
                averageChroma.w = 1;
                var averageColor = Chroma2Color(averageChroma);
                using var changed = new EditorGUI.ChangeCheckScope();
                averageColor = EditorGUILayout.ColorField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorPalette"), averageColor, true, false, false);
                if (!changed.changed) return;
                var deltaChroma = Color2Chroma(averageColor) - averageChroma;
                deltaChroma.z = 0;
                deltaChroma.w = 0;
                for (int i = 0; i < count; i++) {
                    using var colorProperty = colorsProperty.GetArrayElementAtIndex(i);
                    var chroma = chromas[i];
                    chroma += deltaChroma;
                    colorProperty.colorValue = Chroma2Color(chroma);
                }
            }
        }

        public override void DrawInspectorGUI() {
            serializedObject.Update();
            showApplyAll = true;
            DrawEmbeddedInspectorGUI();
            showApplyAll = false;
            EditorGUILayout.Space();
            addRemoveFoldout = EditorGUILayout.Foldout(addRemoveFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.advanced"));
            if (addRemoveFoldout) {
                var count = colorsProperty.arraySize;
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.addPalette"))) {
                        colorsProperty.InsertArrayElementAtIndex(count);
                        using var colorProperty = colorsProperty.GetArrayElementAtIndex(count);
                        colorProperty.colorValue = Color.white;
                        count++;
                    }
                    using (new EditorGUI.DisabledScope(count <= 0))
                        if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.removePalette")))
                            FUtils.DeleteElement(colorsProperty, colorsProperty.arraySize - 1);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        [InitializeOnLoadMethod]
        static void OnInitialize() {
            SceneManager.sceneLoaded += OnSceneLoaded;
            AutoConfigurate();
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) {
            if (!Application.isPlaying) AutoConfigurate(scene);
        }

        static void AutoConfigurate() {
#if UNITY_2022_2_OR_NEWER
            int count = SceneManager.loadedSceneCount;
#else
            int count = SceneManager.sceneCount;
#endif
            for (int i = 0; i < count; i++) AutoConfigurate(SceneManager.GetSceneAt(i));
        }

        static void AutoConfigurate(Scene scene) {
            foreach (var colorConfig in scene.IterateAllComponents<ColorConfig>(true))
                colorConfig.CheckAndConfigurateColors();
            Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
        }

        sealed class ImportPostprocessor : AssetPostprocessor {
            static void OnPostprocessAllAssets(
                string[] importedAssets,
                string[] deletedAssets,
                string[] movedAssets,
                string[] movedFromAssetPaths
            ) => AutoConfigurate();
        }
    }
}