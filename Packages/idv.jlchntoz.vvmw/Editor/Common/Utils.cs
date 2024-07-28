using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEditor;

namespace JLChnToZ.VRC.VVMW {
    public static class Utils {
        static GUIContent tempContent;
        static GUIStyle textFieldDropDownTextStyle, textFieldDropDownStyle;
        static readonly GetFieldInfoAndStaticTypeFromPropertyDelegate getFieldInfoAndStaticTypeFromProperty = Delegate.CreateDelegate(
            typeof(GetFieldInfoAndStaticTypeFromPropertyDelegate), Type
            .GetType("UnityEditor.ScriptAttributeUtility, UnityEditor", false)?
            .GetMethod("GetFieldInfoAndStaticTypeFromProperty", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ) as GetFieldInfoAndStaticTypeFromPropertyDelegate;

        delegate FieldInfo GetFieldInfoAndStaticTypeFromPropertyDelegate(SerializedProperty property, out Type type);

        public static IEnumerable<T> IterateAllComponents<T>(this Scene scene, bool includeEditorOnly = false) where T : Component {
            var pending = new Stack<Transform>();
            var components = new List<T>();
            var rootGameObjects = scene.GetRootGameObjects();
            for (int i = rootGameObjects.Length - 1; i >= 0; i--) pending.Push(rootGameObjects[i].transform);
            while (pending.Count > 0) {
                var transform = pending.Pop();
                if (transform == null || (!includeEditorOnly && transform.tag == "EditorOnly")) continue;
                for (int i = transform.childCount - 1; i >= 0; i--) pending.Push(transform.GetChild(i));
                components.Clear();
                transform.GetComponents(components);
                foreach (var component in components) if (component != null) yield return component;
            }
        }

        public static T FindClosestComponentInHierarchy<T>(Transform startFrom, GameObject[] roots = null) where T : Component =>
            FindClosestComponentInHierarchy(startFrom, typeof(T), roots) as T;

        public static Component FindClosestComponentInHierarchy(Transform startFrom, Type type, GameObject[] roots = null) {
            for (Transform transform = startFrom, lastTransform = null; transform != null; transform = transform.parent) {
                if (transform.TryGetComponent(type, out var result)) return result;
                foreach (Transform child in transform) {
                    if (lastTransform == child) continue;
                    result = transform.GetComponentInChildren(type, true);
                    if (result != null) return result;
                }
                lastTransform = transform;
            }
            if (roots == null) {
                var scene = startFrom.gameObject.scene;
                if (!scene.IsValid()) return null;
                roots = scene.GetRootGameObjects();
            }
            foreach (var root in roots) {
                var result = root.GetComponentInChildren(type, true);
                if (result != null) return result;
            }
            return null;
        }

        public static void DeleteElement(SerializedProperty property, int index) {
            int size = property.arraySize;
            property.DeleteArrayElementAtIndex(index);
            if (size == property.arraySize) property.DeleteArrayElementAtIndex(index);
        }

        public static TDelegate ToDelegate<TDelegate>(this MethodInfo method, object target = null) where TDelegate : Delegate =>
            (TDelegate)(method.IsStatic ?
                Delegate.CreateDelegate(typeof(TDelegate), method, false) :
                Delegate.CreateDelegate(typeof(TDelegate), target, method, false)
            );

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static GUIContent GetTempContent(SerializedProperty property) =>
            GetTempContent(property.displayName, property.tooltip);

        public static GUIContent GetTempContent(string text = "", string tooltip = "", Texture2D image = null) {
            if (tempContent == null) tempContent = new GUIContent();
            tempContent.text = text;
            tempContent.tooltip = tooltip;
            tempContent.image = image;
            return tempContent;
        }

        public static void DrawShaderPropertiesField(
            SerializedProperty property,
            GUIContent label,
            Shader selectedShader,
            Material[] materials,
            ShaderUtil.ShaderPropertyType type
        ) {
            if (textFieldDropDownTextStyle == null) textFieldDropDownTextStyle = GUI.skin.FindStyle("TextFieldDropDownText");
            if (textFieldDropDownStyle == null) textFieldDropDownStyle = GUI.skin.FindStyle("TextFieldDropDown");
            var controlRect = EditorGUILayout.GetControlRect(true);
            using (new EditorGUI.PropertyScope(controlRect, label, property)) {
                controlRect = EditorGUI.PrefixLabel(controlRect, label);
                var size = textFieldDropDownStyle.CalcSize(GUIContent.none);
                var textRect = controlRect;
                textRect.xMin -= EditorGUI.indentLevel * 15F;
                textRect.xMax -= size.x;
                property.stringValue = EditorGUI.TextField(textRect, property.stringValue, textFieldDropDownTextStyle);
                var buttonRect = controlRect;
                buttonRect.xMin = buttonRect.xMax - size.x;
                buttonRect.size = size;
                using (new EditorGUI.DisabledScope(selectedShader == null && (materials == null || materials.Length == 0))) {
                    if (EditorGUI.DropdownButton(buttonRect, GUIContent.none, FocusType.Passive, textFieldDropDownStyle)) {
                        var menu = new GenericMenu();
                        if (selectedShader != null)
                            AppendShaderPropertiesToMenu(menu, selectedShader, property, type);
                        else {
                            var shaderSet = new HashSet<Shader>();
                            for (int j = 0; j < materials.Length; j++) {
                                var material = materials[j];
                                if (material == null) continue;
                                shaderSet.Add(material.shader);
                            }
                            foreach (var shader in shaderSet) {
                                if (menu.GetItemCount() > 0) menu.AddSeparator("");
                                AppendShaderPropertiesToMenu(menu, shader, property, type);
                            }
                        }
                        menu.DropDown(controlRect);
                    }
                }
            }
        }

        static void AppendShaderPropertiesToMenu(
            GenericMenu menu,
            Shader shader,
            SerializedProperty property,
            ShaderUtil.ShaderPropertyType type
        ) {
            int count = ShaderUtil.GetPropertyCount(shader);
            for (int j = 0; j < count; j++) {
                if (ShaderUtil.GetPropertyType(shader, j) != type) continue;
                var propertyName = ShaderUtil.GetPropertyName(shader, j);
                menu.AddItem(
                    new GUIContent($"{ShaderUtil.GetPropertyDescription(shader, j)} ({propertyName})".Replace('/', '.')),
                    property.stringValue == propertyName, SetValue, (property, propertyName)
                );
            }
        }

        static void SetValue(object entry) {
            (SerializedProperty prop, string value) = ((SerializedProperty, string))entry;
            prop.stringValue = value;
            prop.serializedObject.ApplyModifiedProperties();
        }

        public static string FindMainTexturePropertyName(Material material) {
            string fallback = null;
            if (material != null) {
                var shader = material.shader;
                if (shader == null) return "";
                int count = shader.GetPropertyCount();
                for (int i = 0; i < count; i++)
                    if (shader.GetPropertyType(i) == ShaderPropertyType.Texture) {
                        if (shader.GetPropertyFlags(i).HasFlag(ShaderPropertyFlags.MainTexture))
                            return shader.GetPropertyName(i);
                        if (fallback == null)
                            fallback = shader.GetPropertyName(i);
                    }
            }
            return fallback ?? "_MainTex";
        }

        public static FieldInfo GetFieldInfoFromProperty(SerializedProperty property, out Type type) {
            if (getFieldInfoAndStaticTypeFromProperty == null) {
                type = null;
                return null;
            }
            return getFieldInfoAndStaticTypeFromProperty(property, out type);
        }

#if !NETSTANDARD2_1
        // Polyfill for old .NET Framework
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Contains(this string s, string value, StringComparison comparationType) =>
            s.IndexOf(value, comparationType) >= 0;
#endif
    }
}