using UnityEngine;
using UnityEditor;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    [CustomPropertyDrawer(typeof(LocalizedLabelAttribute))]
    public class LocalizedLabelAttributeDrawer : PropertyDrawer {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            Resolve(property, label);
            EditorGUI.PropertyField(position, property, label);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) {
            Resolve(property, label);
            return EditorGUI.GetPropertyHeight(property, label);
        }

        void Resolve(SerializedProperty property, GUIContent label) =>
            Resolve(attribute as LocalizedLabelAttribute, property, label);

        
        public static GUIContent Resolve(SerializedProperty property) {
            var label = Utils.GetTempContent(property.displayName, property.tooltip);
            Resolve(null, property, label);
            return label;
        }

        public static void Resolve(LocalizedLabelAttribute attr, SerializedProperty property, GUIContent label) {
            var i18n = EditorI18N.Instance;
            var key = attr?.Key;
            var field = Utils.GetFieldInfoFromProperty(property, out var _);
            if (string.IsNullOrEmpty(key)) key = $"{field.DeclaringType}.{property.propertyPath}";
            var value = i18n[key];
            if (!string.IsNullOrEmpty(value)) label.text = value;
            var tooltipKey = attr?.TooltipKey;
            if (string.IsNullOrEmpty(tooltipKey)) tooltipKey = $"{key}:tooltip";
            value = i18n[tooltipKey];
            if (!string.IsNullOrEmpty(value)) label.tooltip = value;
        }
    }

    [CustomPropertyDrawer(typeof(LocalizedHeaderAttribute))]
    public class LocalizedHeaderAttributeDrawer : DecoratorDrawer {
        public override void OnGUI(Rect position) {
            var attr = attribute as LocalizedHeaderAttribute;
            var i18n = EditorI18N.Instance;
            var key = attr.Key;
            var value = i18n[key];
            if (string.IsNullOrEmpty(value)) value = $"<Unlocalized: {key}>";
            position.height = EditorGUIUtility.singleLineHeight;
            position.y += EditorGUIUtility.singleLineHeight;
            GUI.Label(position, value, EditorStyles.boldLabel);
        }

        public override float GetHeight() => EditorGUIUtility.singleLineHeight * 2;
    }
}