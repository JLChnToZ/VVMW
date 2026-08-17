using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using JLChnToZ.VRC.Foundation;
using static Unity.Mathematics.math;
using static Unity.Mathematics.quaternion;

namespace JLChnToZ.VRC.VVMW.Designer {
    [InitializeOnLoad]
    public static class ScreenMeshUtils {
        const string fixupMenu = "Tools/VizVid/Fixup Aspect Ratios";

        static ScreenMeshUtils() {
            ScreenConfigurator.tryEstimatePlacement = TryEstimatePlacement;
        }

        [MenuItem(fixupMenu, priority = 1000)]
        static void MenuFixupAspectRatioInMaterial() =>
            TryFixupAspectRatioInMaterial(Selection.GetFiltered<MeshRenderer>(SelectionMode.Editable | SelectionMode.ExcludePrefab | SelectionMode.Deep));

        [MenuItem(fixupMenu, true)]
        static bool MenuValidateFixupAspectRatioInMaterial() =>
            Selection.GetFiltered<MeshRenderer>(SelectionMode.Editable | SelectionMode.ExcludePrefab | SelectionMode.Deep).Length > 0;

        public static void TryFixupAspectRatioInMaterial(MeshRenderer meshRenderer) => TryFixupAspectRatioInMaterial(new[] { meshRenderer });

        public readonly struct MaterialPropertyOverride {
            public readonly int propertyId;
            public readonly ShaderPropertyType propertyType;
            public readonly object value;

            public MaterialPropertyOverride(int propertyId, ShaderPropertyType propertyType, object value) {
                this.propertyId = propertyId;
                this.propertyType = propertyType;
                this.value = value;
            }

            public MaterialPropertyOverride(string propertyName, ShaderPropertyType propertyType, object value) : this(Shader.PropertyToID(propertyName), propertyType, value) { }

            public MaterialPropertyOverride(string propertyName, float value) : this(Shader.PropertyToID(propertyName), ShaderPropertyType.Float, value) { }

            public MaterialPropertyOverride(string propertyName, int value) : this(Shader.PropertyToID(propertyName), ShaderPropertyType.Int, value) { }

            public MaterialPropertyOverride(string propertyName, Color value) : this(Shader.PropertyToID(propertyName), ShaderPropertyType.Color, value) { }

            public MaterialPropertyOverride(string propertyName, Vector4 value) : this(Shader.PropertyToID(propertyName), ShaderPropertyType.Vector, value) { }

            public MaterialPropertyOverride(string propertyName, Texture value) : this(Shader.PropertyToID(propertyName), ShaderPropertyType.Texture, value) { }
        }

        readonly struct MaterialPropertyKey : IEquatable<MaterialPropertyKey> {
            readonly MaterialPropertyOverride[] properties;
            readonly int hashCode;

            public MaterialPropertyKey(MaterialPropertyOverride[] properties) {
                this.properties = properties;
                var hc = new HashCode();
                foreach (var property in properties) {
                    hc.Add(property.propertyId);
                    hc.Add(property.propertyType);
                    hc.Add(property.value);
                }
                hashCode = hc.ToHashCode();
            }

            public bool Equals(MaterialPropertyKey other) {
                if (properties.Length != other.properties.Length) return false;
                for (int i = 0; i < properties.Length; i++) {
                    var a = properties[i];
                    var b = other.properties[i];
                    if (a.propertyId != b.propertyId ||
                        a.propertyType != b.propertyType ||
                        !Equals(a.value, b.value))
                        return false;
                }
                return true;
            }

            public override bool Equals(object obj) => obj is MaterialPropertyKey other && Equals(other);

            public override int GetHashCode() => hashCode;
        }

        public static void TryFixupAspectRatioInMaterial(IEnumerable<Renderer> renderers) =>
            TryFixupMaterialProperties(renderers, GetAspectRatioProperties, GetAspectRatioPostfix, "Fixup Aspect Ratio in Material");

        static MaterialPropertyOverride[] GetAspectRatioProperties(Renderer renderer, int subMeshIndex) {
            if (!renderer.TryGetComponent(out MeshFilter meshFilter)) return null;
            var mesh = meshFilter.sharedMesh;
            if (mesh == null || !mesh.isReadable || !TryEstimateAspectRatio(mesh, subMeshIndex, renderer.localToWorldMatrix, out var aspectRatio))
                return null;
            return new[] { new MaterialPropertyOverride("_AspectRatio", aspectRatio) };
        }

        static string GetAspectRatioPostfix(MaterialPropertyOverride[] properties) =>
            $"_Adjusted_{HumanizeAspectRatio((float)properties[0].value)}";

        public static void TryFixupMaterialProperties(
            IEnumerable<Renderer> renderers,
            MaterialPropertyOverride[] properties,
            string generatedMaterialPostfix = "_Adjusted"
        ) {
            var provider = new FixedMaterialPropertyProvider(properties, generatedMaterialPostfix);
            TryFixupMaterialProperties(renderers, provider.GetProperties, provider.GetGeneratedMaterialPostfix, "Fixup Aspect Ratio in Material");
        }

        sealed class FixedMaterialPropertyProvider {
            readonly MaterialPropertyOverride[] properties;
            readonly string generatedMaterialPostfix;

            public FixedMaterialPropertyProvider(MaterialPropertyOverride[] properties, string generatedMaterialPostfix) {
                this.properties = properties;
                this.generatedMaterialPostfix = generatedMaterialPostfix;
            }

            public MaterialPropertyOverride[] GetProperties(Renderer renderer, int subMeshIndex) => properties;

            public string GetGeneratedMaterialPostfix(MaterialPropertyOverride[] properties) => generatedMaterialPostfix;
        }

        public static void TryFixupMaterialProperties(
            IEnumerable<Renderer> renderers,
            Func<Renderer, int, MaterialPropertyOverride[]> getProperties,
            Func<MaterialPropertyOverride[], string> getGeneratedMaterialPostfix = null,
            string undoName = "Fixup Material Properties"
        ) {
            getGeneratedMaterialPostfix ??= GetDefaultGeneratedMaterialPostfix;
            using (HashSetPool<Renderer>.Get(out var renderererMap))
            using (HashSetPool<Material>.Get(out var materialsRequireAliasing))
            using (DictionaryPool<(Material, MaterialPropertyKey), List<(Renderer, int)>>.Get(out var materialSourceMap))
            using (ListPool<(string, Material, MaterialPropertyOverride[])>.Get(out var generatedMaterials))
            using (DictionaryPool<(Material, MaterialPropertyKey), Material>.Get(out var materialPropertyMap)) {
                foreach (var r in renderers) {
                    if (r == null) continue;
                    renderererMap.Add(r);
                }
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                var scene = prefabStage != null ? prefabStage.scene : SceneManager.GetActiveScene();
                foreach (var renderer in scene.IterateAllComponents<Renderer>()) {
                    if (renderererMap.Contains(renderer))
                        continue;
                    foreach (var mat in renderer.sharedMaterials)
                        if (mat != null)
                            materialsRequireAliasing.Add(mat);
                }
                foreach (var mr in renderererMap) {
                    var materials = mr.sharedMaterials;
                    for (int i = 0; i < materials.Length; i++) {
                        var mat = materials[i];
                        if (mat == null) continue;
                        var properties = getProperties(mr, i);
                        if (!TryGetPropertyKey(mat, properties, out var propertyKey) ||
                            ArePropertiesEqual(mat, properties)) {
                            materialsRequireAliasing.Add(mat);
                            continue;
                        }
                        if (!materialSourceMap.TryGetValue((mat, propertyKey), out var sourceList)) {
                            sourceList = ListPool<(Renderer, int)>.Get();
                            materialSourceMap.Add((mat, propertyKey), sourceList);
                        }
                        sourceList.Add((mr, i));
                    }
                }
                foreach (var kv in materialSourceMap) {
                    var mat = kv.Key.Item1;
                    if (!materialPropertyMap.TryGetValue(kv.Key, out var newMat)) {
                        var assetPath = AssetDatabase.GetAssetPath(mat);
                        var properties = kv.Value.Count > 0 ? getProperties(kv.Value[0].Item1, kv.Value[0].Item2) : null;
                        var excludePropertyIds = GetPropertyIds(properties);
                        bool canModify = materialsRequireAliasing.Add(mat) && MaterialUtil.IsMaterialSafeToModify(mat);
                        if (canModify) {
                            newMat = mat;
                            Undo.RecordObject(newMat, undoName);
                        } else
                            newMat = MaterialUtil.CreateGeneratedAlias(mat, excludePropertyIds);
                        SetProperties(newMat, properties);
                        if (!canModify && getGeneratedMaterialPostfix != null)
                            generatedMaterials.Add((assetPath, newMat, properties));
                        materialPropertyMap.Add(kv.Key, newMat);
                    }
                    foreach (var (mr, index) in kv.Value) {
                        var sharedMaterials = mr.sharedMaterials;
                        sharedMaterials[index] = newMat;
                        Undo.RecordObject(mr, undoName);
                        mr.sharedMaterials = sharedMaterials;
                        if (PrefabUtility.IsPartOfPrefabInstance(mr))
                            PrefabUtility.RecordPrefabInstancePropertyModifications(mr);
                    }
                    ListPool<(Renderer, int)>.Release(kv.Value);
                }
                foreach (var (assetPath, mat, properties) in generatedMaterials)
                    MaterialUtil.SaveMaterialAsAsset(mat, assetPath, getGeneratedMaterialPostfix(properties), false);
            }
        }

        static string GetDefaultGeneratedMaterialPostfix(MaterialPropertyOverride[] properties) => "_Adjusted";

        static bool TryGetPropertyKey(Material material, MaterialPropertyOverride[] properties, out MaterialPropertyKey key) {
            if (properties == null) {
                key = default;
                return false;
            }
            foreach (var property in properties)
                if (property.propertyId == 0 || !material.HasProperty(property.propertyId)) {
                    key = default;
                    return false;
                }
            key = new MaterialPropertyKey(properties);
            return true;
        }

        static bool ArePropertiesEqual(Material material, MaterialPropertyOverride[] properties) {
            foreach (var property in properties) {
                switch (property.propertyType) {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        if (property.value is not float value || !Mathf.Approximately(material.GetFloat(property.propertyId), value)) return false;
                        break;
                    case ShaderPropertyType.Int:
                        if (property.value is not int intValue || material.GetInteger(property.propertyId) != intValue) return false;
                        break;
                    case ShaderPropertyType.Color:
                        if (property.value is not Color color || material.GetColor(property.propertyId) != color) return false;
                        break;
                    case ShaderPropertyType.Vector:
                        if (property.value is not Vector4 vector || material.GetVector(property.propertyId) != vector) return false;
                        break;
                    case ShaderPropertyType.Texture:
                        if (property.value is not Texture && property.value != null) return false;
                        if (material.GetTexture(property.propertyId) != (Texture)property.value) return false;
                        break;
                    default:
                        return false;
                }
            }
            return true;
        }

        static int[] GetPropertyIds(MaterialPropertyOverride[] properties) {
            if (properties == null) return null;
            var ids = new int[properties.Length];
            for (int i = 0; i < properties.Length; i++)
                ids[i] = properties[i].propertyId;
            return ids;
        }

        static void SetProperties(Material material, MaterialPropertyOverride[] properties) {
            foreach (var property in properties) {
                switch (property.propertyType) {
                    case ShaderPropertyType.Float:
                    case ShaderPropertyType.Range:
                        material.SetFloat(property.propertyId, (float)property.value);
                        break;
                    case ShaderPropertyType.Int:
                        material.SetInteger(property.propertyId, (int)property.value);
                        break;
                    case ShaderPropertyType.Color:
                        material.SetColor(property.propertyId, (Color)property.value);
                        break;
                    case ShaderPropertyType.Vector:
                        material.SetVector(property.propertyId, (Vector4)property.value);
                        break;
                    case ShaderPropertyType.Texture:
                        material.SetTexture(property.propertyId, property.value as Texture);
                        break;
                }
            }
        }

        public static bool TryEstimateAspectRatio(Mesh mesh, int subMeshIndex, Matrix4x4 objectToWorld, out float aspectRatio) {
            if (mesh == null || subMeshIndex >= mesh.subMeshCount) {
                aspectRatio = 1;
                return false;
            }
            using var meshDatas = Mesh.AcquireReadOnlyMeshData(mesh);
            using var job = ScreenEstimator.Create(meshDatas[0], subMeshIndex, objectToWorld, out var count);
            job.Schedule(count, 64).Complete();
            if (!job.TryGetResult(out _, out var v, out var u, out _, out _)) {
                aspectRatio = 1;
                return false;
            }
            aspectRatio = sqrt(lengthsq(u) / lengthsq(v));
            return true;
        }

        public static bool TryEstimatePlacement(
            Mesh mesh, int subMeshIndex, Matrix4x4 objectToWorld,
            out Vector3 position, out Quaternion rotation, out Vector3 scale
        ) {
            if (mesh == null || subMeshIndex >= mesh.subMeshCount) {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
                return false;
            }
            using var meshDatas = Mesh.AcquireReadOnlyMeshData(mesh);
            using var job = ScreenEstimator.Create(meshDatas[0], subMeshIndex, objectToWorld, out var count);
            job.Schedule(count, 64).Complete();
            if (!job.TryGetResult(out var c, out var v, out var u, out _, out _)) {
                position = Vector3.zero;
                rotation = Quaternion.identity;
                scale = Vector3.one;
                return false;
            }
            position = c;
            rotation = LookRotationSafe(cross(v, u), v);
            scale = sqrt(float3(lengthsq(u), lengthsq(v), 1));
            return true;
        }

        static string HumanizeAspectRatio(float aspectRatio) {
            const int maxDenominator = 100;
            int bestNumerator = 1, bestDenominator = 1;
            float bestError = Mathf.Abs(aspectRatio - 1);
            for (int denominator = 1; denominator <= maxDenominator; denominator++) {
                int numerator = Mathf.RoundToInt(aspectRatio * denominator);
                float error = Mathf.Abs(aspectRatio - (float)numerator / denominator);
                if (error < bestError) {
                    bestError = error;
                    bestNumerator = numerator;
                    bestDenominator = denominator;
                }
            }
            return $"{bestNumerator}_{bestDenominator}";
        }

        [BurstCompile]
        struct ScreenEstimator : IJobParallelFor, IDisposable {
            [ReadOnly] float4x4 objectToWorld;
            [ReadOnly] NativeArray<float3> vertices;
            [ReadOnly] NativeArray<float2> uvs;
            [ReadOnly] NativeSlice<ushort> indices;
            [WriteOnly] NativeArray<Fragment> frags;
            [WriteOnly] NativeArray<float4> uvRanges;

            public static ScreenEstimator Create(Mesh.MeshData meshData, int subMeshIndex, Matrix4x4 objectToWorld, out int resultCount) {
                var vertexCount = meshData.vertexCount;
                ScreenEstimator instance;
                var indices = meshData.GetIndexData<ushort>();
                if (subMeshIndex < 0) {
                    instance = new ScreenEstimator(objectToWorld, vertexCount, indices, out resultCount);
                } else {
                    var subMesh = meshData.GetSubMesh(subMeshIndex);
                    instance = new ScreenEstimator(objectToWorld, vertexCount, indices.Slice(subMesh.indexStart, subMesh.indexCount), out resultCount);
                }
                meshData.GetVertices(instance.vertices.Reinterpret<Vector3>());
                meshData.GetUVs(0, instance.uvs.Reinterpret<Vector2>());
                return instance;
            }

            static Fragment KahanSum(in NativeArray<Fragment> array) {
                Fragment sum = default, c = default;
                foreach (var v in array) {
                    var y = v - c;
                    var t = sum + y;
                    c = t - sum - y;
                    sum = t;
                }
                return sum;
            }

            ScreenEstimator(float4x4 objectToWorld, int vertexCount, NativeSlice<ushort> indices, out int resultCount) {
                this.objectToWorld = objectToWorld;
                vertices = new NativeArray<float3>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                uvs = new NativeArray<float2>(vertexCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                this.indices = indices;
                resultCount = indices.Length / 3;
                frags = new NativeArray<Fragment>(resultCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                uvRanges = new NativeArray<float4>(resultCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
            }

            readonly float3 WorldPos(int index) => mul(objectToWorld, float4(vertices[index], 1)).xyz;

            public void Execute(int index) {
                frags[index] = default;
                uvRanges[index] = new float4(
                    float.PositiveInfinity, float.PositiveInfinity,
                    float.NegativeInfinity, float.NegativeInfinity
                );
                int i = index * 3;
                if (i + 2 >= indices.Length) return;
                ushort i1 = indices[i++], i2 = indices[i++], i3 = indices[i];
                float3 p0 = WorldPos(i1), p1 = WorldPos(i2), p2 = WorldPos(i3);
                float3 e1 = p1 - p0, e2 = p2 - p0;
                float w = lengthsq(cross(e1, e2));
                if (!(w > 0)) return;
                float2 uv0 = uvs[i1], uv1 = uvs[i2], uv2 = uvs[i3];
                float2 d1 = uv1 - uv0, d2 = uv2 - uv0;
                float det = d1.x * d2.y - d2.x * d1.y;
                if (!(abs(det) > 0)) return;
                float rcp = 1f / det;
                float3 u = (e1 * d2.y - e2 * d1.y) * rcp;
                float3 v = (e2 * d1.x - e1 * d2.x) * rcp;
                if (!(lengthsq(u) > 0) || !(lengthsq(v) > 0)) return;
                float3 origin = p0 - u * uv0.x - v * uv0.y;
                frags[index] = new Fragment(origin + (u + v) * 0.5F, v, u) * w;
                uvRanges[index] = new float4(min(uv0, min(uv1, uv2)), max(uv0, max(uv1, uv2)));
            }

            public readonly bool TryGetResult(out float3 center, out float3 up, out float3 right, out float2 uvMin, out float2 uvMax) {
                var s = KahanSum(frags);
                if (!(s.weight > 0F)) {
                    center = up = right = float.NaN;
                    uvMin = 0F;
                    uvMax = 1F;
                    return false;
                }
                float rcp = 1f / s.weight;
                center = s.center * rcp;
                up = s.up * rcp;
                right = s.right * rcp;
                uvMin = float.PositiveInfinity;
                uvMax = float.NegativeInfinity;
                foreach (var uvRange in uvRanges) {
                    uvMin = min(uvRange.xy, uvMin);
                    uvMax = max(uvRange.zw, uvMax);
                }
                return true;
            }

            public void Dispose() {
                if (vertices.IsCreated) vertices.Dispose();
                if (uvs.IsCreated) uvs.Dispose();
                if (frags.IsCreated) frags.Dispose();
                if (uvRanges.IsCreated) uvRanges.Dispose();
            }
        }

        struct Fragment {
            public float3 center, up, right;
            public float weight;

            public Fragment(float3 center, float3 up, float3 right, float weight = 1F) {
                this.center = center;
                this.up = up;
                this.right = right;
                this.weight = weight;
            }

            public static Fragment operator *(Fragment a, float w) => new Fragment(
                a.center * w,
                a.up * w,
                a.right * w,
                a.weight * w
            );

            public static Fragment operator +(Fragment a, Fragment b) => new Fragment(
                a.center + b.center,
                a.up + b.up,
                a.right + b.right,
                a.weight + b.weight
            );

            public static Fragment operator -(Fragment a, Fragment b) => new Fragment(
                a.center - b.center,
                a.up - b.up,
                a.right - b.right,
                a.weight - b.weight
            );
        }
    }
}