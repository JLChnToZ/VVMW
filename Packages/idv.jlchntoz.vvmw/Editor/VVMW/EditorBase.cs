using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.Editors;
using JLChnToZ.VRC.Foundation.I18N.Editors;

namespace JLChnToZ.VRC.VVMW.Editors {
    using Utils = Foundation.Editors.Utils;
    public abstract class VVMWEditorBase : Editor {
        const string bannerTextureGUID = "e8354bc2ac14e86498c0983daf484661";
        const string iconGUID = "a24ecd1d23cca9e46871bc17dfe3bd46";
        const string fontGUID = "088cf7162d0a81c46ad54028cfdcb382";
        const string listingsID = "idv.jlchntoz.xtlcdn-listing";
        const string listingsURL = "https://xtlcdn.github.io/vpm/index.json";
        protected static readonly Dictionary<Type, (FieldInfo fieldInfo, Type editorType)> controllableTypes = new Dictionary<Type, (FieldInfo, Type)>();
        protected static readonly Dictionary<Type, Type> editorTypes = new Dictionary<Type, Type>();
        static readonly List<MonoBehaviour> behaviours = new List<MonoBehaviour>();
        static Texture2D bannerTexture;
        static PackageSelfUpdater selfUpdater;
        static Font font;
        static GUIStyle versionLabelStyle;
        protected static EditorI18N i18n;
        bool isUdonSharp;

        [InitializeOnLoadMethod]
        static void Init() {
            AssemblyReloadEvents.afterAssemblyReload += GatherControlledTypes;
            GatherControlledTypes();
        }

        public static void HorizontalLine() {
            var padding = EditorGUIUtility.singleLineHeight;
            var rect = EditorGUILayout.GetControlRect(GUILayout.Height(padding));
            rect.height = 1f;
            rect.y += padding * 0.5f;
            EditorGUI.DrawRect(rect, Color.gray);
        }

        public static void UpdateTitle(GUIContent titleContent, string languageKey, bool unsaved = false) {
            var iconPath = AssetDatabase.GUIDToAssetPath(iconGUID);
            if (iconPath != null) {
                var icon = AssetDatabase.LoadAssetAtPath<Texture>(iconPath);
                titleContent.image = icon;
            }
            var text = i18n.GetOrDefault(languageKey);
            if (unsaved) text += "*";
            titleContent.text = text;
        }

        static void GatherControlledTypes() {
            controllableTypes.Clear();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var inheritedEditors = new Dictionary<Type, Type>();
            var processedTypes = new HashSet<Type>();
            var inspectedTypeField = typeof(CustomEditor).GetField("m_InspectedType", flags);
            var editorForChildClassesField = typeof(CustomEditor).GetField("m_EditorForChildClasses", flags);
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                foreach (var type in assembly.GetTypes()) {
                    if (type.IsAbstract) continue;
                    if (type.IsSubclassOf(typeof(MonoBehaviour))) {
                        if (processedTypes.Add(type) && !editorTypes.ContainsKey(type)) {
                            Type targetType = null;
                            int score = 0;
                            foreach (var inheritedEditor in inheritedEditors) {
                                int currentScore = 0;
                                for (var bt = type; bt != null; bt = bt.BaseType) {
                                    if (bt == inheritedEditor.Key) {
                                        if (currentScore > score) {
                                            score = currentScore;
                                            targetType = inheritedEditor.Value;
                                        }
                                        break;
                                    }
                                    currentScore--;
                                }
                            }
                            if (targetType != null) editorTypes[type] = targetType;
                        }
                        if (type.IsSubclassOf(typeof(UdonSharpBehaviour))) {
                            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                            if (fields.Length == 0) continue;
                            foreach (var field in fields) {
                                if (field.FieldType == typeof(Core) && field.GetCustomAttribute<SingletonCoreControlAttribute>() != null) {
                                    editorTypes.TryGetValue(type, out var editorType);
                                    controllableTypes[type] = (field, editorType);
                                    break;
                                }
                            }
                        }
                    } else if (type.IsSubclassOf(typeof(VVMWEditorBase))) {
                        var customEditorAttribute = type.GetCustomAttribute<CustomEditor>();
                        if (customEditorAttribute == null) continue;
                        var inspectedType = inspectedTypeField.GetValue(customEditorAttribute) as Type;
                        if (controllableTypes.TryGetValue(inspectedType, out var value))
                            controllableTypes[inspectedType] = (value.fieldInfo, type);
                        editorTypes[inspectedType] = type;
                        if ((bool)editorForChildClassesField.GetValue(customEditorAttribute)) {
                            inheritedEditors[inspectedType] = type;
                            foreach (var pType in processedTypes) {
                                int baseScore = 0, currentScore = 0;
                                if (editorTypes.TryGetValue(pType, out var editorType)) {
                                    for (var bt = pType; bt != null; bt = bt.BaseType) {
                                        if (bt == editorType) break;
                                        baseScore--;
                                    }
                                } else
                                    baseScore = int.MinValue;
                                for (var bt = pType; bt != null; bt = bt.BaseType) {
                                    if (bt == inspectedType) {
                                        if (currentScore > baseScore) editorTypes[pType] = type;
                                        break;
                                    }
                                    currentScore--;
                                }
                            }
                        }
                    }
                }
        }

        protected virtual void OnEnable() {
            if (selfUpdater == null) {
                selfUpdater = new PackageSelfUpdater(GetType().Assembly, listingsID, listingsURL);
                selfUpdater.CheckInstallationInBackground();
            }
            if (bannerTexture == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(bannerTextureGUID);
                if (!string.IsNullOrEmpty(assetPath)) bannerTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            }
            if (font == null) {
                var assetPath = AssetDatabase.GUIDToAssetPath(fontGUID);
                if (!string.IsNullOrEmpty(assetPath)) font = AssetDatabase.LoadAssetAtPath<Font>(assetPath);
            }
#if VPM_RESOLVER_INCLUDED
            selfUpdater.OnVersionRefreshed += Repaint;
#endif
            i18n = EditorI18N.Instance;
            isUdonSharp = target is UdonSharpBehaviour;
        }

        protected virtual void OnDisable() {
#if VPM_RESOLVER_INCLUDED
            if (selfUpdater != null) selfUpdater.OnVersionRefreshed -= Repaint;
#endif
        }

        protected bool GetFirstVizVidComponent(out IVizVidCompoonent firstComponent) {
            if (targets.Length == 1)
                foreach (var b in behaviours)
                    if (b is IVizVidCompoonent) {
                        firstComponent = b as IVizVidCompoonent;
                        return b == target;
                    }
            firstComponent = null;
            return false;
        }

        public override void OnInspectorGUI() {
            (target as Component).GetComponents(behaviours);
            foreach (var b in behaviours)
                if (editorTypes.ContainsKey(b.GetType())) {
                    if (b == target) DrawBanner();
                    break;
                }
            if (GUILayout.Button(i18n.GetOrDefault("GlobalSettings"))) GlobalSettingsEditorWindow.ShowWindow();
            if (isUdonSharp) {
                using (new EditorGUILayout.VerticalScope(GUI.skin.box))
                using (new EditorGUI.IndentLevelScope()) {
                    if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, true, false))
                        return;
                }
                EditorGUILayout.Space();
            }
            DrawInspectorGUI();
        }

        protected static void DrawBanner() {
            if (bannerTexture != null) {
                var rect = GUILayoutUtility.GetRect(0, 120);
                GUILayout.Space(rect.height);
                var bannerRect = new Rect(
                    rect.x + (rect.width - bannerTexture.width * rect.height / bannerTexture.height) / 2,
                    rect.y,
                    bannerTexture.width * rect.height / bannerTexture.height,
                    rect.height
                );
                GUI.DrawTexture(bannerRect, bannerTexture);
                if (versionLabelStyle == null)
                    versionLabelStyle = new GUIStyle(EditorStyles.whiteLargeLabel) {
                        alignment = TextAnchor.UpperRight,
                        padding = new RectOffset(2, 4, 2, 4),
                        fontStyle = FontStyle.Bold,
                        font = font,
                    };
                var tempContent = Utils.GetTempContent($"v{selfUpdater.CurrentVersion}");
                var versionSize = versionLabelStyle.CalcSize(tempContent);
                GUI.Label(new Rect(bannerRect.xMax - versionSize.x, bannerRect.yMin, versionSize.x, versionSize.y), tempContent, versionLabelStyle);
            }
            EditorGUILayout.Space();
            I18NUtils.DrawLocaleField();
            selfUpdater.DrawUpdateNotifier();
            EditorGUILayout.Space();
        }

        public virtual void DrawInspectorGUI() {
            serializedObject.Update();
            DrawEmbeddedInspectorGUI();
            serializedObject.ApplyModifiedProperties();
        }

        public virtual void DrawEmbeddedInspectorGUI() {
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
                do {
                    if (iterator.propertyPath == "m_Script") continue;
                    EditorGUILayout.PropertyField(iterator);
                } while (iterator.NextVisible(false));
        }
    }
}
