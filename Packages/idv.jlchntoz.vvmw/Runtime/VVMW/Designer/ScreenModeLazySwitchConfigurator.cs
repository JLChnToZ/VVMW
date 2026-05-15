using UnityEngine;
using JLChnToZ.VRC.Foundation;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace JLChnToZ.VRC.VVMW.Designer {
    [EditorOnly, ExecuteInEditMode, DisallowMultipleComponent]
    [RequireComponent(typeof(LazySwitch))]
    [AddComponentMenu("/VizVid/Components/Screen Mode Lazy Switch Configurator")]
    public class ScreenModeLazySwitchConfigurator : MonoBehaviour {
        void Awake() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return;
#endif
            Connect();
        }

        public void Connect() {
#if UNITY_EDITOR && !COMPILER_UDONSHARP
            var globalSettings = GlobalSettings.Instance;
            if (globalSettings == null) return;
            var masterSwitch = globalSettings.MasterScreenModeSwitch;
            if (masterSwitch == null) {
                Debug.LogError("Master screen mode switch is not assigned in GlobalSettings.", globalSettings);
                return;
            }
            var localSwitch = GetComponent<LazySwitch>();
            var currentState = localSwitch.State;
            var masterState = masterSwitch.State;
            using (var so = new SerializedObject(localSwitch)) {
                var masterSwitchProp = so.FindProperty("masterSwitch");
                masterSwitchProp.objectReferenceValue = masterSwitch;
                if (currentState != masterState) GlobalSettings.UpdateSubSwitch(so, currentState, masterState);
                so.ApplyModifiedProperties();
            }
            Undo.DestroyObjectImmediate(this);
#endif
        }

#if UNITY_EDITOR && !COMPILER_UDONSHARP
        [CustomEditor(typeof(ScreenModeLazySwitchConfigurator))]
        class Editor_ : Editor {
            public override void OnInspectorGUI() { }
        }
#endif
    }
}
