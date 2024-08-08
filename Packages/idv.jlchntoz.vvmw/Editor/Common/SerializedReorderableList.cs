#if !UNITY_2022_1_OR_NEWER
using System.Reflection;
#endif
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using JLChnToZ.VRC.VVMW.I18N.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class SerializedReorderableList : ReorderableList {
        static GUIContent headerContent;
#if !UNITY_2022_1_OR_NEWER
        // SerializedProperty.gradientValue is internal before Unity 2022.1
        static readonly PropertyInfo gradientValueField = typeof(SerializedProperty)
            .GetProperty("gradientValue", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
#endif
        readonly HashSet<int> expandedIndices = new HashSet<int>();

        public SerializedReorderableList(
            SerializedProperty elements
        ) : this(elements.serializedObject, elements, true, true, true, true) { }

        public SerializedReorderableList(
            SerializedObject serializedObject, SerializedProperty elements
        ) : this(serializedObject, elements, true, true, true, true) { }

        public SerializedReorderableList(
            SerializedObject serializedObject, SerializedProperty elements,
            bool draggable, bool displayHeader, bool displayAddButton, bool displayRemoveButton
        ) : base(serializedObject, elements, draggable, displayHeader, displayAddButton, displayRemoveButton) {
            drawHeaderCallback = DrawHeader;
            drawElementCallback = DrawElement;
            elementHeightCallback = GetElementHeight;
            onAddCallback = OnAdd;
            onRemoveCallback = OnRemove;
            onReorderCallbackWithDetails = OnReorder;
        }

        void DrawHeader(Rect rect) {
            if (headerContent == null) headerContent = new GUIContent();
            headerContent.text = serializedProperty.displayName;
            headerContent.tooltip = serializedProperty.tooltip;
            LocalizedLabelAttributeDrawer.Resolve(null, serializedProperty, headerContent);
            EditorGUI.LabelField(rect, headerContent, EditorStyles.boldLabel);
        }

        float GetElementHeight(int index) =>
            EditorGUI.GetPropertyHeight(serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, expandedIndices.Contains(index));

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
            if (EditorGUI.PropertyField(rect, serializedProperty.GetArrayElementAtIndex(index), GUIContent.none, expandedIndices.Contains(index)) &&
                !expandedIndices.Add(index)) expandedIndices.Remove(index);
        }

        static void OnAdd(ReorderableList list) {
            var property = list.serializedProperty;
            int index = list.index + 1, count = property.arraySize;
            if (index < 0 || index > count) index = count;
            property.InsertArrayElementAtIndex(index);
            var element = property.GetArrayElementAtIndex(index);
            int depth = element.depth;
            do {
                if (element.isArray) {
                    element.ClearArray();
                    continue;
                }
                switch (element.propertyType) {
                    case SerializedPropertyType.Integer:
                    case SerializedPropertyType.LayerMask:
                    case SerializedPropertyType.ArraySize:
                    case SerializedPropertyType.Character: element.intValue = default; break;
                    case SerializedPropertyType.Boolean: element.boolValue = default; break;
                    case SerializedPropertyType.Float: element.floatValue = default; break;
                    case SerializedPropertyType.String: element.stringValue = ""; break;
                    case SerializedPropertyType.Color: element.colorValue = Color.black; break;
                    case SerializedPropertyType.ObjectReference: element.objectReferenceValue = default; break;
                    case SerializedPropertyType.Enum: element.enumValueIndex = default; break;
                    case SerializedPropertyType.Vector2: element.vector2Value = default; break;
                    case SerializedPropertyType.Vector3: element.vector3Value = default; break;
                    case SerializedPropertyType.Vector4: element.vector4Value = default; break;
                    case SerializedPropertyType.Rect: element.rectValue = default; break;
                    case SerializedPropertyType.AnimationCurve: element.animationCurveValue = default; break;
                    case SerializedPropertyType.Bounds: element.boundsValue = default; break;
                    case SerializedPropertyType.Gradient:
#if UNITY_2022_1_OR_NEWER
                        element.gradientValue = default;
#else
                        gradientValueField.SetValue(element, default);
#endif
                        break;
                    case SerializedPropertyType.Quaternion: element.quaternionValue = Quaternion.identity; break;
                    case SerializedPropertyType.ExposedReference: element.exposedReferenceValue = default; break;
                    case SerializedPropertyType.Vector2Int: element.vector2IntValue = default; break;
                    case SerializedPropertyType.Vector3Int: element.vector3IntValue = default; break;
                    case SerializedPropertyType.RectInt: element.rectIntValue = default; break;
                    case SerializedPropertyType.BoundsInt: element.boundsIntValue = default; break;
#if UNITY_2019_3_OR_NEWER
                    case SerializedPropertyType.ManagedReference: element.managedReferenceValue = default; break;
#endif
#if UNITY_2021_1_OR_NEWER
                    case SerializedPropertyType.Hash128: element.hash128Value = default; break;
#endif
                }
            } while (element.Next(element.propertyType == SerializedPropertyType.Generic) && element.depth > depth);
            list.index = index;
        }

        static void OnRemove(ReorderableList list) {
            var property = list.serializedProperty;
            int index = list.index, count = property.arraySize;
            if (index < 0 || index >= count) index = count - 1;
            property.DeleteArrayElementAtIndex(index);
            if (property.arraySize == count) property.DeleteArrayElementAtIndex(index);
            if (list.index == index) list.index--;
            if (list is SerializedReorderableList srl) {
                var expandedIndices = srl.expandedIndices;
                expandedIndices.Remove(index);
                for (int i = index; i < count; i++)
                    if (expandedIndices.Remove(i + 1))
                        expandedIndices.Add(i);
            }
        }

        static void OnReorder(ReorderableList list, int oldIndex, int newIndex) {
            if (!(list is SerializedReorderableList srl)) return;
            var expandedIndices = srl.expandedIndices;
            if (oldIndex < newIndex) {
                for (int i = oldIndex; i < newIndex; i++)
                    if (expandedIndices.Remove(i))
                        expandedIndices.Add(i + 1);
            } else if (oldIndex > newIndex) {
                for (int i = newIndex + 1; i <= oldIndex; i++)
                    if (expandedIndices.Remove(i))
                        expandedIndices.Add(i - 1);
            } else return;
            if (expandedIndices.Remove(newIndex))
                expandedIndices.Add(oldIndex);
        }
    }
}
