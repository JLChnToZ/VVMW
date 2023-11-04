using System;
using UnityEngine;
using UnityEditor;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW {
    using static LocatableAttribute;
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
            if (attribute == null) return Utils.FindClosestComponentInHierarchy(startFrom.transform, fieldInfo.FieldType);
            if (attribute.TypeNames == null || attribute.TypeNames.Length == 0) {
                var result = Utils.FindClosestComponentInHierarchy(startFrom.transform, fieldInfo.FieldType);
                if (result != null) return result;
            } else foreach (var typeName in attribute.TypeNames) {
                var type = Type.GetType(typeName, false);
                if (type != null) {
                    var result = Utils.FindClosestComponentInHierarchy(startFrom.transform, type);
                    if (result != null) return result;
                }
            }
            if (!string.IsNullOrEmpty(attribute.InstaniatePrefabGuid)) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(attribute.InstaniatePrefabGuid));
                if (prefab == null) {
                    Debug.LogWarning($"Cannot find prefab with GUID {attribute.InstaniatePrefabGuid}");
                    return null;
                }
                var result = InstaniatePrefab(prefab, startFrom, attribute.InstaniatePrefabPosition, attribute.TypeNames);
                if (result != null) return result;
            } else if (!string.IsNullOrEmpty(attribute.InstaniatePrefabPath)) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(attribute.InstaniatePrefabPath);
                if (prefab == null) {
                    Debug.LogWarning($"Cannot find prefab at path {attribute.InstaniatePrefabPath}");
                    return null;
                }
                var result = InstaniatePrefab(prefab, startFrom, attribute.InstaniatePrefabPosition, attribute.TypeNames);
                if (result != null) return result;
            }
            Debug.LogWarning($"Cannot find any component of type {string.Join(", ", attribute.TypeNames)}");
            return null;
        }

        Component InstaniatePrefab(GameObject prefab, Component current, InstaniatePrefabHierachyPosition spawnPosition, string[] searchTypeNames) {
            var result = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (result == null) return null;
            Undo.RegisterCreatedObjectUndo(result, $"Instaniate {prefab.name}");
            var transform = PrefabUtility.GetOutermostPrefabInstanceRoot(result).transform;
            int currentSiblingIndex = 0;
            if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.Child)) {
                transform.SetParent(current.transform, false);
                currentSiblingIndex = current.transform.childCount - 1;
            } else if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.SameLevel)) {
                transform.SetParent(current.transform.parent, false);
                currentSiblingIndex = current.transform.GetSiblingIndex();
            } else for (var parent = current.transform; parent != null; parent = parent.parent) {
                currentSiblingIndex = parent.GetSiblingIndex();
            }
            if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.First))
                transform.SetAsFirstSibling();
            else if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.Last))
                transform.SetAsLastSibling();
            else if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.Before))
                transform.SetSiblingIndex(currentSiblingIndex);
            else if (spawnPosition.HasFlag(InstaniatePrefabHierachyPosition.After))
                transform.SetSiblingIndex(currentSiblingIndex + 1);
            var targetComponent = result.GetComponentInChildren(fieldInfo.FieldType);
            if (targetComponent == null && searchTypeNames != null)
                foreach (var typeName in searchTypeNames) {
                    var type = Type.GetType(typeName, false);
                    if (type != null) {
                        targetComponent = result.GetComponentInChildren(type);
                        if (targetComponent != null) break;
                    }
                }
            return targetComponent;
        }
    }
}