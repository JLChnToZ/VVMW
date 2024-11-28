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
        [SerializeField] internal Object[] screenTargets;
        [SerializeField] internal int[] screenTargetModes;
        [SerializeField] internal int[] screenTargetIndeces;
        [SerializeField] internal string[] screenTargetPropertyNames, avProPropertyNames;
        [SerializeField] internal Texture[] screenTargetDefaultTextures;
        [SerializeField, LocalizedLabel] bool broadcastScreenTexture;
        [SerializeField, LocalizedLabel] string broadcastScreenTextureName = "_Udon_VideoTex";
        int[] screenTargetPropertyIds, avProPropertyIds;
        MaterialPropertyBlock screenTargetPropertyBlock;
        int broadcastTextureId;
        DataDictionary screenSharedProperties;

        /// <summary>
        /// The texture of the video.
        /// </summary>
        public Texture VideoTexture => Utilities.IsValid(activeHandler) ? activeHandler.Texture : null;

        void StartBroadcastScreenTexture() {
            if (!broadcastScreenTexture) return;
            if (broadcastTextureId == 0) broadcastTextureId = VRCShader.PropertyToID(broadcastScreenTextureName);
            var videoTexture = VideoTexture;
            if (Utilities.IsValid(videoTexture)) VRCShader.SetGlobalTexture(broadcastTextureId, videoTexture);
        }

        void StopBroadcastScreenTexture() {
            if (broadcastScreenTexture) VRCShader.SetGlobalTexture(broadcastTextureId, null);
        }

        void InitScreenProperties() {
            if (Utilities.IsValid(screenTargetPropertyNames)) {
                screenTargetPropertyIds = new int[screenTargetPropertyNames.Length];
                for (int i = 0; i < screenTargetPropertyNames.Length; i++) {
                    var propertyName = screenTargetPropertyNames[i];
                    if (!string.IsNullOrEmpty(propertyName) && propertyName != "_")
                        screenTargetPropertyIds[i] = VRCShader.PropertyToID(propertyName);
                }
            }
            if (Utilities.IsValid(avProPropertyNames)) {
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

#if COMPILER_UDONSHARP
        public
#else
        internal
#endif
        void _OnTextureChanged() {
            var videoTexture = VideoTexture;
            var hasVideoTexture = Utilities.IsValid(videoTexture);
            var isAvPro = hasVideoTexture && IsAVPro;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (!Utilities.IsValid(screenTargets[i])) continue;
                Texture texture = null;
                if (hasVideoTexture)
                    texture = videoTexture;
                else if (Utilities.IsValid(screenTargetDefaultTextures) && i < screenTargetDefaultTextures.Length)
                    texture = screenTargetDefaultTextures[i];
                if (!Utilities.IsValid(texture)) texture = defaultTexture;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        SetTextureToMaterial(texture, (Material)screenTargets[i], i, isAvPro);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (!Utilities.IsValid(screenTargetPropertyBlock)) screenTargetPropertyBlock = new MaterialPropertyBlock();
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

        /// <summary>
        /// Get the shader property of the screen.
        /// </summary>
        /// <param name="id">The ID of the property. This is obtained by <see cref="VRCShader.PropertyToID(string)"/></param>
        /// <returns>The value of the property.</returns>
        public float GetScreenFloatExtra(int id) {
            if (Utilities.IsValid(screenSharedProperties) && screenSharedProperties.TryGetValue(id, TokenType.Float, out var value))
                return value.Float;
            float v = 0;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (!Utilities.IsValid(screenTargets[i])) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        v = ((Material)screenTargets[i]).GetFloat(id);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (!Utilities.IsValid(screenTargetPropertyBlock)) screenTargetPropertyBlock = new MaterialPropertyBlock();
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
            if (!Utilities.IsValid(screenSharedProperties)) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = v;
            return v;
        }

        /// <summary>
        /// Set the shader property of the screen.
        /// </summary>
        /// <param name="id">The ID of the property. This is obtained by <see cref="VRCShader.PropertyToID(string)"/></param>
        /// <param name="value">The value of the property.</param>
        public void SetScreenFloatExtra(int id, float value) {
            if (!Utilities.IsValid(screenSharedProperties)) screenSharedProperties = new DataDictionary();
            screenSharedProperties[id] = value;
            for (int i = 0, length = screenTargets.Length; i < length; i++) {
                if (!Utilities.IsValid(screenTargets[i])) continue;
                switch (screenTargetModes[i] & 0x7) {
                    case 0: { // Material
                        ((Material)screenTargets[i]).SetFloat(id, value);
                        break;
                    }
                    case 1: { // Renderer (Property Block)
                        var renderer = (Renderer)screenTargets[i];
                        int index = screenTargetIndeces[i];
                        if (!Utilities.IsValid(screenTargetPropertyBlock)) screenTargetPropertyBlock = new MaterialPropertyBlock();
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

#if !COMPILER_UDONSHARP
        void DrawScreenGizmos() {
            for (int i = 0; i < screenTargets.Length; i++) {
                Gizmos.color = Color.HSVToRGB(i * 0.35F % 1F, 1F, 1F);
                if (screenTargets[i] is MeshRenderer meshRenderer) {
                    if (meshRenderer.TryGetComponent(out MeshFilter meshFilter)) {
                        var mesh = meshFilter.sharedMesh;
                        if (mesh != null) {
                            Gizmos.matrix = meshFilter.transform.localToWorldMatrix;
                            Gizmos.DrawWireMesh(mesh, screenTargetIndeces[i]);
                        }
                    }
                    continue;
                }
                if (screenTargets[i] is Renderer renderer) {
                    var bounds = renderer.localBounds;
                    Gizmos.matrix = renderer.transform.localToWorldMatrix;
                    Gizmos.DrawWireCube(bounds.center, bounds.size);
                    continue;
                }
            }
        }
#endif
    }
}
