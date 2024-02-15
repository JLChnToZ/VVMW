using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VizVidBehaviour), true)]
    public class VizVidBehaviourEditor : VVMWEditorBase {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target, false, false)) return;
            DrawDefaultInspector();
        }
    }
}