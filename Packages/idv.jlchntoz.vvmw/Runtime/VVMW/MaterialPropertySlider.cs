using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using JLChnToZ.VRC.Foundation;

namespace JLChnToZ.VRC.VVMW {
    [UdonBehaviourSyncMode(BehaviourSyncMode.NoVariableSync)]
    public class MaterialPropertySlider : UdonSharpBehaviour {
        [BindEvent(nameof(Slider.onValueChanged), nameof(_OnSliderChanged))]
        [SerializeField] Slider slider;
        [SerializeField] Material targetMaterial;
        [SerializeField] Renderer targetRenderer;
        [Tooltip("The target material index of the renderer, -1 to set per-renderer properties.")]
        [SerializeField] int rendererMaterialIndex = -1;
        [SerializeField] string propertyName;
        int properlyNameID;
        MaterialPropertyBlock propertyBlock;

        float PropertyValue {
            get {
                if (Utilities.IsValid(targetRenderer)) {
                    if (!Utilities.IsValid(propertyBlock)) propertyBlock = new MaterialPropertyBlock();
                    if (rendererMaterialIndex >= 0)
                        targetRenderer.GetPropertyBlock(propertyBlock, rendererMaterialIndex);
                    else
                        targetRenderer.GetPropertyBlock(propertyBlock);
                    if (propertyBlock.HasFloat(properlyNameID))
                        return propertyBlock.GetFloat(properlyNameID);
                    if (rendererMaterialIndex < 0) {
                        foreach (var mat in targetRenderer.sharedMaterials)
                            if (Utilities.IsValid(mat) && mat.HasProperty(properlyNameID))
                                return mat.GetFloat(properlyNameID);
                    } else {
                        var mat = targetRenderer.sharedMaterials[rendererMaterialIndex];
                        if (Utilities.IsValid(mat) && mat.HasProperty(properlyNameID))
                            return mat.GetFloat(properlyNameID);
                    }
                } else if (Utilities.IsValid(targetMaterial))
                    return targetMaterial.GetFloat(properlyNameID);
                return float.NaN;
            }
            set {
                if (Utilities.IsValid(targetRenderer)) {
                    if (!Utilities.IsValid(propertyBlock)) propertyBlock = new MaterialPropertyBlock();
                    if (rendererMaterialIndex < 0)
                        targetRenderer.GetPropertyBlock(propertyBlock);
                    else
                        targetRenderer.GetPropertyBlock(propertyBlock, rendererMaterialIndex);
                    propertyBlock.SetFloat(properlyNameID, value);
                    if (rendererMaterialIndex < 0)
                        targetRenderer.SetPropertyBlock(propertyBlock);
                    else
                        targetRenderer.SetPropertyBlock(propertyBlock, rendererMaterialIndex);
                } else if (Utilities.IsValid(targetMaterial))
                    targetMaterial.SetFloat(properlyNameID, value);
            }
        }

        void Start() {
            properlyNameID = VRCShader.PropertyToID(propertyName);
            if (!Utilities.IsValid(targetMaterial) && !Utilities.IsValid(targetRenderer)) {
                targetRenderer = GetComponent<Renderer>();
                if (!Utilities.IsValid(targetRenderer)) {
                    Debug.LogError("[VVMW] No target material or renderer specified.");
                    return;
                }
            }
            var value = PropertyValue;
            if (!float.IsNaN(value)) slider.SetValueWithoutNotify(value);
        }

        public void _OnSliderChanged() {
            PropertyValue = slider.value;
        }
    }
}