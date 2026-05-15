using UnityEngine;
using UnityEditor;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;

namespace JLChnToZ.VRC.VVMW.Designer {
    [CustomPropertyDrawer(typeof(ColorConfigPresetAttribute))]
    public class ColorPresetPicker : PropertyDrawer {
        static EditorI18N i18n;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            if (property.propertyType != SerializedPropertyType.Integer) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            var targetObject = property.serializedObject.targetObject as MonoBehaviour;
            if (targetObject == null) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            var colorConfig = targetObject.GetComponentInParent<ColorConfig>(true);
            if (colorConfig == null) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            if (colorConfig.colors == null || colorConfig.colors.Length == 0) {
                EditorGUI.PropertyField(position, property, label);
                return;
            }
            using var propertyScope = new EditorGUI.PropertyScope(position, label, property);
            var iValue = property.intValue;
            var color = iValue >= 0 && iValue < colorConfig.colors.Length ? colorConfig.colors[iValue] : Color.white;
            position = EditorGUI.PrefixLabel(position, propertyScope.content);
            i18n ??= EditorI18N.Instance;
            var content = iValue < 0 ? i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorN", 0) : GUIContent.none;
            if (GUI.Button(position, content, EditorStyles.popup))
                PopupWindow.Show(position, new DropdownContent(colorConfig, property, position.size));
            if (iValue >= 0) {
                // Draw color preview
                position.xMax -= 24;
                position.xMin += 4;
                position.yMax -= 4;
                position.yMin += 4;
                EditorGUI.DrawRect(position, color);
            }
        }

        class DropdownContent : PopupWindowContent {
            static readonly Vector3 luminanceVector = new(0.299f, 0.587f, 0.114f);
            readonly ColorConfig colorConfig;
            readonly SerializedProperty targetProperty;
            readonly Vector2 size;
            static GUIStyle labelStyle;

            public DropdownContent(ColorConfig colorConfig, SerializedProperty targetProperty, Vector2 size) {
                this.colorConfig = colorConfig;
                this.targetProperty = targetProperty;
                this.size = size;
                labelStyle ??= new GUIStyle(EditorStyles.label);
            }

            public override Vector2 GetWindowSize() => new(
                size.x,
                (colorConfig.colors.Length + 1) * (EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing)
            );

            public override void OnGUI(Rect rect) {
                rect.height = EditorGUIUtility.singleLineHeight;
                var color = EditorGUIUtility.isProSkin ? new Color(0.3f, 0.3f, 0.3f) : new Color(0.8f, 0.8f, 0.8f);
                for (int i = -1; i < colorConfig.colors.Length; i++) {
                    if (i >= 0) EditorGUI.DrawRect(rect, color = colorConfig.colors[i]);
                    labelStyle.normal.textColor = Vector3.Dot((Vector4)color, luminanceVector) >= 0.5f ? Color.black : Color.white;
                    if (GUI.Button(rect,
                        i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.ColorConfig.colorN", i + 1),
                        labelStyle
                    )) {
                        targetProperty.intValue = i;
                        targetProperty.serializedObject.ApplyModifiedProperties();
                        editorWindow.Close();
                    }
                    rect.y += rect.height + EditorGUIUtility.standardVerticalSpacing;
                }
            }
        }
    }
}