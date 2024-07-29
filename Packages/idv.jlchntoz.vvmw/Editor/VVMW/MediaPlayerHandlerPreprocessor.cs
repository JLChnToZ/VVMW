using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEditor.Build;
using UnityEditor;
using UnityEditor.Build.Reporting;
using VRC.SDK3.Video.Components;
using VRC.SDK3.Video.Components.AVPro;
using VRC.SDK3.Video.Components.Base;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class MediaPlayerHandlerPreprocessor : IPreprocessor {
        public int CallbackOrder => 0;

        public void OnPreprocess(Scene scene) {
            foreach (var handler in scene.IterateAllComponents<AbstractMediaPlayerHandler>())
                using (var handlerSo = new SerializedObject(handler)) {
                    TrustedUrlTypes urlType = default;
                    if (handler is VideoPlayerHandler) {
                        if (!handler.TryGetComponent(out BaseVRCVideoPlayer videoPlayer))
                            continue;
                        if (!handler.TryGetComponent(out Renderer renderer))
                            renderer = handler.gameObject.AddComponent<MeshRenderer>();
                        renderer.enabled = false;
                        renderer.lightProbeUsage = LightProbeUsage.Off;
                        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                        renderer.shadowCastingMode = ShadowCastingMode.Off;
                        renderer.receiveShadows = false;
                        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                        renderer.allowOcclusionWhenDynamic = false;
                        var texturePropertyName = handlerSo.FindProperty("texturePropertyName").stringValue;
                        var audioSource = handlerSo.FindProperty("primaryAudioSource").objectReferenceValue as AudioSource;
                        using (var videoPlayerSo = new SerializedObject(videoPlayer)) {
                            videoPlayerSo.FindProperty("autoPlay").boolValue = false;
                            videoPlayerSo.FindProperty("loop").boolValue = false;
                            if (videoPlayer is VRCAVProVideoPlayer) {
                                urlType = TrustedUrlTypes.AVProDesktop;
                                handlerSo.FindProperty("isLowLatency").boolValue = videoPlayerSo.FindProperty("useLowLatency").boolValue;
                                if (!videoPlayer.TryGetComponent(out VRCAVProVideoScreen screen))
                                    screen = handler.gameObject.AddComponent<VRCAVProVideoScreen>();
                                using (var screenSo = new SerializedObject(screen)) {
                                    screenSo.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
                                    screenSo.FindProperty("materialIndex").intValue = 0;
                                    screenSo.FindProperty("textureProperty").stringValue = texturePropertyName;
                                    screenSo.FindProperty("useSharedMaterial").boolValue = false;
                                    screenSo.ApplyModifiedPropertiesWithoutUndo();
                                }
                                if (audioSource != null) {
                                    if (!audioSource.TryGetComponent(out VRCAVProVideoSpeaker speaker))
                                        speaker = audioSource.gameObject.AddComponent<VRCAVProVideoSpeaker>();
                                    using (var speakerSo = new SerializedObject(speaker)) {
                                        speakerSo.FindProperty("videoPlayer").objectReferenceValue = videoPlayer;
                                        speakerSo.ApplyModifiedPropertiesWithoutUndo();
                                    }
                                }
                            } else if (videoPlayer is VRCUnityVideoPlayer) {
                                urlType = TrustedUrlTypes.UnityVideo;
                                videoPlayerSo.FindProperty("renderMode").intValue = 1;
                                videoPlayerSo.FindProperty("targetMaterialRenderer").objectReferenceValue = renderer;
                                videoPlayerSo.FindProperty("targetMaterialProperty").stringValue = texturePropertyName;
                                videoPlayerSo.FindProperty("aspectRatio").intValue = 0;
                                if (audioSource != null) {
                                    var targetAudioSources = videoPlayerSo.FindProperty("targetAudioSources");
                                    targetAudioSources.arraySize = 1;
                                    targetAudioSources.GetArrayElementAtIndex(0).objectReferenceValue = audioSource;
                                }
                            }
                            videoPlayerSo.ApplyModifiedPropertiesWithoutUndo();
                        }
                        bool isAvPro = urlType == TrustedUrlTypes.AVProDesktop;
                        handlerSo.FindProperty("isAvPro").boolValue = isAvPro;
                        handlerSo.FindProperty("useSharedMaterial").boolValue = isAvPro;
                    } else if (handler is ImageViewerHandler)
                        urlType = TrustedUrlTypes.ImageUrl;
                    if (!TrustedUrlUtils.CopyTrustedUrlsToStringArrayUnchecked(handlerSo.FindProperty("trustedUrlDomains"), urlType))
                        Debug.LogError($"[MediaPlayerHandlerPreprocessor] Failed to copy trusted URL domains for {handler.name}.", handler);
                    handlerSo.ApplyModifiedPropertiesWithoutUndo();
                }
        }
    }
}