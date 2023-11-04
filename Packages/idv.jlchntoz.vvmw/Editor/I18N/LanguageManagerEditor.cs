using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using JLChnToZ.VRC.VVMW.Editors;
using System;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    [CustomEditor(typeof(LanguageManager))]
    public class LanguageManagerEditor : VVMWEditorBase {
        static GUIContent textContent;
        SerializedProperty languageJsonFiles, languageJson;
        LanguageEditorWindow openedWindow;
        ReorderableListUtils languageJsonFilesList;
        GUIStyle wrappedTextAreaStyle;
        bool showJson = false;
        [NonSerialized] bool hasInit;

        protected override void OnEnable() {
            base.OnEnable();
            if (textContent == null) textContent = new GUIContent();
            if (wrappedTextAreaStyle == null)
                wrappedTextAreaStyle = new GUIStyle(EditorStyles.textArea) {
                    wordWrap = true
                };
            languageJsonFiles = serializedObject.FindProperty("languageJsonFiles");
            languageJson = serializedObject.FindProperty("languageJson");
            languageJsonFilesList = new ReorderableListUtils(languageJsonFiles);
            hasInit = true;
        }
        
        public override void OnInspectorGUI() {
            if (!hasInit) OnEnable();
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, drawScript: false)) return;
            if (GUILayout.Button("Open Language Editor")) {
                if (openedWindow != null) openedWindow.Focus();
                else openedWindow = LanguageEditorWindow.Open(target as LanguageManager);
            }
            EditorGUILayout.Space();
            serializedObject.Update();
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                languageJsonFilesList.Draw();
                if (changed.changed && openedWindow != null) openedWindow.RefreshJsonLists();
            }
            if (openedWindow == null || openedWindow.LanguageManager != target) openedWindow = null;
            if (showJson = EditorGUILayout.Foldout(showJson, languageJson.displayName, true)) {
                textContent.text = languageJson.stringValue;
                var height = wrappedTextAreaStyle.CalcHeight(textContent, EditorGUIUtility.currentViewWidth);
                var rect = EditorGUILayout.GetControlRect(false, height);
                using (var propScope = new EditorGUI.PropertyScope(rect, textContent, languageJson))
                using (var changeScope = new EditorGUI.ChangeCheckScope()) {
                    var newJson = EditorGUI.TextArea(rect, languageJson.stringValue, wrappedTextAreaStyle);
                    if (changeScope.changed) languageJson.stringValue = newJson;
                }
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}