using UnityEngine;
using UnityEditor;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VideoPlayerHandler))]
    public class VideoPlayerHandlerEditor : VVMWEditorBase {

        SerializedProperty texturePropertyNameProperty,
            isAvProProperty,
            playerNameProperty,
            primaryAudioSourceProperty,
            useFlickerWorkaroundProperty,
            blitMaterialProperty;
        Material[] materials;

        [InitializeOnLoadMethod]
        static void RegisterTrustedUrlDelegate() {
            AbstractMediaPlayerHandler.applyTurstedUrl = TrustedUrlUtils.CopyTrustedUrls;
        }

        protected override void OnEnable() {
            base.OnEnable();
            texturePropertyNameProperty = serializedObject.FindProperty("texturePropertyName");
            playerNameProperty = serializedObject.FindProperty("playerName");
            isAvProProperty = serializedObject.FindProperty("isAvPro");
            primaryAudioSourceProperty = serializedObject.FindProperty("primaryAudioSource");
            useFlickerWorkaroundProperty = serializedObject.FindProperty("useFlickerWorkaround");
            blitMaterialProperty = serializedObject.FindProperty("blitMaterial");
            materials = null;
        }

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(this.target, false, false)) return;
            if (PrefabUtility.IsPartOfPrefabAsset(this.target)) {
                EditorGUILayout.HelpBox("Please use the prefab instance in scene to edit.", MessageType.Info);
                DrawDefaultInspector();
                return;
            }
            if (Application.isPlaying) {
                EditorGUILayout.HelpBox("Please exit play mode to edit.", MessageType.Info);
                DrawDefaultInspector();
                return;
            }
            serializedObject.Update();
            var target = this.target as VideoPlayerHandler;
            EditorGUILayout.PropertyField(playerNameProperty);
            EditorGUILayout.LabelField("Mesh Renderer (Don't change it unless needed)", EditorStyles.boldLabel);
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(target.gameObject);
            HideControlledComponent(renderer);
            using (var so = new SerializedObject(renderer)) {
                so.FindProperty("m_Enabled").boolValue = false;
                var materialsProperty = so.FindProperty("m_Materials");
                bool isChanged = false;
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    EditorGUILayout.PropertyField(materialsProperty, true);
                    isChanged = changed.changed;
                }
                if (materials == null || materials.Length != materialsProperty.arraySize) {
                    materials = new Material[materialsProperty.arraySize];
                    isChanged = true;
                }
                if (isChanged)
                    for (int i = 0; i < materials.Length; i++)
                        materials[i] = materialsProperty.GetArrayElementAtIndex(i).objectReferenceValue as Material;
                so.ApplyModifiedProperties();
            }
            Utils.DrawShaderPropertiesField(
                texturePropertyNameProperty,
                Utils.GetTempContent(texturePropertyNameProperty.displayName, texturePropertyNameProperty.tooltip),
                null, materials,
                ShaderUtil.ShaderPropertyType.TexEnv
            );
            EditorGUILayout.Space();
            var controlledVideoPlayer = target.GetComponent<BaseVRCVideoPlayer>();
            if (controlledVideoPlayer is VRCUnityVideoPlayer unityVideoPlayer) {
                EditorGUILayout.LabelField("Unity Video Player", EditorStyles.boldLabel);
                HideControlledComponent(unityVideoPlayer);
                using (var so = new SerializedObject(unityVideoPlayer)) {
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), true);
                    so.ApplyModifiedProperties();
                }
            } else if (controlledVideoPlayer is VRCAVProVideoPlayer avProVideoPlayer) {
                EditorGUILayout.LabelField("AVPro Video Player", EditorStyles.boldLabel);
                HideControlledComponent(avProVideoPlayer);
                using (var so = new SerializedObject(avProVideoPlayer)) {
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), true);
                    EditorGUILayout.PropertyField(so.FindProperty("useLowLatency"), true);
                    so.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.PropertyField(primaryAudioSourceProperty);
            if (isAvProProperty.boolValue) {
                EditorGUILayout.PropertyField(useFlickerWorkaroundProperty);
                if (useFlickerWorkaroundProperty.boolValue)
                    EditorGUILayout.PropertyField(blitMaterialProperty);
            }
            serializedObject.ApplyModifiedProperties();
        }

        static void HideControlledComponent(Component component) {
            if (component == null) return;
            if (component.hideFlags.HasFlag(HideFlags.HideInInspector)) return;
            component.hideFlags |= HideFlags.HideInInspector;
            EditorUtility.SetDirty(component);
        }
    }
}