using UnityEditor;
using UdonSharp;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(OverlayControl))]
    public class OverlayControlEditor : VVMWEditorBase {
        bool isUdonSharp;

        protected override void OnEnable() {
            base.OnEnable();
            isUdonSharp = target is UdonSharpBehaviour;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (isUdonSharp && UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            DrawDefaultInspector();
        }
    }

    [CustomEditor(typeof(UIHandler))]
    public class UIHandlerEditor : VVMWEditorBase {
        bool isUdonSharp;

        protected override void OnEnable() {
            base.OnEnable();
            isUdonSharp = target is UdonSharpBehaviour;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (isUdonSharp && UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            DrawDefaultInspector();
        }
    }
}