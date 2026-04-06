using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(ActiveRegionConfig))]
    public class ActiveRegionConfigEditor : VVMWEditorBase {
        BoxBoundsHandle boundsHandle;
        SerializedProperty boundsProp;
        SerializedProperty staticRegionProp;

        protected override void OnEnable() {
            base.OnEnable();
            boundsHandle = new BoxBoundsHandle();
            boundsProp = serializedObject.FindProperty(nameof(ActiveRegionConfig.bounds));
            staticRegionProp = serializedObject.FindProperty(nameof(ActiveRegionConfig.staticRegion));
        }

        public override void DrawInspectorGUI() {
            EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.ActiveRegionConfig.message"), MessageType.Info);
            EditorGUILayout.Space();
            base.DrawInspectorGUI();
        }

        void OnSceneGUI() {
            serializedObject.Update();
            var transform = (target as ActiveRegionConfig).transform;
            var bounds = boundsProp.boundsValue;
            boundsHandle.center = bounds.center;
            boundsHandle.size = bounds.size;
            using (var changed = new EditorGUI.ChangeCheckScope())
            using (new Handles.DrawingScope(staticRegionProp.boolValue ? Matrix4x4.identity : transform.localToWorldMatrix)) {
                boundsHandle.DrawHandle();
                if (changed.changed)
                    boundsProp.boundsValue = new Bounds(boundsHandle.center, boundsHandle.size);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
