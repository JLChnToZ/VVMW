using System;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class ReorderableListUtils {
        delegate FieldInfo GetFieldInfoAndStaticTypeFromPropertyDelegate(SerializedProperty property, out Type type);
        delegate void ShowObjectPickerDelegate(UnityObject obj, bool allowSceneObjects, string searchFilter, int controlID);
        public delegate void HeaderCallbackDelegate(ref Rect rect);
        public delegate void ElementCallbackDelegate(ref Rect rect, int index, bool isActive, bool isFocused);
        public delegate void ElementHeightCallbackDelegate(ref float height, int index);

        static GetFieldInfoAndStaticTypeFromPropertyDelegate GetFieldInfoAndStaticTypeFromProperty;
        public readonly SerializedProperty property;
        readonly ReorderableList list;
        static GUIContent titleContent;
        ShowObjectPickerDelegate showObjectPicker;
        int controlId, objectPickerIndex;

        public HeaderCallbackDelegate DrawHeaderCallback { get; set; }

        public ElementCallbackDelegate DrawElementCallback { get; set; }

        public ElementHeightCallbackDelegate ElementHeightCallback { get; set; }

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

        void DrawHeader(Rect rect) {
            DrawHeaderCallback?.Invoke(ref rect);
            if (rect.width <= 0 || rect.height <= 0) return;
            if (titleContent == null) titleContent = new GUIContent();
            titleContent.text = property.displayName;
            titleContent.tooltip = property.tooltip;
            using (new EditorGUI.PropertyScope(rect, GUIContent.none, property))
                EditorGUI.LabelField(rect, titleContent);
        }

        public void Draw(Rect rect) {
            controlId = GUIUtility.GetControlID(FocusType.Passive);
            list.DoList(rect);
            HandleObjectPicker();
        }

        public void Draw() {
            controlId = GUIUtility.GetControlID(FocusType.Passive);
            list.DoLayoutList();
            HandleObjectPicker();
        }

        float GetElementHeight(int index) {
            var element = property.GetArrayElementAtIndex(index);
            var height = EditorGUI.GetPropertyHeight(element);
            ElementHeightCallback?.Invoke(ref height, index);
            return height + EditorGUIUtility.standardVerticalSpacing;
        }

        void DrawElement(Rect rect, int index, bool isActive, bool isFocused) {
            DrawElementCallback?.Invoke(ref rect, index, isActive, isFocused);
            if (rect.width <= 0 || rect.height <= 0) return;
            rect.height -= EditorGUIUtility.standardVerticalSpacing;
            var element = property.GetArrayElementAtIndex(index);
            EditorGUI.PropertyField(rect, element, GUIContent.none);
        }

        void OnAdd(ReorderableList list) {
            property.InsertArrayElementAtIndex(property.arraySize);
            objectPickerIndex = property.arraySize - 1;
            var element = property.GetArrayElementAtIndex(objectPickerIndex);
            switch (element.propertyType) {
                case SerializedPropertyType.ObjectReference:
                    element.objectReferenceValue = null;
                    ShowObjectPicker(element);
                    break;
            }
        }

        void ShowObjectPicker(SerializedProperty element) {
            // We cache the methods into delegates to avoid reflection every time
            if (showObjectPicker == null) {
                // GetFieldInfoAndStaticTypeFromProperty is an internal method in an internal class
                if (GetFieldInfoAndStaticTypeFromProperty == null)
                    GetFieldInfoAndStaticTypeFromProperty = Type.GetType("UnityEditor.ScriptAttributeUtility, UnityEditor")
                        .GetMethod("GetFieldInfoAndStaticTypeFromProperty", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .ToDelegate<GetFieldInfoAndStaticTypeFromPropertyDelegate>();
                GetFieldInfoAndStaticTypeFromProperty(element, out Type type);
                // ShowObjectPicker is a generic method and the type can't be inferred until we get the type from GetFieldInfoAndStaticTypeFromProperty
                showObjectPicker = typeof(EditorGUIUtility)
                    .GetMethod("ShowObjectPicker", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .MakeGenericMethod(type)
                    .ToDelegate<ShowObjectPickerDelegate>();
            }
            showObjectPicker(element.objectReferenceValue, true, "", controlId);
        }

        void HandleObjectPicker() {
            if (showObjectPicker == null) return; // No object picker
            var e = Event.current;
            if (e.type == EventType.ExecuteCommand) {
                int objectPickerIndex = this.objectPickerIndex;
                switch (e.commandName) {
                    case "ObjectSelectorClosed":
                        if (EditorGUIUtility.GetObjectPickerControlID() != controlId) return;
                        this.objectPickerIndex = -1;
                        break;
                    case "ObjectSelectorUpdated":
                        if (EditorGUIUtility.GetObjectPickerControlID() != controlId) return;
                        break;
                    default: return;
                }
                var element = property.GetArrayElementAtIndex(objectPickerIndex);
                element.objectReferenceValue = EditorGUIUtility.GetObjectPickerObject();
                e.Use();
            }
        }

        void OnRemove(ReorderableList list) {
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