using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Editors;
using FUtils = JLChnToZ.VRC.Foundation.Editors.Utils;

namespace JLChnToZ.VRC.VVMW.Designer {
    [CustomEditor(typeof(ColorConfig))]
    public class ColorConfigEditor : VVMWEditorBase {
        SerializedProperty colorsProperty;
        bool addRemoveFoldout;

        protected override void OnEnable() {
            base.OnEnable();
            colorsProperty = serializedObject.FindProperty("colors");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorPalette"), EditorStyles.boldLabel);
            for (int i = 0; i < colorsProperty.arraySize; i++) {
                var colorProperty = colorsProperty.GetArrayElementAtIndex(i);
                EditorGUILayout.PropertyField(colorProperty, new GUIContent(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorN", i + 1)));
            }
            addRemoveFoldout = EditorGUILayout.Foldout(addRemoveFoldout, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.advanced"));
            if (addRemoveFoldout)
                using (new EditorGUILayout.HorizontalScope()) {
                    if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.addPalette"))) {
                        colorsProperty.InsertArrayElementAtIndex(colorsProperty.arraySize);
                        var colorProperty = colorsProperty.GetArrayElementAtIndex(colorsProperty.arraySize - 1);
                        colorProperty.colorValue = Color.white;
                    }
                    using (new EditorGUI.DisabledScope(colorsProperty.arraySize <= 0))
                        if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.removePalette")))
                            FUtils.DeleteElement(colorsProperty, colorsProperty.arraySize - 1);
                }
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.apply")))
                    (target as ColorConfig).ConfigurateColors();
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.apply_all")) &&
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