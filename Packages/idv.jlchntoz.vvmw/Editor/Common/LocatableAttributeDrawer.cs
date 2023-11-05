using System;
using System.Reflection;
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
                            var result = Locate(targetComponent, fieldInfo.FieldType, attribute as LocatableAttribute, true);
                            if (result != null) {
                                EditorGUIUtility.PingObject(result);
                                property.objectReferenceValue = result;
                            }
                        }
                } else
                    EditorGUI.PropertyField(position, property, label);
        }

        public static UnityObject Locate(Component target, FieldInfo fieldInfo, bool createPrefab = true, bool resolve = false) {
            var result = Locate(target, fieldInfo.FieldType, fieldInfo.GetCustomAttribute<LocatableAttribute>(), createPrefab);
            if (resolve && result != null && fieldInfo.GetValue(target) as UnityObject == null) {
                fieldInfo.SetValue(target, result);
                if (PrefabUtility.IsPartOfPrefabInstance(target))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                else
                    EditorUtility.SetDirty(target);
            }
            return result;
        }

        static UnityObject Locate(Component target, Type fieldType, LocatableAttribute attribute, bool createPrefab) {
            if (target == null) return null;
            if (attribute == null) return Utils.FindClosestComponentInHierarchy(target.transform, fieldType);
            if (attribute.TypeNames == null || attribute.TypeNames.Length == 0) {
                var result = Utils.FindClosestComponentInHierarchy(target.transform, fieldType);
                if (result != null) return result;
            } else foreach (var typeName in attribute.TypeNames) {
                var type = Type.GetType(typeName, false);
                if (type == null) continue;
                var result = Utils.FindClosestComponentInHierarchy(target.transform, type);
                if (result != null) return result;
            }
            if (!createPrefab) return null;
            string prefabPath = null;
            if (!string.IsNullOrEmpty(attribute.InstaniatePrefabGuid)) {
                prefabPath = AssetDatabase.GUIDToAssetPath(attribute.InstaniatePrefabGuid);
                if (string.IsNullOrEmpty(prefabPath)) {
                    Debug.LogWarning($"Cannot find prefab with GUID {attribute.InstaniatePrefabGuid}");
                    return null;
                }
            } else if (!string.IsNullOrEmpty(attribute.InstaniatePrefabPath))
                prefabPath = attribute.InstaniatePrefabPath;
            if (!string.IsNullOrEmpty(prefabPath)) {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                if (prefab == null) {
                    Debug.LogWarning($"Cannot find prefab at path {prefabPath}");
                    return null;
                }
                var result = InstaniatePrefab(prefab, target, attribute.InstaniatePrefabPosition, fieldType, attribute.TypeNames);
                if (result != null) return result;
            }
            Debug.LogWarning($"Cannot find any component of type {string.Join(", ", attribute.TypeNames)}");
            return null;
        }

        static Component InstaniatePrefab(
            GameObject prefab,
            Component current,
            InstaniatePrefabHierachyPosition spawnPosition,
            Type fieldType,
            string[] searchTypeNames
        ) {
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
            if (searchTypeNames == null || searchTypeNames.Length == 0)
                return result.GetComponentInChildren(fieldType);
            foreach (var typeName in searchTypeNames) {
                var type = Type.GetType(typeName, false);
                if (type == null) continue;
                var targetComponent = result.GetComponentInChildren(type);
                if (targetComponent != null) return targetComponent;
            }
            return null;
        }
    }
}