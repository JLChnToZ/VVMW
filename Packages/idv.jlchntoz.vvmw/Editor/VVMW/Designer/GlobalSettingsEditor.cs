using UnityEngine;
using UnityEditor;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using UnityEditor.SceneManagement;
using JLChnToZ.VRC.VVMW.Designer;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW.Editors {

    [CustomEditor(typeof(GlobalSettings), true)]
    public class GlobalSettingsEditor : VVMWEditorBase {
        bool isPrefabMode;

        protected override void OnEnable() {
            base.OnEnable();
            isPrefabMode = PrefabStageUtility.GetCurrentPrefabStage() != null;
        }

        public override void DrawEmbeddedInspectorGUI() {
            if (isPrefabMode) base.DrawEmbeddedInspectorGUI();
        }
    }
    
    public class GlobalSettingsEditorWindow : EditorWindow {
        const string prefabPath = "Packages/idv.jlchntoz.vvmw/Prefabs/VizVid Global Settings.prefab";
        const string fullscreenModeImageGUID = "98d4735164b493e4aad4bd4dfd002fca";
        const string classicModeImageGUID = "6306f4580d17f3d498feab38059d6d80";

        GlobalSettings globalSettings;
        Texture2D fullscreenModeImage, classicModeImage;
        SerializedObject globalSettingsSO, unlockModeSwitchSO;
        SerializedProperty masterSwitchProp, defaultStrategyProp, masterSwitchStateProp;

        static void ResolveImage(ref Texture2D image, string guid) {
            if (image != null) return;
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return;
            image = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void DrawImage(Texture2D image, float width) {
            if (image == null) return;
            var rect = GUILayoutUtility.GetRect(0, width);
            rect.height = rect.width * image.height / image.width;
            GUI.Box(rect, image);
        }

        public static void ShowWindow() {
            var win = GetWindow<GlobalSettingsEditorWindow>();
            win.ShowAuxWindow();
        }

        void OnEnable() {
            var title = titleContent;
            VVMWEditorBase.UpdateTitle(title, "GlobalSettings");
            titleContent = title;
            var fixedSize = new Vector2(800, 400);
            minSize = fixedSize;
            maxSize = fixedSize;
        }

        void OnDisable() {
            globalSettingsSO?.Dispose();
            unlockModeSwitchSO?.Dispose();
        }

        bool TrySpawnPrefabIfNotExists() {
            if (globalSettings != null) return true;
            globalSettings = GlobalSettings.Instance;
            if (globalSettings != null) return true;
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                return false;
            globalSettings = FindObjectOfType<GlobalSettings>(true);
            if (globalSettings != null) return true;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return false;
            var instance = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (instance == null) return false;
            instance.transform.SetAsFirstSibling();
            return instance.TryGetComponent(out globalSettings);
        }

        void OnGUI() {
            if (!TrySpawnPrefabIfNotExists()) Close();
            ResolveSerializedProperties();
            if (unlockModeSwitchSO != null) DrawUnlockModeSettings();
            DrawSteragySettings();
        }

        void ResolveSerializedProperties() {
            if (globalSettingsSO != null && globalSettingsSO.targetObject != globalSettings) {
                globalSettingsSO.Dispose();
                globalSettingsSO = null;
            }
            if (globalSettingsSO == null) {
                globalSettingsSO = new SerializedObject(globalSettings);
                masterSwitchProp = globalSettingsSO.FindProperty("masterScreenModeSwitch");
                defaultStrategyProp = globalSettingsSO.FindProperty("defaultCoreMatchingStrategy");
            }
            globalSettingsSO.Update();
            var masterSwitch = masterSwitchProp.objectReferenceValue;
            if (unlockModeSwitchSO != null && unlockModeSwitchSO.targetObject != masterSwitch) {
                unlockModeSwitchSO.Dispose();
                unlockModeSwitchSO = null;
            }
            if (masterSwitch != null && unlockModeSwitchSO == null) {
                unlockModeSwitchSO = new SerializedObject(masterSwitch);
                masterSwitchStateProp = unlockModeSwitchSO.FindProperty("state");
            }
        }

        void DrawUnlockModeSettings() {
            var i18n = EditorI18N.Instance;
            unlockModeSwitchSO.Update();
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("UnlockMode"), EditorStyles.boldLabel);
            var value = masterSwitchStateProp.intValue;
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUILayout.VerticalScope()) {
                    ResolveImage(ref classicModeImage, classicModeImageGUID);
                    DrawImage(classicModeImage, 190);
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        bool selected = EditorGUILayout.Toggle(i18n.GetLocalizedContent("BarMode"), value == 0, EditorStyles.radioButton);
                        if (changed.changed && selected) UpdateAllSubSwitches(0);
                    }
                }
                using (new EditorGUILayout.VerticalScope()) {
                    ResolveImage(ref fullscreenModeImage, fullscreenModeImageGUID);
                    DrawImage(fullscreenModeImage, 190);
                    using (var changed = new EditorGUI.ChangeCheckScope()) {
                        bool selected = EditorGUILayout.Toggle(i18n.GetLocalizedContent("FullScreenMode"), value == 1, EditorStyles.radioButton);
                        if (changed.changed && selected) UpdateAllSubSwitches(1);
                    }
                }
            }
            unlockModeSwitchSO.ApplyModifiedProperties();
        }

        void DrawSteragySettings() {
            var i18n = EditorI18N.Instance;
            EditorGUILayout.PropertyField(defaultStrategyProp, i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.OverlayControl.coreControlStrategy"));
            globalSettingsSO.ApplyModifiedProperties();
        }

        void UpdateAllSubSwitches(int newState) {
            var originalState = masterSwitchStateProp.intValue;
            if (newState == originalState) return;
            GlobalSettings.UpdateAllSubSwitches(masterSwitchProp.objectReferenceValue as LazySwitch, originalState, newState);
            masterSwitchStateProp.intValue = newState;
        }
    }
}
