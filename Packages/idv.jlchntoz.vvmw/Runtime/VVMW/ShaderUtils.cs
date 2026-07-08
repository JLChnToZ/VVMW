using UnityEngine;
using VRC.SDKBase;

namespace JLChnToZ.VRC.VVMW {
    public static class ShaderUtils {
        public static bool CopyMaterialIntegerProperty(MaterialPropertyBlock src, Material dest, int propertyId) {
            if (src.HasInteger(propertyId)) {
                dest.SetInteger(propertyId, src.GetInteger(propertyId));
                return true;
            }
            return CopyMaterialFloatProperty(src, dest, propertyId);
        }

        public static bool CopyMaterialFloatProperty(MaterialPropertyBlock src, Material dest, int propertyId) {
            if (src.HasFloat(propertyId)) {
                dest.SetFloat(propertyId, src.GetFloat(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialVectorProperty(MaterialPropertyBlock src, Material dest, int propertyId) {
            if (src.HasVector(propertyId)) {
                dest.SetVector(propertyId, src.GetVector(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialColorProperty(MaterialPropertyBlock src, Material dest, int propertyId) {
            if (src.HasColor(propertyId)) {
                dest.SetColor(propertyId, src.GetColor(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialTextureProperty(MaterialPropertyBlock src, Material dest, int propertyId) {
            if (src.HasTexture(propertyId)) {
                dest.SetTexture(propertyId, src.GetTexture(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialIntegerProperty(Material src, Material dest, int propertyId) {
            if (Utilities.IsValid(src) && src.HasInteger(propertyId)) {
                dest.SetInteger(propertyId, src.GetInteger(propertyId));
                return true;
            }
            return CopyMaterialFloatProperty(src, dest, propertyId);
        }

        public static bool CopyMaterialFloatProperty(Material src, Material dest, int propertyId) {
            if (Utilities.IsValid(src) && src.HasFloat(propertyId)) {
                dest.SetFloat(propertyId, src.GetFloat(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialVectorProperty(Material src, Material dest, int propertyId) {
            if (Utilities.IsValid(src) && src.HasVector(propertyId)) {
                dest.SetVector(propertyId, src.GetVector(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialColorProperty(Material src, Material dest, int propertyId) {
            if (Utilities.IsValid(src) && src.HasColor(propertyId)) {
                dest.SetColor(propertyId, src.GetColor(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialTextureProperty(Material src, Material dest, int propertyId) {
            if (Utilities.IsValid(src) && src.HasTexture(propertyId)) {
                dest.SetTexture(propertyId, src.GetTexture(propertyId));
                return true;
            }
            return false;
        }

        public static bool CopyMaterialIntegerProperty(MaterialPropertyBlock src, Material fallback, Material dest, int propertyId) =>
            CopyMaterialIntegerProperty(src, dest, propertyId) ||
            CopyMaterialIntegerProperty(fallback, dest, propertyId);

        public static bool CopyMaterialFloatProperty(MaterialPropertyBlock src, Material fallback, Material dest, int propertyId) =>
            CopyMaterialFloatProperty(src, dest, propertyId) ||
            CopyMaterialFloatProperty(fallback, dest, propertyId);

        public static bool CopyMaterialVectorProperty(MaterialPropertyBlock src, Material fallback, Material dest, int propertyId) =>
            CopyMaterialVectorProperty(src, dest, propertyId) ||
            CopyMaterialVectorProperty(fallback, dest, propertyId);

        public static bool CopyMaterialColorProperty(MaterialPropertyBlock src, Material fallback, Material dest, int propertyId) =>
            CopyMaterialColorProperty(src, dest, propertyId) ||
            CopyMaterialColorProperty(fallback, dest, propertyId);

        public static bool CopyMaterialTextureProperty(MaterialPropertyBlock src, Material fallback, Material dest, int propertyId) =>
            CopyMaterialTextureProperty(src, dest, propertyId) ||
            CopyMaterialTextureProperty(fallback, dest, propertyId);

        public static bool CopyMaterialIntegerProperty(MaterialPropertyBlock src, Material[] fallbackCandidates, Material dest, int propertyId) {
            if (CopyMaterialIntegerProperty(src, dest, propertyId)) return true;
            foreach (var candidate in fallbackCandidates)
                if (CopyMaterialIntegerProperty(candidate, dest, propertyId)) return true;
            return false;
        }

        public static bool CopyMaterialFloatProperty(MaterialPropertyBlock src, Material[] fallbackCandidates, Material dest, int propertyId) {
            if (CopyMaterialFloatProperty(src, dest, propertyId)) return true;
            foreach (var candidate in fallbackCandidates)
                if (CopyMaterialFloatProperty(candidate, dest, propertyId)) return true;
            return false;
        }

        public static bool CopyMaterialVectorProperty(MaterialPropertyBlock src, Material[] fallbackCandidates, Material dest, int propertyId) {
            if (CopyMaterialVectorProperty(src, dest, propertyId)) return true;
            foreach (var candidate in fallbackCandidates)
                if (CopyMaterialVectorProperty(candidate, dest, propertyId)) return true;
            return false;
        }

        public static bool CopyMaterialColorProperty(MaterialPropertyBlock src, Material[] fallbackCandidates, Material dest, int propertyId) {
            if (CopyMaterialColorProperty(src, dest, propertyId)) return true;
            foreach (var candidate in fallbackCandidates)
                if (CopyMaterialColorProperty(candidate, dest, propertyId)) return true;
            return false;
        }

        public static bool CopyMaterialTextureProperty(MaterialPropertyBlock src, Material[] fallbackCandidates, Material dest, int propertyId) {
            if (CopyMaterialTextureProperty(src, dest, propertyId)) return true;
            foreach (var candidate in fallbackCandidates)
                if (CopyMaterialTextureProperty(candidate, dest, propertyId)) return true;
            return false;
        }
    }
}