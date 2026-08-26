using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using JLChnToZ.VRC.Foundation;
using JLChnToZ.VRC.Foundation.I18N.Editors;
using JLChnToZ.VRC.VVMW.Editors;

namespace JLChnToZ.VRC.VVMW.Designer {
    [CustomEditor(typeof(AudioSourceConfigurator))]
    [CanEditMultipleObjects]
    public class AudioSourceConfiguratorEditor : VVMWEditorBase {
        BoxBoundsHandle placementHandle;
        SphereBoundsHandle nearHandle, farHandle;
        SerializedProperty audioSourceProp, nearBoundsProp, farBoundsProp;

        static bool DrawSphereHandle(SphereBoundsHandle handle, ref AudioSourceConfigurator.SphereBounds bounds, AudioSourceConfigurator undoTarget) {
            using var change = new EditorGUI.ChangeCheckScope();
            handle.radius = bounds.radius;
            handle.center = Handles.PositionHandle(bounds.center, Quaternion.identity);
            handle.DrawHandle();
            if (change.changed) {
                Undo.RecordObject(undoTarget, "Adjust Bounds");
                bounds.radius = handle.radius;
                bounds.center = handle.center;
                if (PrefabUtility.IsPartOfPrefabInstance(undoTarget))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(undoTarget);
                return true;
            }
            return false;
        }

        protected override void OnEnable() {
            base.OnEnable();
            audioSourceProp = serializedObject.FindProperty(nameof(AudioSourceConfigurator.audioSources));
            placementHandle = new BoxBoundsHandle { wireframeColor = Color.yellow };
            nearBoundsProp = serializedObject.FindProperty(nameof(AudioSourceConfigurator.nearBounds));
            nearHandle = new SphereBoundsHandle { wireframeColor = Color.green };
            farBoundsProp = serializedObject.FindProperty(nameof(AudioSourceConfigurator.farBounds));
            farHandle = new SphereBoundsHandle { wireframeColor = Color.red };
        }

        public override void DrawEmbeddedInspectorGUI() {
            EditorGUILayout.PropertyField(audioSourceProp);
            EditorGUILayout.PropertyField(nearBoundsProp);
            EditorGUILayout.PropertyField(farBoundsProp);
            using (new EditorGUILayout.HorizontalScope()) {
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.estimate")))
                    (target as AudioSourceConfigurator).TryEstimate();
                if (GUILayout.Button(i18n.GetLocalizedContent("JLChnToZ.VRC.VVMW.Designer.AudioSourceConfigurator.fit")))
                    (target as AudioSourceConfigurator).TryFit();
            }
        }

        void OnSceneGUI() {
            var target = this.target as AudioSourceConfigurator;
            if (target == null) return;
            DrawSphereHandle(nearHandle, ref target.nearBounds, target);
            DrawSphereHandle(farHandle, ref target.farBounds, target);
            DrawAudioSourceAdjustmenthandles(target.audioSources);
        }

        void DrawAudioSourceAdjustmenthandles(AudioSource[] audioSources) {
            if (audioSources == null || audioSources.Length == 0) return;
            using (PooledObjectExtensions.Get(out List<Vector3> positions)) {
                bool isFirst = true;
                Bounds bounds = default;
                foreach (var audioSource in audioSources) {
                    if (audioSource == null) continue;
                    var pos = audioSource.transform.position;
                    if (isFirst) {
                        bounds = new Bounds(pos, Vector3.zero);
                        isFirst = false;
                    } else
                        bounds.Encapsulate(pos);
                    positions.Add(pos);
                }
                var min = bounds.min;
                var max = bounds.max;
                for (int i = 0, count = positions.Count; i < count; i++) {
                    var pos = positions[i];
                    pos.Set(
                        Mathf.InverseLerp(min.x, max.x, pos.x),
                        Mathf.InverseLerp(min.y, max.y, pos.y),
                        Mathf.InverseLerp(min.z, max.z, pos.z)
                    );
                    positions[i] = pos;
                }
                placementHandle.center = bounds.center;
                placementHandle.size = bounds.size;
                using (var change = new EditorGUI.ChangeCheckScope()) {
                    placementHandle.DrawHandle();
                    if (change.changed) {
                        var newBounds = new Bounds(placementHandle.center, placementHandle.size);
                        min = newBounds.min;
                        max = newBounds.max;
                        int i = 0;
                        foreach (var audioSource in audioSources) {
                            if (audioSource == null) continue;
                            var pos = positions[i++];
                            pos.Set(
                                Mathf.Lerp(min.x, max.x, pos.x),
                                Mathf.Lerp(min.y, max.y, pos.y),
                                Mathf.Lerp(min.z, max.z, pos.z)
                            );
                            var transform = audioSource.transform;
                            Undo.RecordObject(transform, "Move Audio Source");
                            transform.position = pos;
                            if (PrefabUtility.IsPartOfPrefabInstance(transform))
                                PrefabUtility.RecordPrefabInstancePropertyModifications(transform);
                        }
                        Undo.CollapseUndoOperations(Undo.GetCurrentGroup());
                    }
                }
            }
        }
    }
}