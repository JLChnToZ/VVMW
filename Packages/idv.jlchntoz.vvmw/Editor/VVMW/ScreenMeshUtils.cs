using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Pool;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Burst;
using static Unity.Mathematics.math;

namespace JLChnToZ.VRC.VVMW.Designer {
    
    public static class ScreenMeshUtils {
        const string directory = "Assets/VizVid_Generated/";

        public static void TryFixupAspectRatioInMaterial(MeshRenderer meshRenderer) {
            if (meshRenderer == null || !meshRenderer.TryGetComponent(out MeshFilter meshFilter)) return;
            var mesh = meshFilter.sharedMesh;
            if (mesh == null) return;
            var materials = meshRenderer.sharedMaterials;
            var aspectRatioID = Shader.PropertyToID("_AspectRatio");
            var matrix = meshRenderer.localToWorldMatrix;
            using (ListPool<Material>.Get(out var generatedMaterials)) {
                bool changed = false;
                for (int i = 0; i < materials.Length; i++) {
                    var mat = materials[i];
                    if (mat == null ||
                        !mat.HasProperty(aspectRatioID) ||
                        !TryEstimateAspectRatio(mesh, i, matrix, out var aspectRatio) ||
                        Mathf.Approximately(mat.GetFloat(aspectRatioID), aspectRatio))
                        continue;
                    bool isGenerated = AssetDatabase.GetAssetPath(mat).StartsWith(directory);
                    var newMat = isGenerated ? mat : new Material(mat) { parent = mat };
                    if (isGenerated) Undo.RecordObject(newMat, "Fixup Aspect Ratio in Material");
                    newMat.SetFloat(aspectRatioID, aspectRatio);
                    materials[i] = newMat;
                    if (!isGenerated) generatedMaterials.Add(newMat);
                    changed = true;
                }
                if (generatedMaterials.Count > 0) {
                    if (!AssetDatabase.IsValidFolder(directory))
                        AssetDatabase.CreateFolder("Assets", "VizVid_Generated");
                    AssetDatabase.StartAssetEditing();
                    try {
                        foreach (var mat in generatedMaterials)
                            AssetDatabase.CreateAsset(mat, AssetDatabase.GenerateUniqueAssetPath($"{directory}{mat.name}_Adjusted.mat"));
                    } finally {
                        AssetDatabase.StopAssetEditing();
                    }
                }
                if (changed) {
                    Undo.RecordObject(meshRenderer, "Fixup Aspect Ratio in Material");
                    meshRenderer.sharedMaterials = materials;
                    Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
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
            return job.TryGetRseult(out aspectRatio);
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
                var vertices = new NativeArray<Vector3>(meshData.vertexCount, Allocator.TempJob);
                var uvs = new NativeArray<Vector2>(meshData.vertexCount, Allocator.TempJob);
                var indices = meshData.GetIndexData<ushort>();
                meshData.GetVertices(vertices);
                meshData.GetUVs(0, uvs);
                length = subMesh.indexCount / 3;
                return new AspectRatioFinder {
                    indices = indices.Slice(subMesh.indexStart, subMesh.indexCount),
                    vertices = vertices.Reinterpret<float3>(),
                    uvs = uvs.Reinterpret<float2>(),
                    results = new NativeArray<float2>(length, Allocator.TempJob),
                    objectToWorld = objectToWorld,
                };
            }

            static float EstimateAspectRatio(
                float3 p1, float2 uv1,
                float3 p2, float2 uv2,
                float3 p3, float2 uv3
            ) {
                var duv = float4(uv2, uv3) - uv1.xyxy;
                var r = transpose(mul(
                    float3x3(
                        duv.w, -duv.y, 0,
                        -duv.z, duv.x, 0,
                        0, 0, 0
                    ),
                    float3x3(
                        p2 - p1,
                        p3 - p1,
                        float3(0)
                    )
                ));
                var l1 = dot(r.c1, r.c1);
                return l1 > 0 ? length(r.c0) * rsqrt(l1) : 0;
            }

            static float Area(float3 p1, float3 p2, float3 p3) => length(cross(p2 - p1, p3 - p1)) * 0.5f;

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
                if (i + 2 >= indices.Length) return;
                int i1 = indices[i], i2 = indices[i + 1], i3 = indices[i + 2];
                float3 p1 = WorldPos(i1), p2 = WorldPos(i2), p3 = WorldPos(i3);
                float area = Area(p1, p2, p3);
                results[index] = float2(EstimateAspectRatio(
                    p3, uvs[i3],
                    p1, uvs[i1],
                    p2, uvs[i2]
                ) * area, area);
            }

            public readonly bool TryGetRseult(out float result) {
                float2 resultWeights = KahanSum(results);
                if (resultWeights.y > 0) {
                    result = resultWeights.x / resultWeights.y;
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