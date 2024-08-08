using UnityEditor;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    [CustomEditor(typeof(EditorI18N))]
    public class EditorI18NEditor : Editor {
        SerializedProperty i18nData;

        void OnEnable() {
            i18nData = serializedObject.FindProperty("i18nData");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            EditorGUILayout.HelpBox("This is an editor only scriptable object, it's used to store i18n data for editor only.", MessageType.Info);
            EditorGUILayout.PropertyField(i18nData);
            if (GUILayout.Button("Reload")) (target as EditorI18N).Reload();
            serializedObject.ApplyModifiedProperties();
        }

        public static void DrawLocaleField() {
            var i18n = EditorI18N.Instance;
            var languageIndex = i18n.LanguageIndex;
            using (var changeCheck = new EditorGUI.ChangeCheckScope()) {
                languageIndex = EditorGUILayout.Popup(i18n.GetOrDefault("Language"), languageIndex, i18n.LanguageNames);
                if (changeCheck.changed) i18n.LanguageIndex = languageIndex;
            }
            var machineTranslated = i18n["MachineTranslationMessage"];
            if (!string.IsNullOrEmpty(machineTranslated)) EditorGUILayout.HelpBox(machineTranslated, MessageType.Info);
        }
    }
}