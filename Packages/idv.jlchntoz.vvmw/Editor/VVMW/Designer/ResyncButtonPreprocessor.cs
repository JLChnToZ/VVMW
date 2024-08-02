using UnityEditor;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Designer {
    internal static class ResyncButtonPreprocessor {
        [InitializeOnLoadMethod]
        static void RegisterUdonSharpBehaviourGetter() {
            ResyncButtonConfigurator.getBackingUdonBehaviour = UdonSharpEditorUtility.GetBackingUdonBehaviour;
        }
    }
}