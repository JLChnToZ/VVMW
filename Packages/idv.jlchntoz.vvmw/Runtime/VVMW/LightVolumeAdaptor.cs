using System;
using System.Collections.Generic;
using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
using JLChnToZ.VRC.VVMW.Designer;
#if VRC_LIGHT_VOLUMES
using VRCLightVolumes;
#endif

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    [AddComponentMenu("VizVid/Light Volume Adaptor (VizVid)")]
    [HelpURL("https://xtlcdn.github.io/VizVid/docs/#vrc-light-volumes")]
    public partial class LightVolumeAdaptor : VizVidBehaviour {
        [SerializeField, BindUdonSharpEvent(
            nameof(_OnTextureChanged),
            nameof(_OnTimeDrift)
        ), Locatable, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")]
        internal Core core;
#if VRC_LIGHT_VOLUMES
        [SerializeField, Resolve("/**")] LightVolumeManager lightVolumeManager;
        [SerializeField] internal LightVolumeInstance[] lightVolumes;
#if VRC_LIGHT_VOLUMES_V2
        [SerializeField] internal PointLightVolumeInstance[] pointLightVolumes;
#endif
#if VRC_LIGHT_VOLUMES_V3
        [SerializeField, HideInInspector] internal PointLightVolumeInstance[] cookiePointLightVolumes;
        [SerializeField, HideInInspector] internal bool needReadback;
#endif
#endif
        bool hasInitialized;
        Texture videoTexture;
        Color32[] pixels;
        bool isRunning;

        void OnEnable() {
#if VRC_LIGHT_VOLUMES
#if VRC_LIGHT_VOLUMES_V3
            if (needReadback) core.enableMipmap = true;
#else
            core.enableMipmap = true;
#endif
            SendCustomEventDelayedFrames(nameof(_OnTextureChanged), 0);
            if (hasInitialized) return;
            hasInitialized = true;
#else
            Debug.LogWarning("[LightVolumeAdaptor] VRC Light Volumes are not imported. Please import the package to use this feature.");
            enabled = false;
#endif
        }

        void OnDisable() => SetColor(Color.black);

        bool ShouldAutoUpdate() => !core.IsStatic && core.IsPlaying && !core.IsPaused;

        public override void OnVideoPlay() {
            if (!isRunning) _OnTextureChanged();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnTimeDrift() {
            if (!isRunning) _OnTextureChanged();
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _OnTextureChanged() {
#if VRC_LIGHT_VOLUMES_V3
            UpdateTextureCookie();
#endif
            if (!DoReadbackRequest() || isRunning) return;
            isRunning = true;
            SendCustomEventDelayedFrames(nameof(_DoReadbackRequest), 0);
        }

#if COMPILER_UDONSHARP
        public
#endif
        void _DoReadbackRequest() {
            if (DoReadbackRequest())
                SendCustomEventDelayedFrames(nameof(_DoReadbackRequest), 0);
            else
                isRunning = false;
        }

        bool DoReadbackRequest() {
#if VRC_LIGHT_VOLUMES_V3
            if (!needReadback) return false;
#endif
            if (!enabled || !gameObject.activeInHierarchy)
                return false;
            videoTexture = core.VideoTexture;
            if (!Utilities.IsValid(videoTexture)) {
                SetColor(Color.black);
                return false;
            }
            VRCAsyncGPUReadback.Request(videoTexture, videoTexture.mipmapCount - 1, TextureFormat.RGBA32, (IUdonEventReceiver)this);
            return ShouldAutoUpdate();
        }

        public override void OnAsyncGpuReadbackComplete(VRCAsyncGPUReadbackRequest request) {
            if (!enabled || !gameObject.activeInHierarchy) return;
            if (request.hasError || !Utilities.IsValid(videoTexture)) {
                SetColor(Color.black);
                return;
            }
            int dataSize = request.layerDataSize / 4; // Assuming Color32 is 4 bytes
            if (dataSize <= 0) {
                SetColor(Color.black);
                return;
            }
            if (!Utilities.IsValid(pixels) || pixels.Length < dataSize)
                pixels = new Color32[dataSize];
            if (!request.TryGetData(pixels)) {
                SetColor(Color.black);
                return;
            }
            SetColor(pixels[dataSize / 2]);
        }

        void SetColor(Color color) {
#if VRC_LIGHT_VOLUMES
            if (Utilities.IsValid(lightVolumes))
                foreach (var lightVolume in lightVolumes) {
                    if (!Utilities.IsValid(lightVolume)) continue;
                    lightVolume.Color = color;
                }
#if VRCLV2_IMPORTED
            if (Utilities.IsValid(pointLightVolumes))
                foreach (var pointLightVolume in pointLightVolumes) {
                    if (!Utilities.IsValid(pointLightVolume)) continue;
                    pointLightVolume.Color = color;
                }
#else
            else return;
            if (Utilities.IsValid(lightVolumeManager) &&
                !lightVolumeManager.AutoUpdateVolumes)
                lightVolumeManager.UpdateVolumes();
#endif
#endif
        }

#if VRC_LIGHT_VOLUMES_V3
        void UpdateTextureCookie() {
            if (!Utilities.IsValid(cookiePointLightVolumes)) return;
            var length = cookiePointLightVolumes.Length;
            if (length <= 0) return;
            bool autoUpdate = ShouldAutoUpdate();
            for (int i = 0; i < length; i++) {
                var pointLightVolume = cookiePointLightVolumes[i];
                if (Utilities.IsValid(pointLightVolume) &&
                    (!autoUpdate || pointLightVolume.AutoUpdateCustomTexture != autoUpdate))
                    pointLightVolume.SetCustomMaterial(pointLightVolume.CustomTextureMaterial, autoUpdate);
            }
        }
#endif
    }

#if !COMPILER_UDONSHARP
    partial class LightVolumeAdaptor : IVizVidCompoonent {
        Core IVizVidCompoonent.Core => core;
    }

#if UNITY_EDITOR && VRCLV3_IMPORTED
    partial class LightVolumeAdaptor : ISelfPreProcess {
        public int Priority => 0;

        public void PreProcess() {
            using (PooledObjectExtensions.Get(out List<ScreenConfigurator> screenConfigurators)) {
                foreach (var screenConfigurator in gameObject.scene.IterateAllComponents<ScreenConfigurator>()) {
                    if (screenConfigurator == null || screenConfigurator.core != core)
                        continue;
                    screenConfigurators.Add(screenConfigurator);
                }
                var scaleModeId = Shader.PropertyToID("_ScaleMode");
                var stereoShiftId = Shader.PropertyToID("_StereoShift");
                var stereoExtendId = Shader.PropertyToID("_StereoExtend");
                var aspectRatioId = Shader.PropertyToID("_AspectRatio");
                if (screenConfigurators.Count > 0)
                    using (PooledObjectExtensions.Get(out HashSet<PointLightVolumeInstance> cookiePLV, screenConfigurators.Count))
                    using (PooledObjectExtensions.Get(out HashSet<PointLightVolumeInstance> dynamicCookiePLV, screenConfigurators.Count)) {
                        foreach (var configurator in screenConfigurators) {
                            if (configurator == null || configurator.coreIndex < 0)
                                continue;
#if VRCLV3_EARLY_VERSION
                            var pointLightVolume = configurator.pointLightVolume;
                            if (pointLightVolume == null)
                                continue;
                            var pointLightVolumeInstance = pointLightVolume.PointLightVolumeInstance;
#else
                            var pointLightVolumeInstance = configurator.pointLightVolume;
#endif
                            if (pointLightVolumeInstance == null || !cookiePLV.Add(pointLightVolumeInstance)) continue;
                            var material = pointLightVolumeInstance.CustomTextureMaterial;
                            if (material == null) continue;
                            var targetMaterials = configurator.Renderer.sharedMaterials;
                            var targetIndex = core.screenTargetIndeces[configurator.coreIndex];
                            if (targetIndex >= 0 && targetIndex < targetMaterials.Length) {
                                var targetMaterial = targetMaterials[targetIndex];
                                if (targetMaterial != null) {
                                    ShaderUtils.CopyMaterialIntegerProperty(material, targetMaterial, scaleModeId);
                                    ShaderUtils.CopyMaterialVectorProperty(material, targetMaterial, stereoShiftId);
                                    ShaderUtils.CopyMaterialVectorProperty(material, targetMaterial, stereoExtendId);
                                    ShaderUtils.CopyMaterialFloatProperty(material, targetMaterial, aspectRatioId);
                                }
                            } else {
                                bool hasScaleMode = false, hasStereoShift = false, hasStereoExtend = false, hasAspectRatio = false;
                                foreach (var targetMaterial in targetMaterials) {
                                    if (targetMaterial == null) continue;
                                    if (!hasScaleMode) hasScaleMode = ShaderUtils.CopyMaterialIntegerProperty(material, targetMaterial, scaleModeId);
                                    if (!hasStereoShift) hasStereoShift = ShaderUtils.CopyMaterialVectorProperty(material, targetMaterial, stereoShiftId);
                                    if (!hasStereoExtend) hasStereoExtend = ShaderUtils.CopyMaterialVectorProperty(material, targetMaterial, stereoExtendId);
                                    if (!hasAspectRatio) hasAspectRatio = ShaderUtils.CopyMaterialFloatProperty(material, targetMaterial, aspectRatioId);
                                }
                            }
                            core.AddScreen(
                                material, 0, -1,
                                "_VideoTex",
                                "_IsAVProVideo",
                                core.screenTargetDefaultTextures[configurator.coreIndex],
                                Vector4.zero
                            );
                            var defaultTexture = configurator.core.screenTargetDefaultTextures[configurator.coreIndex];
                            pointLightVolumeInstance.AutoUpdateCustomTexture = defaultTexture != null && defaultTexture is RenderTexture;
                            if (!pointLightVolumeInstance.AutoUpdateCustomTexture) dynamicCookiePLV.Add(pointLightVolumeInstance);
                        }
                        cookiePointLightVolumes = new PointLightVolumeInstance[dynamicCookiePLV.Count];
                        dynamicCookiePLV.CopyTo(cookiePointLightVolumes);
                    }
            }
            if (lightVolumes == null)
                lightVolumes = Array.Empty<LightVolumeInstance>();
            else if (lightVolumes.Length > 0)
                using (PooledObjectExtensions.Get(out HashSet<LightVolumeInstance> lv, lightVolumes.Length)) {
                    foreach (var l in lightVolumes) if (l != null) lv.Add(l);
                    lightVolumes = new LightVolumeInstance[lv.Count];
                    lv.CopyTo(lightVolumes);
                }
            if (pointLightVolumes == null)
                pointLightVolumes = Array.Empty<PointLightVolumeInstance>();
            else if (pointLightVolumes.Length > 0)
                using (PooledObjectExtensions.Get(out HashSet<PointLightVolumeInstance> plv, pointLightVolumes.Length)) {
                    foreach (var pl in pointLightVolumes) if (pl != null) plv.Add(pl);
                    plv.ExceptWith(cookiePointLightVolumes);
                    pointLightVolumes = new PointLightVolumeInstance[plv.Count];
                    plv.CopyTo(pointLightVolumes);
                }
            needReadback = lightVolumes.Length > 0 || pointLightVolumes.Length > 0;
        }
    }
#endif
#endif
}
