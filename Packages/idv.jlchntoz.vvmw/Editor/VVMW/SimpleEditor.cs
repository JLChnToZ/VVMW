using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VizVidBehaviour), true)]
    public class VizVidBehaviourEditor : VVMWEditorBase {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            serializedObject.Update();
            var iterator = serializedObject.GetIterator();
            if (iterator.NextVisible(true))
                do {
                    if (iterator.propertyPath == "m_Script") continue;
                    EditorGUILayout.PropertyField(iterator);
                } while (iterator.NextVisible(false));
            serializedObject.ApplyModifiedProperties();
        }
    }
}