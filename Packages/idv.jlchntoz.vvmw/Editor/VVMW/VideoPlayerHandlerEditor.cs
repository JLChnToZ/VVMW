using UnityEngine;
using UnityEditor;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;
using UdonSharpEditor;

namespace JLChnToZ.VRC.VVMW.Editors {
    [CustomEditor(typeof(VideoPlayerHandler))]
    public class VideoPlayerHandlerEditor : VVMWEditorBase {

        SerializedProperty texturePropertyNameProperty, useSharedMaterialProperty, isAvProProperty, playerNameProperty, primaryAudioSourceProperty, useFlickerWorkaroundProperty, blitMaterialProperty;

        protected override void OnEnable() {
            base.OnEnable();
            texturePropertyNameProperty = serializedObject.FindProperty("texturePropertyName");
            useSharedMaterialProperty = serializedObject.FindProperty("useSharedMaterial");
            playerNameProperty = serializedObject.FindProperty("playerName");
            isAvProProperty = serializedObject.FindProperty("isAvPro");
            primaryAudioSourceProperty = serializedObject.FindProperty("primaryAudioSource");
            useFlickerWorkaroundProperty = serializedObject.FindProperty("useFlickerWorkaround");
            blitMaterialProperty = serializedObject.FindProperty("blitMaterial");
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
                EditorGUILayout.PropertyField(so.FindProperty("m_Materials"), true);
                so.ApplyModifiedProperties();
            }
            EditorGUILayout.PropertyField(texturePropertyNameProperty);
            var meshFilter = target.GetComponent<MeshFilter>();
            HideControlledComponent(meshFilter);
            EditorGUILayout.Space();
            var controlledVideoPlayer = target.GetComponent<BaseVRCVideoPlayer>();
            if (controlledVideoPlayer is VRCUnityVideoPlayer unityVideoPlayer) {
                EditorGUILayout.LabelField("Unity Video Player", EditorStyles.boldLabel);
                isAvProProperty.boolValue = false;
                if (useSharedMaterialProperty.boolValue) useSharedMaterialProperty.boolValue = false;
                HideControlledComponent(unityVideoPlayer);
                using (var so = new SerializedObject(unityVideoPlayer)) {
                    so.FindProperty("autoPlay").boolValue = false;
                    so.FindProperty("loop").boolValue = false;
                    so.FindProperty("renderMode").intValue = 1;
                    so.FindProperty("targetMaterialRenderer").objectReferenceValue = renderer;
                    so.FindProperty("targetMaterialProperty").stringValue = texturePropertyNameProperty.stringValue;
                    so.FindProperty("aspectRatio").intValue = 0;
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), true);
                    var primaryAudioSource = primaryAudioSourceProperty.objectReferenceValue as AudioSource;
                    if (primaryAudioSource != null) {
                        var prop = so.FindProperty("targetAudioSources");
                        prop.arraySize = 1;
                        prop.GetArrayElementAtIndex(0).objectReferenceValue = primaryAudioSource;
                    }
                    so.ApplyModifiedProperties();
                }
            } else if (controlledVideoPlayer is VRCAVProVideoPlayer avProVideoPlayer) {
                EditorGUILayout.LabelField("AVPro Video Player", EditorStyles.boldLabel);
                isAvProProperty.boolValue = true;
                if (!useSharedMaterialProperty.boolValue) useSharedMaterialProperty.boolValue = true;
                HideControlledComponent(avProVideoPlayer);
                using (var so = new SerializedObject(avProVideoPlayer)) {
                    so.FindProperty("autoPlay").boolValue = false;
                    so.FindProperty("loop").boolValue = false;
                    EditorGUILayout.PropertyField(so.FindProperty("maximumResolution"), true);
                    EditorGUILayout.PropertyField(so.FindProperty("useLowLatency"), true);
                    so.ApplyModifiedProperties();
                }
                var controlledVideoScreen = target.GetComponent<VRCAVProVideoScreen>();
                if (controlledVideoScreen == null) controlledVideoScreen = Undo.AddComponent<VRCAVProVideoScreen>(target.gameObject);
                HideControlledComponent(controlledVideoScreen);
                using (var so = new SerializedObject(controlledVideoScreen)) {
                    so.FindProperty("videoPlayer").objectReferenceValue = avProVideoPlayer;
                    so.FindProperty("materialIndex").intValue = 0;
                    so.FindProperty("textureProperty").stringValue = texturePropertyNameProperty.stringValue;
                    so.FindProperty("useSharedMaterial").boolValue = false;
                    so.ApplyModifiedProperties();
                }
                var primaryAudioSource = primaryAudioSourceProperty.objectReferenceValue as AudioSource;
                if (primaryAudioSource != null) {
                    if (!primaryAudioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker))
                        speaker = Undo.AddComponent<VRCAVProVideoSpeaker>(primaryAudioSource.gameObject);
                    using (var so = new SerializedObject(speaker)) {
                        so.FindProperty("videoPlayer").objectReferenceValue = avProVideoPlayer;
                        so.ApplyModifiedProperties();
                    }
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