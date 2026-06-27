using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;
using VRC.SDK3.Data;

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
        [SerializeField, HideInInspector] internal bool hasLightVolumes = true;
        [SerializeField, HideInInspector] internal bool useCookie = true;
        [SerializeField] internal int[] screenTargetIDs;
        int mainTexId, scaleModeId, stereoShiftId, stereoExtendId, aspectRatioId, emissionIntensityId;
        MaterialPropertyBlock mpb;
#endif
#endif
        bool hasInitialized;
        Texture videoTexture;
        Color32[] pixels;
        bool isRunning;
        bool needReadback;

        void OnEnable() {
#if VRC_LIGHT_VOLUMES
            core.enableMipmap = true;
            SendCustomEventDelayedFrames(nameof(_SyncAllScreenTargets), 0);
            SendCustomEventDelayedFrames(nameof(_OnTextureChanged), 0);
            if (hasInitialized) return;
            hasInitialized = true;
#if VRC_LIGHT_VOLUMES_V3
            mainTexId = VRCShader.PropertyToID("_MainTex");
            scaleModeId = VRCShader.PropertyToID("_ScaleMode");
            stereoShiftId = VRCShader.PropertyToID("_StereoShift");
            stereoExtendId = VRCShader.PropertyToID("_StereoExtend");
            aspectRatioId = VRCShader.PropertyToID("_AspectRatio");
            emissionIntensityId = VRCShader.PropertyToID("_EmissionIntensity");
            mpb = new MaterialPropertyBlock();
#endif
#else
            Debug.LogWarning("[LightVolumeAdaptor] VRC Light Volumes are not imported. Please import the package to use this feature.");
            enabled = false;
#endif
        }

        void OnDisable() => SetColor(Color.black);

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
            if (!isRunning) needReadback = true;
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
            if (!enabled || !gameObject.activeInHierarchy || !needReadback)
                return false;
            videoTexture = core.VideoTexture;
            if (!Utilities.IsValid(videoTexture)) {
                SetColor(Color.black);
                return false;
            }
            VRCAsyncGPUReadback.Request(videoTexture, videoTexture.mipmapCount - 1, TextureFormat.RGBA32, (IUdonEventReceiver)this);
            return !core.IsStatic && core.IsPlaying && !core.IsPaused;
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
            needReadback = false;
#if VRC_LIGHT_VOLUMES
            if (Utilities.IsValid(lightVolumes))
                foreach (var lightVolume in lightVolumes) {
                    if (!Utilities.IsValid(lightVolume)) continue;
                    lightVolume.Color = color;
                    needReadback = true;
                }
#if VRC_LIGHT_VOLUMES_V2
            if (Utilities.IsValid(pointLightVolumes))
                foreach (var pointLightVolume in pointLightVolumes) {
                    if (!Utilities.IsValid(pointLightVolume)) continue;
#if VRC_LIGHT_VOLUMES_V3
                    if (Utilities.IsValid(pointLightVolume.CustomTextureMaterial)) continue;
#endif
                    pointLightVolume.Color = color;
                    needReadback = true;
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

        public void SyncScreenTarget(int id) {
            if (id < 0 || id >= pointLightVolumes.Length) return;
            SyncScreenTargetUnchecked(id);
        }

        public void _SyncAllScreenTargets() {
            for (int i = 0, count = pointLightVolumes.Length; i < count; i++)
                SyncScreenTargetUnchecked(i);
        }

        void SyncScreenTargetUnchecked(int id) {
            var destLv = pointLightVolumes[id];
            if (!Utilities.IsValid(destLv)) return;
            var dest = destLv.CustomTextureMaterial;
            if (!Utilities.IsValid(dest)) return;
            id = screenTargetIDs[id];
            if (id < 0) return;
            var src = core.screenTargets[id];
            if (!Utilities.IsValid(src)) return;
            switch (core.screenTargetModes[id] & 0x7) {
                case 0: {
                        var material = (Material)src;
                        ShaderUtils.CopyMaterialIntegerProperty(material, dest, scaleModeId);
                        ShaderUtils.CopyMaterialVectorProperty(material, dest, stereoShiftId);
                        ShaderUtils.CopyMaterialVectorProperty(material, dest, stereoExtendId);
                        ShaderUtils.CopyMaterialFloatProperty(material, dest, aspectRatioId);
                        ShaderUtils.CopyMaterialFloatProperty(material, dest, emissionIntensityId);
                    }
                    break;
                case 1: {
                        var renderer = (Renderer)src;
                        var materials = renderer.sharedMaterials;
                        int index = core.screenTargetIndeces[id];
                        mpb.Clear();
                        if (index < 0) {
                            renderer.GetPropertyBlock(mpb);
                            ShaderUtils.CopyMaterialIntegerProperty(mpb, materials, dest, scaleModeId);
                            ShaderUtils.CopyMaterialVectorProperty(mpb, materials, dest, stereoShiftId);
                            ShaderUtils.CopyMaterialVectorProperty(mpb, materials, dest, stereoExtendId);
                            ShaderUtils.CopyMaterialFloatProperty(mpb, materials, dest, aspectRatioId);
                            ShaderUtils.CopyMaterialFloatProperty(mpb, materials, dest, emissionIntensityId);
                        } else {
                            renderer.GetPropertyBlock(mpb, index);
                            var material = materials[index];
                            ShaderUtils.CopyMaterialIntegerProperty(mpb, material, dest, scaleModeId);
                            ShaderUtils.CopyMaterialVectorProperty(mpb, material, dest, stereoShiftId);
                            ShaderUtils.CopyMaterialVectorProperty(mpb, material, dest, stereoExtendId);
                            ShaderUtils.CopyMaterialFloatProperty(mpb, material, dest, aspectRatioId);
                            ShaderUtils.CopyMaterialFloatProperty(mpb, material, dest, emissionIntensityId);
                        }
                    }
                    break;
                case 2:
                case 3: {
                        var renderer = (Renderer)src;
                        int index = core.screenTargetIndeces[id];
                        var material = renderer.sharedMaterials[index];
                        ShaderUtils.CopyMaterialIntegerProperty(material, dest, scaleModeId);
                        ShaderUtils.CopyMaterialVectorProperty(material, dest, stereoShiftId);
                        ShaderUtils.CopyMaterialVectorProperty(material, dest, stereoExtendId);
                        ShaderUtils.CopyMaterialFloatProperty(material, dest, aspectRatioId);
                        ShaderUtils.CopyMaterialFloatProperty(material, dest, emissionIntensityId);
                    }
                    break;
            }
        }

        void UpdateTextureCookie() {
            if (Utilities.IsValid(pointLightVolumes)) {
                bool autoUpdate = !core.IsStatic && core.IsPlaying && !core.IsPaused;
                var texture = core.VideoTexture;
                foreach (var pointLightVolume in pointLightVolumes) {
                    if (!Utilities.IsValid(pointLightVolume)) continue;
                    var material = pointLightVolume.CustomTextureMaterial;
                    if (!Utilities.IsValid(material)) continue;
                    material.SetTexture(mainTexId, texture);
                    pointLightVolume.SetCustomMaterial(material, autoUpdate);
                }
            }
        }
#endif
    }

#if !COMPILER_UDONSHARP
    partial class LightVolumeAdaptor : IVizVidCompoonent {
        Core IVizVidCompoonent.Core => core;
    }

#if UNITY_EDITOR && VRC_LIGHT_VOLUMES_V3
    partial class LightVolumeAdaptor : ISelfPreProcess {
        public int Priority => 0;

        public void PreProcess() {
        }
    }
#endif
#endif
}