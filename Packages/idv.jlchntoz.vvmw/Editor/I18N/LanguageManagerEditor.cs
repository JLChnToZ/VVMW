using UnityEngine;
using UnityEditor;
using UdonSharpEditor;
using JLChnToZ.VRC.VVMW.Editors;
using UnityEditorInternal;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    [CustomEditor(typeof(LanguageManager))]
    public class LanguageManagerEditor : VVMWEditorBase {
        static GUIContent textContent;
        SerializedProperty languageJsonFiles, languageJson;
        LanguageEditorWindow openedWindow;
        ReorderableListUtils languageJsonFilesList;
        GUIStyle wrappedTextAreaStyle;
        bool showJson = false;

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
        }
        
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, drawScript: false)) return;
            using (new EditorGUI.DisabledScope(openedWindow != null))
                if (GUILayout.Button("Open Language Editor"))
                    openedWindow = LanguageEditorWindow.Open(target as LanguageManager);
            EditorGUILayout.Space();
            serializedObject.Update();
            using (var changed = new EditorGUI.ChangeCheckScope()) {
                languageJsonFilesList.list.DoLayoutList();
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