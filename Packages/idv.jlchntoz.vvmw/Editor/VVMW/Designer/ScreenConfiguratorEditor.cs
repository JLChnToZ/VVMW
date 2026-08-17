using System;
using UnityEngine;
using UnityEditor;
using JLChnToZ.VRC.VVMW.Editors;
using JLChnToZ.VRC.Foundation.I18N.Editors;
#if VRCLV3_EARLY_VERSION
using VRCLightVolumes;
#elif VRCLV3_IMPORTED
using PointLightVolume = VRCLightVolumes.PointLightVolumeInstance;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [CustomEditor(typeof(ScreenConfigurator))]
    public class ScreenConfiguratorEditor : VVMWEditorBase {
        SerializedObject coreEditorObject;
        SerializedProperty coreProperty;
        SerializedProperty screenRendererProperty, coreScreenRendererProperty;
        SerializedProperty targetModeProperty, coreTargetModeProperty;
        SerializedProperty targetIndexProperty, coreTargetIndexProperty;
        SerializedProperty targetPropertyNameProperty, coreTargetPropertyNameProperty;
        SerializedProperty avProPropertyNameProperty, coreAvProPropertyNameProperty;
        SerializedProperty defaultTextureProperty, coreDefaultTextureProperty;
#if VRCLV3_IMPORTED
        SerializedProperty pointLightVolumeProperty;
#endif
        int lastIndex = -1;

        protected override void OnEnable() {
            base.OnEnable();
            coreProperty = serializedObject.FindProperty("core");
            screenRendererProperty = serializedObject.FindProperty("screenRenderer");
            targetModeProperty = serializedObject.FindProperty("targetMode");
            targetIndexProperty = serializedObject.FindProperty("targetIndex");
            targetPropertyNameProperty = serializedObject.FindProperty("targetPropertyName");
            avProPropertyNameProperty = serializedObject.FindProperty("avProPropertyName");
            defaultTextureProperty = serializedObject.FindProperty("defaultTexture");
#if VRCLV3_IMPORTED
            pointLightVolumeProperty = serializedObject.FindProperty("pointLightVolume");
#endif
        }

        protected override void OnDisable() {
            base.OnDisable();
            if (coreEditorObject != null) {
                coreEditorObject.Dispose();
                coreEditorObject = null;
            }
        }

        public override void DrawEmbeddedInspectorGUI() {
            var target = this.target as ScreenConfigurator;
            if (GetFirstVizVidComponent(out var first) || first == null)
                using (var changeCheck = new EditorGUI.ChangeCheckScope()) {
                    EditorGUILayout.PropertyField(coreProperty);
                    if (changeCheck.changed) serializedObject.ApplyModifiedProperties(); // Triggers OnValidate
                }
            else if (first.Core != coreProperty.objectReferenceValue) {
                coreProperty.objectReferenceValue = first.Core;
                serializedObject.ApplyModifiedProperties();
            }
            var core = coreProperty.objectReferenceValue as Core;
            bool fallback = false;
            if (target.IsPrefabEditingMode || core == null) {
                fallback = true;
            } else {
                if (coreEditorObject == null || coreEditorObject.targetObject != core) {
                    if (coreEditorObject != null) {
                        coreEditorObject.Dispose();
                        coreEditorObject = null;
                    }
                    coreEditorObject = new SerializedObject(core);
                    lastIndex = -1;
                }
                int index = Array.IndexOf(core.screenTargets, target.Renderer);
                if (lastIndex != index) {
                    lastIndex = index;
                    UpdateCoreProperties(index);
                }
                if (index < 0) fallback = true;
                else {
                    coreEditorObject.Update();
                    DrawCoreProperties(
                        screenRendererProperty,
                        coreTargetModeProperty,
                        coreTargetIndexProperty,
                        coreTargetPropertyNameProperty,
                        coreAvProPropertyNameProperty,
                        coreDefaultTextureProperty
                    );
                    if (screenRendererProperty.objectReferenceValue != coreScreenRendererProperty.objectReferenceValue)
                        coreScreenRendererProperty.objectReferenceValue = screenRendererProperty.objectReferenceValue;
                    coreEditorObject.ApplyModifiedProperties();
                }
            }
            if (fallback)
                DrawCoreProperties(
                    screenRendererProperty,
                    targetModeProperty,
                    targetIndexProperty,
                    targetPropertyNameProperty,
                    avProPropertyNameProperty,
                    defaultTextureProperty
                );
            DrawExtraProperties();
        }

        public void DrawExtraProperties() {
            var target = this.target as ScreenConfigurator;
#if VRCLV3_IMPORTED
            using (var changeCheck = new EditorGUI.ChangeCheckScope())
            using (new EditorGUILayout.HorizontalScope()) {
                EditorGUILayout.PropertyField(pointLightVolumeProperty);
                var lv = pointLightVolumeProperty.objectReferenceValue as PointLightVolume;
                if (lv == null) {
                    if (GUILayout.Button(i18n.GetLocalizedContent("ScreenConfigurator.CreateLightVolume"), GUILayout.ExpandWidth(false)))
                        target.CreateLightVolume();
                } else if (GUILayout.Button(i18n.GetLocalizedContent("ScreenConfigurator.FixupLightVolume"), GUILayout.ExpandWidth(false)) || changeCheck.changed) {
                    var renderer = screenRendererProperty.objectReferenceValue;
                    ScreenConfigurator.EnsureLightVolumeCookie(lv, renderer != null ? renderer.name : target.name);
                }
            }
#endif
            if (GUILayout.Button(i18n.GetLocalizedContent("ScreenConfigurator.FixupAspectRatio"))) {
                var meshRenderer = screenRendererProperty.objectReferenceValue as MeshRenderer;
                ScreenMeshUtils.TryFixupAspectRatioInMaterial(meshRenderer);
#if VRCLV3_IMPORTED
                var lv = pointLightVolumeProperty.objectReferenceValue as PointLightVolume;
                if (lv != null)
                    ScreenConfigurator.TryEstimatePlacement(lv.transform, targetIndexProperty.intValue, true);
#endif
            }
        }

        void DrawCoreProperties(
            SerializedProperty screenRendererProperty,
            SerializedProperty targetModeProperty,
            SerializedProperty targetIndexProperty,
            SerializedProperty targetPropertyNameProperty,
            SerializedProperty avProPropertyNameProperty,
            SerializedProperty defaultTextureProperty
        ) {
            EditorGUILayout.ObjectField(screenRendererProperty, typeof(Renderer));
            var renderer = screenRendererProperty.objectReferenceValue as Renderer;
            if (!renderer) return;
            CoreEditor.ParseScreenMode(targetModeProperty, out int mode, out bool useST, out int blitFlags);
            CoreEditor.DrawScreenRendererOptions(
                targetIndexProperty, renderer,
                ref mode, out var shader, out var materials
            );
            CoreEditor.DrawScreenMaterialOptions(
                screenRendererProperty,
                targetPropertyNameProperty,
                avProPropertyNameProperty,
                ref useST, shader, materials
            );
            if (CoreEditor.DrawScreenTextureOptions(defaultTextureProperty))
                ScreenConfigurator.NotifyDefaultTextureChanged(renderer, targetIndexProperty.intValue);
            CoreEditor.SetScreenMode(targetModeProperty, mode, useST, blitFlags);
        }

        void UpdateCoreProperties(int index) {
            if (coreEditorObject == null || index < 0) {
                coreScreenRendererProperty = null;
                coreTargetModeProperty = null;
                coreTargetIndexProperty = null;
                coreTargetPropertyNameProperty = null;
                coreAvProPropertyNameProperty = null;
                coreDefaultTextureProperty = null;
            } else {
                coreScreenRendererProperty = coreEditorObject.FindProperty("screenTargets").GetArrayElementAtIndex(index);
                coreTargetModeProperty = coreEditorObject.FindProperty("screenTargetModes").GetArrayElementAtIndex(index);
                coreTargetIndexProperty = coreEditorObject.FindProperty("screenTargetIndeces").GetArrayElementAtIndex(index);
                coreTargetPropertyNameProperty = coreEditorObject.FindProperty("screenTargetPropertyNames").GetArrayElementAtIndex(index);
                coreAvProPropertyNameProperty = coreEditorObject.FindProperty("avProPropertyNames").GetArrayElementAtIndex(index);
                coreDefaultTextureProperty = coreEditorObject.FindProperty("screenTargetDefaultTextures").GetArrayElementAtIndex(index);
            }
        }
    }
}