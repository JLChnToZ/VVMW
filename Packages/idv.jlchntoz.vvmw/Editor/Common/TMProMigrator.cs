using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    public static class TMProMigratator {
        static readonly string defaultFontGuid = "f7984a9379f06e74a8d507baf148b47c";
        static readonly Dictionary<Font, string> fontGuidMapping = new Dictionary<Font, string>();
        static readonly Dictionary<string, TMP_FontAsset> fontAssetMapping = new Dictionary<string, TMP_FontAsset>();
        static readonly List<MonoBehaviour> tempMonoBehaviours = new List<MonoBehaviour>();

        [MenuItem("Tools/VizVid/Migrate TMPro Components")]
        static void MigrateSelected() {
            LoadFontMapping();
            ComponentReplacer.InitAllComponents();
            foreach (var gameObject in Selection.gameObjects)
                Migrate(gameObject);
        }
        public static void LoadFontMapping() {
            fontGuidMapping.Clear();
            fontAssetMapping.Clear();
            foreach (var guid in AssetDatabase.FindAssets("t:TMP_FontAsset", new[] { "Assets", "Packages" })) {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (fontAsset == null) continue;
                var font = fontAsset.sourceFontFile;
                if (font == null) continue;
                fontGuidMapping[font] = guid;
                fontAssetMapping[guid] = fontAsset;
            }
        }

        public static void Migrate(GameObject root) {
            var migratableTypes = new Dictionary<Type, bool>();
            var migratableFields = new Dictionary<Type, Dictionary<FieldInfo, FieldInfo>>();
            foreach (var monoBehaviour in root.GetComponentsInChildren<MonoBehaviour>(true)) {
                var type = monoBehaviour.GetType();
                if (!migratableTypes.TryGetValue(type, out var isTypeMigratable))
                    migratableTypes[type] = isTypeMigratable = type.GetCustomAttribute<TMProMigratableAttribute>() != null;
                if (!migratableFields.TryGetValue(type, out var mapping)) {
                    foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)) {
                        var attr = field.GetCustomAttribute<TMProMigratableAttribute>();
                        if (attr == null) continue;
                        if (mapping == null) mapping = new Dictionary<FieldInfo, FieldInfo>();
                        var targetField = type.GetField(attr.TMProFieldName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        if (targetField != null) mapping[field] = targetField;
                    }
                    migratableFields[type] = mapping;
                }
                if (isTypeMigratable && monoBehaviour.TryGetComponent(out Text text))
                    Migrate(text);
                if (mapping != null) {
                    foreach (var kv in mapping) {
                        var sourceField = kv.Key;
                        var targetField = kv.Value;
                        var sourceValue = sourceField.GetValue(monoBehaviour);
                        if (sourceValue is Text sourceText) {
                            var targetText = Migrate(sourceText);
                            sourceField.SetValue(monoBehaviour, null);
                            targetField.SetValue(monoBehaviour, targetText);
                        }
                    }
                    EditorUtility.SetDirty(monoBehaviour);
                }
            }
            foreach (var text in root.GetComponentsInChildren<Text>(true)) {
                var referencedComponents = ComponentReplacer.GetReferencedComponents(text);
                if (referencedComponents.Count == 0) Migrate(text);
            }
        }

        public static TextMeshProUGUI Migrate(Text textComponent) {
            if (textComponent == null) return null;
            var specialHandlingTypes = new List<MonoBehaviour>();
            textComponent.GetComponents(tempMonoBehaviours);
            GameObject tempGameObject = null;
            try {
                if (specialHandlingTypes.Count > 0) {
                    tempGameObject = Instantiate(textComponent.gameObject);
                    tempGameObject.SetActive(false);
                    tempGameObject.hideFlags = HideFlags.HideAndDontSave;
                }
                var text = textComponent.text;
                var font = textComponent.font;
                var fontSize = textComponent.fontSize;
                var fontStyle = textComponent.fontStyle;
                var alignment = textComponent.alignment;
                var color = textComponent.color;
                var lineSpacing = textComponent.lineSpacing;
                var richText = textComponent.supportRichText;
                var vertOverflow = textComponent.verticalOverflow;
                var horzOverflow = textComponent.horizontalOverflow;
                var bestFit = textComponent.resizeTextForBestFit;
                var minSize = textComponent.resizeTextMinSize;
                var maxSize = textComponent.resizeTextMaxSize;
                var raycastTarget = textComponent.raycastTarget;
                var gameObject = textComponent.gameObject;
                var tmpComponent = ComponentReplacer.TryReplaceComponent<TextMeshProUGUI>(textComponent, false);
                if (tmpComponent == null) return null;
                tmpComponent.text = text;
                if (font != null && fontGuidMapping.TryGetValue(font, out var fontGuid)) {
                    if (!fontAssetMapping.TryGetValue(fontGuid, out var fontAsset)) {
                        fontAsset = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(AssetDatabase.GUIDToAssetPath(defaultFontGuid));
                        fontAssetMapping[fontGuid] = fontAsset;
                    }
                    fontGuidMapping[font] = fontGuid;
                    tmpComponent.font = fontAsset;
                }
                tmpComponent.fontSize = fontSize;
                switch (fontStyle) {
                    case FontStyle.Normal: tmpComponent.fontStyle = FontStyles.Normal; break;
                    case FontStyle.Bold: tmpComponent.fontStyle = FontStyles.Bold; break;
                    case FontStyle.Italic: tmpComponent.fontStyle = FontStyles.Italic; break;
                    case FontStyle.BoldAndItalic: tmpComponent.fontStyle = FontStyles.Bold | FontStyles.Italic; break;
                }
                switch (alignment) {
                    case TextAnchor.UpperLeft: tmpComponent.alignment = TextAlignmentOptions.TopLeft; break;
                    case TextAnchor.UpperCenter: tmpComponent.alignment = TextAlignmentOptions.Top; break;
                    case TextAnchor.UpperRight: tmpComponent.alignment = TextAlignmentOptions.TopRight; break;
                    case TextAnchor.MiddleLeft: tmpComponent.alignment = TextAlignmentOptions.MidlineLeft; break;
                    case TextAnchor.MiddleCenter: tmpComponent.alignment = TextAlignmentOptions.Midline; break;
                    case TextAnchor.MiddleRight: tmpComponent.alignment = TextAlignmentOptions.MidlineRight; break;
                    case TextAnchor.LowerLeft: tmpComponent.alignment = TextAlignmentOptions.BottomLeft; break;
                    case TextAnchor.LowerCenter: tmpComponent.alignment = TextAlignmentOptions.Bottom; break;
                    case TextAnchor.LowerRight: tmpComponent.alignment = TextAlignmentOptions.BottomRight; break;
                }
                tmpComponent.color = color;
                tmpComponent.lineSpacing = lineSpacing;
                tmpComponent.richText = richText;
                tmpComponent.overflowMode = vertOverflow == VerticalWrapMode.Truncate ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
                tmpComponent.enableWordWrapping = horzOverflow == HorizontalWrapMode.Wrap;
                tmpComponent.enableAutoSizing = bestFit;
                tmpComponent.fontSizeMin = minSize;
                tmpComponent.fontSizeMax = maxSize;
                tmpComponent.raycastTarget = raycastTarget;
                return tmpComponent;
            } finally {
                if (tempGameObject != null) DestroyImmediate(tempGameObject);
            }
        }
    }
}