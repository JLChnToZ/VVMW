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
                EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.VideoPlayerHandler:no_prefab"), MessageType.Info);
                DrawDefaultInspector();
                return;
            }
            if (Application.isPlaying) {
                EditorGUILayout.HelpBox(i18n.GetOrDefault("JLChnToZ.VRC.VVMW.VideoPlayerHandler:no_play_mode"), MessageType.Info);
                DrawDefaultInspector();
                return;
            }
            serializedObject.Update();
            var target = this.target as VideoPlayerHandler;
            EditorGUILayout.PropertyField(playerNameProperty);
            EditorGUILayout.LabelField(i18n.GetLocalizedContent("HEADER:MeshRenderer"), EditorStyles.boldLabel);
            var renderer = target.GetComponent<Renderer>();
            if (renderer == null) renderer = Undo.AddComponent<MeshRenderer>(target.gameObject);
            HideControlledComponent(renderer);
            using (var so = new SerializedObject(renderer)) {
                so.FindProperty("m_Enabled").boolValue = false;
                var materialsProperty = so.FindProperty("m_Materials");
                bool isChanged = false;
                using (var changed = new EditorGUI.ChangeCheckScope()) {
                    EditorGUILayout.PropertyField(materialsProperty, i18n.GetLocalizedContent("VVMW.Material"), true);
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
                i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.VideoPlayerHandler.texturePropertyName"),
                null, materials,
                ShaderUtil.ShaderPropertyType.TexEnv
            );
            EditorGUILayout.Space();
            var controlledVideoPlayer = target.GetComponent<BaseVRCVideoPlayer>();
            if (controlledVideoPlayer is VRCUnityVideoPlayer unityVideoPlayer) {
                EditorGUILayout.LabelField(i18n.GetLocalizedContent("HEADER:UnityVideoPlayer"), EditorStyles.boldLabel);
                HideControlledComponent(unityVideoPlayer);
                using (var so = new SerializedObject(unityVideoPlayer)) {
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.VideoPlayerHandler.maxResolution"), true);
                    so.ApplyModifiedProperties();
                }
            } else if (controlledVideoPlayer is VRCAVProVideoPlayer avProVideoPlayer) {
                EditorGUILayout.LabelField(i18n.GetLocalizedContent("HEADER:AVProVideoPlayer"), EditorStyles.boldLabel);
                HideControlledComponent(avProVideoPlayer);
                using (var so = new SerializedObject(avProVideoPlayer)) {
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.VideoPlayerHandler.maxResolution"), true);
                    EditorGUILayout.PropertyField(so.FindProperty("useLowLatency"), i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.VideoPlayerHandler.isLowLatency"), true);
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