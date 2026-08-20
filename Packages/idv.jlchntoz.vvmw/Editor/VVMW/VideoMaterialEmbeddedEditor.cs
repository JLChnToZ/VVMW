using System;
using UnityEngine;
using UnityEngine.Rendering;
using JLChnToZ.VRC.VVMW.Designer;

namespace JLChnToZ.VRC.VVMW.Editors {
    public class VideoMaterialEmbeddedEditor {
        static bool hasPropertyIdInitialized;
        static int mirrorFlipId;
        static int renderModeId;
        static int emissionIntensityId;
        static int scaleModeId;
        readonly Renderer renderer;
        Material[] materials;

        public Material[] Materials => materials;

        public bool MirrorFlip {
            get {
                TryGetMirrorFlip(out var result);
                return result;
            }
            set {
                if (materials == null) return;
                int convertedValue = value ? 1 : 0;
                if (renderer == null) {
                    for (int i = 0; i < materials.Length; i++) {
                        ref var material = ref materials[i];
                        if (material == null || !material.HasProperty(mirrorFlipId) || material.GetInt(mirrorFlipId) == convertedValue)
                            continue;
                        bool requireSave = !MaterialUtil.IsMaterialSafeToModify(material);
                        if (requireSave) material = MaterialUtil.CreateGeneratedAlias(material, new [] { mirrorFlipId });
                        material.SetInt(mirrorFlipId, convertedValue);
                        if (requireSave) MaterialUtil.SaveMaterialAsAsset(material, null, "_MirrorFlip", true);
                    }
                    return;
                }
                ScreenMeshUtils.TryFixupMaterialProperties(new [] { renderer }, new [] {
                    new ScreenMeshUtils.MaterialPropertyOverride(mirrorFlipId, ShaderPropertyType.Int, convertedValue)
                });
            }
        }

        public VRCMirrorModeFlag RenderMode {
            get {
                TryGetRenderMode(out var result);
                return result;
            }
            set {
                if (materials == null) return;
                if (renderer == null) {
                    for (int i = 0; i < materials.Length; i++) {
                        ref var material = ref materials[i];
                        if (material == null || !material.HasProperty(renderModeId) || material.GetInt(renderModeId) == (int)value)
                            continue;
                        bool requireSave = !MaterialUtil.IsMaterialSafeToModify(material);
                        if (requireSave) material = MaterialUtil.CreateGeneratedAlias(material, new [] { renderModeId });
                        material.SetInt(renderModeId, (int)value);
                        if (requireSave) MaterialUtil.SaveMaterialAsAsset(material, null, "_RenderMode", true);
                    }
                    return;
                }
                ScreenMeshUtils.TryFixupMaterialProperties(new [] { renderer }, new [] {
                    new ScreenMeshUtils.MaterialPropertyOverride(renderModeId, ShaderPropertyType.Int, (int)value)
                });
            }
        }

        public float EmissionIntensity {
            get {
                TryGetEmissionIntensity(out var result);
                return result;
            }
            set {
                if (materials == null) return;
                if (renderer == null) {
                    for (int i = 0; i < materials.Length; i++) {
                        ref var material = ref materials[i];
                        if (material == null || !material.HasProperty(emissionIntensityId) || Mathf.Approximately(material.GetFloat(emissionIntensityId), value))
                            continue;
                        bool requireSave = !MaterialUtil.IsMaterialSafeToModify(material);
                        if (requireSave) material = MaterialUtil.CreateGeneratedAlias(material, new [] { emissionIntensityId });
                        material.SetFloat(emissionIntensityId, value);
                        if (requireSave) MaterialUtil.SaveMaterialAsAsset(material, null, "_EmissionIntensity", true);
                    }
                    return;
                }
                ScreenMeshUtils.TryFixupMaterialProperties(new [] { renderer }, new [] {
                    new ScreenMeshUtils.MaterialPropertyOverride(emissionIntensityId, ShaderPropertyType.Float, value)
                });
            }
        }

        public ScaleMode ScaleMode {
            get {
                TryGetScaleMode(out var result);
                return result;
            }
            set {
                if (materials == null) return;
                if (renderer == null) {
                    for (int i = 0; i < materials.Length; i++) {
                        ref var material = ref materials[i];
                        if (material == null || !material.HasProperty(scaleModeId) || material.GetInt(scaleModeId) == (int)value)
                            continue;
                        bool requireSave = !MaterialUtil.IsMaterialSafeToModify(material);
                        if (requireSave) material = MaterialUtil.CreateGeneratedAlias(material, new [] { scaleModeId });
                        material.SetInt(scaleModeId, (int)value);
                        if (requireSave) MaterialUtil.SaveMaterialAsAsset(material, null, "_ScaleMode", true);
                    }
                    return;
                }
                ScreenMeshUtils.TryFixupMaterialProperties(new [] { renderer }, new [] {
                    new ScreenMeshUtils.MaterialPropertyOverride(scaleModeId, ShaderPropertyType.Int, (int)value)
                });
            }
        }
        
        static void InitializePropertyIds() {
            if (hasPropertyIdInitialized) return;
            mirrorFlipId = Shader.PropertyToID("_IsMirror");
            renderModeId = Shader.PropertyToID("_RenderMode");
            emissionIntensityId = Shader.PropertyToID("_EmissionIntensity");
            scaleModeId = Shader.PropertyToID("_ScaleMode");
            hasPropertyIdInitialized = true;
        }

        public VideoMaterialEmbeddedEditor(Renderer renderer) {
            this.renderer = renderer;
            InitializePropertyIds();
            RefreshMaterials();
        }

        public VideoMaterialEmbeddedEditor(params Material[] materials) {
            this.materials = materials;
            InitializePropertyIds();
        }

        void RefreshMaterials() {
            if (renderer == null) return;
            materials = renderer.sharedMaterials;
        }

        public bool TryGetMirrorFlip(out bool mirrorFlip) {
            mirrorFlip = false;
            if (materials == null) return false;
            foreach (var material in materials) {
                if (material == null) continue;
                if (!material.HasProperty(mirrorFlipId)) continue;
                mirrorFlip = material.GetInt(mirrorFlipId) != 0;
                return true;
            }
            return false;
        }

        public bool TryGetRenderMode(out VRCMirrorModeFlag renderMode) {
            renderMode = VRCMirrorModeFlag.None;
            if (materials == null) return false;
            foreach (var material in materials) {
                if (material == null) continue;
                if (!material.HasProperty(renderModeId)) continue;
                renderMode = (VRCMirrorModeFlag)material.GetInt(renderModeId);
                return true;
            }
            return false;
        }

        public bool TryGetEmissionIntensity(out float emissionIntensity) {
            emissionIntensity = 0f;
            if (materials == null) return false;
            int materialCount = 0;
            foreach (var material in materials) {
                if (material == null) continue;
                if (!material.HasProperty(emissionIntensityId)) continue;
                emissionIntensity += material.GetFloat(emissionIntensityId);
                materialCount++;
            }
            if (materialCount == 0) return false;
            emissionIntensity /= materialCount;
            return true;
        }

        public bool TryGetScaleMode(out ScaleMode scaleMode) {
            scaleMode = ScaleMode.Stretch;
            if (materials == null) return false;
            foreach (var material in materials) {
                if (material == null) continue;
                if (!material.HasProperty(scaleModeId)) continue;
                scaleMode = (ScaleMode)material.GetInt(scaleModeId);
                return true;
            }
            return false;
        }
    }

    [Flags]
    public enum VRCMirrorModeFlag {
        None = 0,
        DirectLook = 0x1,
        VRHandheldCamera = 0x2,
        DesktopHandheldCamera = 0x4,
        Screenshot = 0x8,
        VRMirror = 0x10,
        VRHandheldCameraInMirror = 0x20,
        VRFaceMirror = 0x40,
        VRScreenshotInMirror = 0x80,
        DesktopMirror = 0x100,
        DesktopFaceMirror = 0x200,
        DesktopHandheldCameraInMirror = 0x400,
        DesktopScreenshotInMirror = 0x800,
    }

    public enum ScaleMode {
        Stretch,
        Contain,
        Cover,
    }
}