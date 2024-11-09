using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.SDK3.Data;
using JLChnToZ.VRC.Foundation.I18N;

namespace JLChnToZ.VRC.VVMW {
    public partial class Core {
        Vector4 normalST = new Vector4(1, 1, 0, 0), flippedST = new Vector4(1, -1, 0, 1);
        Rect normalRect = new Rect(0, 0, 1, 1), flippedRect = new Rect(0, 1, 1, -1);
        [SerializeField, LocalizedLabel] Texture defaultTexture;
        [SerializeField] Object[] screenTargets;
        [SerializeField] int[] screenTargetModes;
        [SerializeField] int[] screenTargetIndeces;
        [SerializeField] string[] screenTargetPropertyNames, avProPropertyNames;
        [SerializeField] Texture[] screenTargetDefaultTextures;
        [SerializeField, LocalizedLabel] bool broadcastScreenTexture;
        [SerializeField, LocalizedLabel] string broadcastScreenTextureName = "_Udon_VideoTex";
        int[] screenTargetPropertyIds, avProPropertyIds;
        MaterialPropertyBlock screenTargetPropertyBlock;
        int broadcastTextureId;
        DataDictionary screenSharedProperties;

        public Texture VideoTexture => activeHandler != null ? activeHandler.Texture : null;

        void StartBroadcastScreenTexture() {
            if (!broadcastScreenTexture) return;
            if (broadcastTextureId == 0) broadcastTextureId = VRCShader.PropertyToID(broadcastScreenTextureName);
            var videoTexture = VideoTexture;
            if (videoTexture != null) VRCShader.SetGlobalTexture(broadcastTextureId, videoTexture);
        }

        void StopBroadcastScreenTexture() {
            if (broadcastScreenTexture) VRCShader.SetGlobalTexture(broadcastTextureId, null);
        }

        void InitScreenProperties() {
            if (screenTargetPropertyNames != null) {
                screenTargetPropertyIds = new int[screenTargetPropertyNames.Length];
                for (int i = 0; i < screenTargetPropertyNames.Length; i++) {
                    var propertyName = screenTargetPropertyNames[i];
                    if (!string.IsNullOrEmpty(propertyName) && propertyName != "_")
                        screenTargetPropertyIds[i] = VRCShader.PropertyToID(propertyName);
                }
            }
            if (avProPropertyNames != null) {
                avProPropertyIds = new int[avProPropertyNames.Length];
                for (int i = 0; i < avProPropertyNames.Length; i++) {
                    if ((screenTargetModes[i] & 0x8) != 0)
                        avProPropertyIds[i] = VRCShader.PropertyToID(screenTargetPropertyNames[i] + "_ST");
                    else {
                        var propertyName = avProPropertyNames[i];
                        if (!string.IsNullOrEmpty(propertyName) && propertyName != "_")
                            avProPropertyIds[i] = VRCShader.PropertyToID(propertyName);
                    }
                }
            }
        }

        public void _OnTextureChanged() {
            var videoTexture = VideoTexture;
            var hasVideoTexture = videoTexture != null;
            var isAvPro = hasVideoTexture && IsAVPro;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                Texture texture = null;
                if (hasVideoTexture)
                    texture = videoTexture;
                else if (screenTargetDefaultTextures != null && i < screenTargetDefaultTextures.Length)
                    texture = screenTargetDefaultTextures[i];
                if (texture == null) texture = defaultTexture;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        SetTextureToMaterial(texture, (Material)screenTargets[i], i, isAvPro);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) renderer.GetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        if (screenTargetPropertyIds[i] != 0)
                            screenTargetPropertyBlock.SetTexture(screenTargetPropertyIds[i], texture);
                        if (avProPropertyIds[i] != 0) {
                            if ((screenTargetModes[i] & 0x8) != 0)
                            #if UNITY_STANDALONE_WIN
                                screenTargetPropertyBlock.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
                            #else
                                {} // Do nothing
                            #endif
                            else
                                screenTargetPropertyBlock.SetInt(avProPropertyIds[i], isAvPro ? 1 : 0);
                        }
                        if (index < 0) renderer.SetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.SetPropertyBlock(screenTargetPropertyBlock, index);
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        SetTextureToMaterial(texture, material, i, isAvPro);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        SetTextureToMaterial(texture, material, i, isAvPro);
                        break;
                    }
                    case 4: { // UI RawImage
                        var rawImage = (RawImage)screenTargets[i];
                        rawImage.texture = texture;
                        #if UNITY_STANDALONE_WIN
                        rawImage.uvRect = isAvPro ? flippedRect : normalRect;
                        #endif
                        break;
                    }
                }
            }
            if (broadcastScreenTexture) VRCShader.SetGlobalTexture(broadcastTextureId, videoTexture);
            UpdateRealtimeGI();
            SendEvent("_OnTextureChanged");
        }

        public float GetScreenFloatExtra(int id) {
            if (screenSharedProperties != null && screenSharedProperties.TryGetValue(id, TokenType.Float, out var value))
                return value.Float;
            float v = 0;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        v = ((Material)screenTargets[i]).GetFloat(id);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) {
                            renderer.GetPropertyBlock(screenTargetPropertyBlock);
                            if (screenTargetPropertyBlock.HasFloat(id)) {
                                v = screenTargetPropertyBlock.GetFloat(id);
                                break;
                            }
                            foreach (var m in renderer.sharedMaterials)
                                if (m.HasProperty(id)) {
                                    v = m.GetFloat(id);
                                    break;
                                }
                            break;
                        }
                        renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        if (screenTargetPropertyBlock.HasFloat(id)) {
                            v = screenTargetPropertyBlock.GetFloat(id);
                            break;
                        }
                        var material = renderer.sharedMaterials[index];
                        if (material.HasProperty(id)) {
                            v = material.GetFloat(id);
                            break;
                        }
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        v = material.GetFloat(id);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        v = material.GetFloat(id);
                        break;
                    }
                }
            }
            if (screenSharedProperties == null) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = v;
            return v;
        }

        public void SetScreenFloatExtra(int id, float value) {
            if (screenSharedProperties == null) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = value;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (screenTargets[i] == null) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        ((Material)screenTargets[i]).SetFloat(id, value);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (screenTargetPropertyBlock == null) screenTargetPropertyBlock = new MaterialPropertyBlock();
                        if (index < 0) renderer.GetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.GetPropertyBlock(screenTargetPropertyBlock, index);
                        screenTargetPropertyBlock.SetFloat(id, value);
                        if (index < 0) renderer.SetPropertyBlock(screenTargetPropertyBlock);
                        else renderer.SetPropertyBlock(screenTargetPropertyBlock, index);
                        break;
                    }
                    case 2: { // Renderer (Shared Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.sharedMaterial : renderer.sharedMaterials[index];
                        material.SetFloat(id, value);
                        break;
                    }
                    case 3: { // Renderer (Cloned Material)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        var material = index < 0 ? renderer.material : renderer.materials[index];
                        material.SetFloat(id, value);
                        break;
                    }
                }
            }
            SendEvent("_OnScreenSharedPropertiesChanged");
        }

        void SetTextureToMaterial(Texture texture, Material material, int i, bool isAvPro) {
            if (screenTargetPropertyIds[i] != 0)
                material.SetTexture(screenTargetPropertyIds[i], texture);
            if (avProPropertyIds[i] != 0) {
                if ((screenTargetModes[i] & 0x8) != 0)
                #if UNITY_STANDALONE_WIN
                    material.SetVector(avProPropertyIds[i], isAvPro ? flippedST : normalST);
                #else
                    {} // Do nothing
                #endif
                else
                    material.SetInt(avProPropertyIds[i], isAvPro ? 1 : 0);
            }
        }
    }
}
