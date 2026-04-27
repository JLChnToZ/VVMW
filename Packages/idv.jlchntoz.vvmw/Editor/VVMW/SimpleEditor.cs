using UnityEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VizVidBehaviour), true)]
    public class VizVidBehaviourEditor : VVMWEditorBase {
    }

    [CustomEditor(typeof(AudioController), true)]
    public class AudioControllerEditor : VVMWEditorBase {
    }

    [CustomEditor(typeof(ActiveRegionManager), true)]
    public class ActiveRegionManagerEditor : VVMWEditorBase {
    }
}