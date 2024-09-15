using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;

namespace JLChnToZ.VRC.VVMW {
    public static class Utils {
        static GUIStyle textFieldDropDownTextStyle, textFieldDropDownStyle;

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
    }
}