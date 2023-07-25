using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

namespace JLChnToZ {
    public class ReorderableListUtils {
        public readonly SerializedProperty property;
        public readonly ReorderableList list;
        static GUIContent titleContent;

        public ReorderableListUtils(SerializedProperty property) {
            this.property = property;
            list = new ReorderableList(property.serializedObject, property, true, true, true, true) {
                drawHeaderCallback = DrawHeader,
                drawElementCallback = DrawElement,
                elementHeightCallback = GetElementHeight,
                onAddCallback = OnAdd,
                onRemoveCallback = OnRemove,
            };
        }

        public void DrawHeader(Rect rect) {
            if (titleContent == null) titleContent = new GUIContent();
            titleContent.text = property.displayName;
            titleContent.tooltip = property.tooltip;
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
                EditorGUI.LabelField(rect, titleContent);
        }

        public float GetElementHeight(int index) {
            var element = property.GetArrayElementAtIndex(index);
            return EditorGUI.GetPropertyHeight(element) + EditorGUIUtility.standardVerticalSpacing;
        }

        public void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
            rect.height -= EditorGUIUtility.standardVerticalSpacing;
            var element = property.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        }

        public void OnAdd(ReorderableList list) {
            property.InsertArrayElementAtIndex(property.arraySize);
        }

        public void OnRemove(ReorderableList list) {
            var arraySize = property.arraySize;
            property.DeleteArrayElementAtIndex(list.index);
            #if !UNITY_2021_2_OR_NEWER
            if (property.arraySize == arraySize)
                // Need to call twice to remove the empty space
                property.DeleteArrayElementAtIndex(list.index);
            #endif
        }
    }
}