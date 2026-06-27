using System;
using UnityEditor;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW.Designer {
    internal static class MaterialUtil {
        const string assetsRoot = "Assets";
        const string assetsPath = assetsRoot + "/";
        const string directoryName = "VizVid_Generated";
        const string directoryPath = assetsPath + directoryName + "/";

        static void EnsureDirectoryExists() {
            if (!AssetDatabase.IsValidFolder(directoryPath)) AssetDatabase.CreateFolder(assetsRoot, directoryName);
        }

        public static bool IsMaterialSafeToModify(Material material) {
            if (material == null) return false;
            var path = AssetDatabase.GetAssetPath(material);
            return !string.IsNullOrEmpty(path) && path.StartsWith(assetsPath) && AssetDatabase.IsNativeAsset(material);
        }

        public static Material CreateGeneratedAlias(Material original, int[] excludePropertyIds = null) {
            if (original == null) return null;
            var alias = new Material(original);
            var shader = alias.shader;
            int propertyCount = shader.GetPropertyCount();
            var parentMat = alias;
            do {
                bool hasOtherPropertyChanged = false;
                for (int i = 0; i < propertyCount; i++) {
                    int id = shader.GetPropertyNameId(i);
                    if ((excludePropertyIds == null || Array.IndexOf(excludePropertyIds, id) < 0) &&
                        parentMat.IsPropertyOverriden(id)) {
                        hasOtherPropertyChanged = true;
                        break;
                    }
                }
                if (hasOtherPropertyChanged) {
                    alias.parent = parentMat;
                    break;
                }
                parentMat = parentMat.parent;
            } while (parentMat != null);
            return alias;
        }

        public static void SaveMaterialAsAsset(Material material, string path, string postfix, bool autoSave = true) {
            if (material == null) return;
            if (string.IsNullOrEmpty(path))
                path = AssetDatabase.GetAssetPath(material);
            if (string.IsNullOrEmpty(path) || !path.StartsWith(assetsPath)) {
                EnsureDirectoryExists();
                path = $"{directoryPath}{material.name}{postfix}.mat";
            } else
                path = path.Insert(path.LastIndexOf('.'), postfix);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(path));
            if (autoSave) AssetDatabase.SaveAssets();
        }
    }
}
