using UnityEngine;
using VRC.SDKBase;
using VRC.SDK3.Rendering;
using VRC.Udon.Common.Interfaces;
using UdonSharp;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N;

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
        ), Locatable, LocalizedLabel(Key = "JLChnToZ.VRC.VVMW.Core")] internal Core core;
#if VRC_LIGHT_VOLUMES
        [SerializeField, Resolve("/**")] LightVolumeManager lightVolumeManager;
        [SerializeField] internal LightVolumeInstance[] lightVolumes;
#if VRC_LIGHT_VOLUMES_V2
        [SerializeField] internal PointLightVolumeInstance[] pointLightVolumes;
#endif
#endif
        Texture videoTexture;
        Color32[] pixels;
        bool isRunning;

        void OnEnable() {
#if VRC_LIGHT_VOLUMES
            core.enableMipmap = true;
            _OnTextureChanged();
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
            if (!enabled || !gameObject.activeInHierarchy)
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
#if VRC_LIGHT_VOLUMES
            if (Utilities.IsValid(lightVolumes))
                foreach (var lightVolume in lightVolumes) {
                    if (!Utilities.IsValid(lightVolume)) continue;
                    lightVolume.Color = color;
                }
#if VRC_LIGHT_VOLUMES_V2
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
    }

#if !COMPILER_UDONSHARP
    partial class LightVolumeAdaptor : IVizVidCompoonent {
        Core IVizVidCompoonent.Core => core;
    }
#endif
}