using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace JLChnToZ.VRC.VVMW.Editors {
    
    [CustomEditor(typeof(OverlayControl))]
    public class OverlayControlEditor : VVMWEditorBase {

        BoxBoundsHandle boundsHandle;

        protected override void OnEnable() {
            base.OnEnable();
            CoreBoundsDataEditor.activeBoundsPropPath = null;
            boundsHandle = new BoxBoundsHandle();
        }

        void OnSceneGUI() {
            var activeBoundsPropPath = CoreBoundsDataEditor.activeBoundsPropPath;
            serializedObject.Update();
            if (!string.IsNullOrEmpty(activeBoundsPropPath)) {
                var coreBoundsProp = serializedObject.FindProperty(activeBoundsPropPath);
                if (coreBoundsProp != null) {
                    var referenceTransformProp = coreBoundsProp.FindPropertyRelative("referenceTransform");
                    var boundsPropRect = coreBoundsProp.FindPropertyRelative("bounds");
                    if (referenceTransformProp != null && boundsPropRect != null) {
                        var referenceTransform = referenceTransformProp.objectReferenceValue as Transform;
                        var bounds = boundsPropRect.boundsValue;
                        boundsHandle.center = bounds.center;
                        boundsHandle.size = bounds.size;
                        using (var changed = new EditorGUI.ChangeCheckScope())
                        using (new Handles.DrawingScope(referenceTransform != null ? referenceTransform.localToWorldMatrix : Matrix4x4.identity)) {
                            boundsHandle.DrawHandle();
                            if (changed.changed)
                                boundsPropRect.boundsValue = new Bounds(boundsHandle.center, boundsHandle.size);
                        }
                    }
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }

    [CustomPropertyDrawer(typeof(OverlayControl.CoreBoundsData))]
    public class CoreBoundsDataEditor : PropertyDrawer {
        public static string activeBoundsPropPath;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var rect = position;
            rect.xMax -= 30;
            EditorGUI.PropertyField(rect, property, property.isExpanded);
            var buttonRect = position;
            buttonRect.xMin = rect.xMax + 5;
            buttonRect.height = 20;
            bool selected = activeBoundsPropPath == property.propertyPath;
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                selected = GUI.Toggle(buttonRect, selected, EditorGUIUtility.IconContent("EditCollider"), EditorStyles.miniButton);
                if (changed.changed) {
                    activeBoundsPropPath = selected ? property.propertyPath : null;
                    SceneView.RepaintAll();
                }
            }
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            EditorGUI.GetPropertyHeight(property) + EditorGUIUtility.standardVerticalSpacing;
    }
}