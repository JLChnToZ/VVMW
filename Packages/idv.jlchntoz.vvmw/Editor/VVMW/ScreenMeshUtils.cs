using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using JLChnToZ.VRC.Foundation;
using static Unity.Mathematics.math;

namespace JLChnToZ.VRC.VVMW.Designer {
    
    public static class ScreenMeshUtils {
        const string fixupMenu = "Tools/VizVid/Fixup Aspect Ratios";
        const string directory = "Assets/VizVid_Generated/";

        [MenuItem(fixupMenu, priority = 1000)]
        static void MenuFixupAspectRatioInMaterial() =>
            TryFixupAspectRatioInMaterial(Selection.GetFiltered<MeshRenderer>(SelectionMode.Editable | SelectionMode.ExcludePrefab | SelectionMode.Deep));

        [MenuItem(fixupMenu, true)]
        static bool MenuValidateFixupAspectRatioInMaterial() =>
            Selection.GetFiltered<MeshRenderer>(SelectionMode.Editable | SelectionMode.ExcludePrefab | SelectionMode.Deep).Length > 0;

        public static void TryFixupAspectRatioInMaterial(MeshRenderer meshRenderer) => TryFixupAspectRatioInMaterial(new[] { meshRenderer });

        public static void TryFixupAspectRatioInMaterial(IEnumerable<MeshRenderer> renderers) {
            using (HashSetPool<MeshRenderer>.Get(out var renderersCache))
            using (HashSetPool<Material>.Get(out var materialsRequireAliasing)) {
                renderersCache.UnionWith(renderers);
                var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
                var scene = prefabStage != null ? prefabStage.scene : SceneManager.GetActiveScene();
                foreach (var renderer in scene.IterateAllComponents<Renderer>()) {
                    if (renderer is MeshRenderer mr && renderersCache.Contains(mr))
                        continue;
                    foreach (var mat in renderer.sharedMaterials)
                        if (mat != null)
                            materialsRequireAliasing.Add(mat);
                }
                using (DictionaryPool<(Material, float), List<(MeshRenderer, int)>>.Get(out var materialSourceMap)) {
                    var aspectRatioID = Shader.PropertyToID("_AspectRatio");
                    foreach (var mr in renderers) {
                        if (!mr.TryGetComponent(out MeshFilter mf)) continue;
                        var mesh = mf.sharedMesh;
                        var materials = mr.sharedMaterials;
                        var matrix = mr.localToWorldMatrix;
                        List<(MeshRenderer, int)> sourceList = null;
                        for (int i = 0; i < materials.Length; i++) {
                            var mat = materials[i];
                            if (mat == null) continue;
                            if (!mat.HasProperty(aspectRatioID) ||
                                !TryEstimateAspectRatio(mesh, i, matrix, out var aspectRatio) ||
                                Mathf.Approximately(mat.GetFloat(aspectRatioID), aspectRatio)) {
                                materialsRequireAliasing.Add(mat);
                                continue;
                            }
                            if (sourceList == null && !materialSourceMap.TryGetValue((mat, aspectRatio), out sourceList)) {
                                sourceList = ListPool<(MeshRenderer, int)>.Get();
                                materialSourceMap.Add((mat, aspectRatio), sourceList);
                            }
                            sourceList.Add((mr, i));
                        }
                    }
                    using (ListPool<(string, Material, float)>.Get(out var generatedMaterials)) {
                        using (DictionaryPool<(Material, float), Material>.Get(out var materialAspectMap)) 
                            foreach (var kv in materialSourceMap) {
                                var (mat, aspectRatio) = kv.Key;
                                if (!materialAspectMap.TryGetValue(kv.Key, out var newMat)) {
                                    var assetPath = AssetDatabase.GetAssetPath(mat);
                                    if (materialsRequireAliasing.Add(mat) && !string.IsNullOrEmpty(assetPath) && !assetPath.StartsWith("Packages/")) {
                                        newMat = mat;
                                        Undo.RecordObject(newMat, "Fixup Aspect Ratio in Material");
                                    } else {
                                        newMat = new Material(mat) { parent = mat };
                                        generatedMaterials.Add((assetPath, newMat, aspectRatio));
                                    }
                                    newMat.SetFloat(aspectRatioID, aspectRatio);
                                    materialAspectMap.Add(kv.Key, newMat);
                                }
                                foreach (var (mr, index) in kv.Value) {
                                    var sharedMaterials = mr.sharedMaterials;
                                    sharedMaterials[index] = newMat;
                                    Undo.RecordObject(mr, "Fixup Aspect Ratio in Material");
                                    mr.sharedMaterials = sharedMaterials;
                                }
                                ListPool<(MeshRenderer, int)>.Release(kv.Value);
                            }
                        bool hasValidatedFolder = false;
                        foreach (var (assetPath, mat, aspectRatio) in generatedMaterials) {
                            string path;
                            string postfix = $"_Adjusted_{HumanizeAspectRatio(aspectRatio)}";
                            if (!string.IsNullOrEmpty(assetPath) && !assetPath.StartsWith("Packages/"))
                                path = AssetDatabase.GenerateUniqueAssetPath(assetPath.Insert(assetPath.LastIndexOf('.'), postfix));
                            else {
                                if (!hasValidatedFolder) {
                                    hasValidatedFolder = true;
                                    if (!AssetDatabase.IsValidFolder(directory))
                                        AssetDatabase.CreateFolder("Assets", "VizVid_Generated");
                                }
                                path = AssetDatabase.GenerateUniqueAssetPath($"{directory}{mat.name}{postfix}.mat");
                            }
                            AssetDatabase.CreateAsset(mat, path);
                        }
                    }
                }
            }
        }

        public static bool TryEstimateAspectRatio(Mesh mesh, int subMeshIndex, Matrix4x4 objectToWorld, out float aspectRatio) {
            if (mesh == null || subMeshIndex < 0 || subMeshIndex >= mesh.subMeshCount) {
                aspectRatio = 1;
                return false;
            }
            using var job = AspectRatioFinder.Create(mesh, subMeshIndex, objectToWorld, out var length);
            job.Schedule(length, 64).Complete();
            return job.TryGetResult(out aspectRatio);
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
        struct AspectRatioFinder : IJobParallelFor, IDisposable {
            [ReadOnly] float4x4 objectToWorld;
            [ReadOnly] NativeArray<float3> vertices;
            [ReadOnly] NativeArray<float2> uvs;
            [ReadOnly] NativeSlice<ushort> indices;
            [WriteOnly] NativeArray<float2> results;

            public static AspectRatioFinder Create(Mesh mesh, int subMeshIndex, Matrix4x4 objectToWorld, out int length) {
                var meshData = Mesh.AcquireReadOnlyMeshData(mesh)[0];
                var subMesh = meshData.GetSubMesh(subMeshIndex);
                var vc = meshData.vertexCount;
                var vertices = new NativeArray<Vector3>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var uvs = new NativeArray<Vector2>(vc, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
                var indices = meshData.GetIndexData<ushort>();
                meshData.GetVertices(vertices);
                meshData.GetUVs(0, uvs);
                length = subMesh.indexCount / 3;
                return new AspectRatioFinder {
                    indices = indices.Slice(subMesh.indexStart, subMesh.indexCount),
                    vertices = vertices.Reinterpret<float3>(),
                    uvs = uvs.Reinterpret<float2>(),
                    results = new NativeArray<float2>(length, Allocator.TempJob, NativeArrayOptions.UninitializedMemory),
                    objectToWorld = objectToWorld,
                };
            }

            static float2 KahanSum(in NativeArray<float2> array) {
                float2 sum = 0, c = 0;
                foreach (var v in array) {
                    var y = v - c;
                    var t = sum + y;
                    c = t - sum - y;
                    sum = t;
                }
                return sum;
            }

            readonly float3 WorldPos(int index) => mul(objectToWorld, float4(vertices[index], 1)).xyz;

            public void Execute(int index) {
                int i = index * 3;
                results[index] = 0;
                if (i + 2 >= indices.Length) return;
                ushort i1 = indices[i++], i2 = indices[i++], i3 = indices[i];
                float3 p0 = WorldPos(i1), p1 = WorldPos(i2), p2 = WorldPos(i3);
                float3 e1 = p1 - p0, e2 = p2 - p0;
                float w = lengthsq(cross(e1, e2));
                if (w <= 0) return;
                float4 duv = float4(uvs[i3], uvs[i2]) - uvs[i1].xyxy;
                float tt = lengthsq(e1 * duv.w - e2 * duv.y);
                if (tt <= 0) return;
                float bb = lengthsq(e2 * duv.x - e1 * duv.z);
                if (bb <= 0) return;
                results[index] = float2(log2(tt / bb) * w, w);
            }

            public readonly bool TryGetResult(out float result) {
                float2 resultWeights = KahanSum(results);
                if (resultWeights.y > 0) {
                    result = exp2(resultWeights.x / resultWeights.y * 0.5f);
                    return true;
                }
                result = float.NaN;
                return false;
            }

            public void Dispose() {
                if (vertices.IsCreated) vertices.Dispose();
                if (uvs.IsCreated) uvs.Dispose();
                if (results.IsCreated) results.Dispose();
            }
        }
    }
}