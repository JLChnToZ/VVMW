using System;
using UnityEngine;
using UnityEditor;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    [CustomPropertyDrawer(typeof(LocatableAttribute))]
    public class LocatableAttributeDrawer : PropertyDrawer {
        GUIContent findButtonContent = new GUIContent("Find");
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            var so = property.serializedObject;
            var targetComponent = so.targetObject as Component;
            using (new EditorGUI.PropertyScope(position, label, property))
                if (targetComponent != null &&
                    property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue == null) {
                    var buttonStyle = EditorStyles.miniButton;
                    var buttonRect = new Rect(position);
                    buttonRect.xMin = buttonRect.xMax - buttonStyle.CalcSize(findButtonContent).x;
                    var propertyRect = new Rect(position) {
                        xMax = buttonRect.xMin - 2,
                    };
                    EditorGUI.PropertyField(propertyRect, property, label);
                    using (new EditorGUI.DisabledGroupScope(so.isEditingMultipleObjects))
                        if (GUI.Button(buttonRect, findButtonContent, buttonStyle)) {
                            var result = Find(targetComponent);
                            if (result != null) {
                                EditorGUIUtility.PingObject(result);
                                property.objectReferenceValue = result;
                            }
                        }
                } else
                    EditorGUI.PropertyField(position, property, label);
        }

        UnityObject Find(Component startFrom) {
            if (startFrom == null) return null;
            var attribute = this.attribute as LocatableAttribute;
            if (attribute == null || attribute.TypeNames == null || attribute.TypeNames.Length == 0)
                return Utils.FindClosestComponentInHierarchy(startFrom.transform, fieldInfo.FieldType);
            foreach (var typeName in attribute.TypeNames) {
                var type = Type.GetType(typeName, false);
                if (type != null) {
                    var result = Utils.FindClosestComponentInHierarchy(startFrom.transform, type);
                    if (result != null) return result;
                }
            }
            Debug.LogWarning($"Cannot find any component of type {string.Join(", ", attribute.TypeNames)}");
            return null;
        }
    }
}