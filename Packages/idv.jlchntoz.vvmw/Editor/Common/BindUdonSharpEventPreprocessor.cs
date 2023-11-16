using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.Build.Reporting;

using UdonSharp;
using UdonSharpEditor;

using UnityObject = UnityEngine.Object;
using VRC.Udon;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal sealed class BindUdonSharpEventPreprocessor : BindEventPreprocessorBase {
        readonly Dictionary<UdonSharpEventSender, List<UdonSharpBehaviour>> eventSenders = new Dictionary<UdonSharpEventSender, List<UdonSharpBehaviour>>();
        
        public override void OnProcessScene(Scene scene, BuildReport report) {
            foreach (var usharp in scene.IterateAllComponents<UdonSharpBehaviour>()) {
                var type = usharp.GetType();
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(usharp);
                if (udon == null) {
                    Debug.LogError($"[BindUdonSharpEventPreprocessor] `{usharp.name}` is not correctly configured.", usharp);
                    continue;
                }
                var fieldInfos = GetFields<BindUdonSharpEventAttribute>(type);
                foreach (var field in fieldInfos) {
                    var targetObj = field.GetValue(usharp);
                    if (targetObj is Array array)
                        for (int i = 0, length = array.GetLength(0); i < length; i++)
                            AddEntry(array.GetValue(i) as UnityObject, usharp);
                    else if (targetObj is UnityObject unityObject)
                        AddEntry(unityObject, usharp);
                }
            }
            var remapped = new List<UdonSharpBehaviour>();
            var duplicateCheck = new HashSet<UdonSharpBehaviour>();
            foreach (var kv in eventSenders) {
                var sender = kv.Key;
                if (sender == null) {
                    Debug.LogError("[BindUdonSharpEventPreprocessor] Event sender is null, this should not happen.", sender);
                    continue;
                }
                using (var so = new SerializedObject(sender)) {
                    var prop = so.FindProperty("targets");
                    for (int i = 0, count = prop.arraySize; i < count; i++)
                        if (prop.GetArrayElementAtIndex(i).objectReferenceValue is UdonSharpBehaviour ub && ub != null && duplicateCheck.Add(ub))
                            remapped.Add(ub);
                    foreach (var entry in kv.Value)
                        if (entry != null && duplicateCheck.Add(entry))
                            remapped.Add(entry);
                    prop.arraySize += remapped.Count;
                    for (int i = 0, count = remapped.Count; i < count; i++)
                        prop.GetArrayElementAtIndex(i).objectReferenceValue = remapped[i];
                    remapped.Clear();
                    duplicateCheck.Clear();
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                UdonSharpEditorUtility.CopyProxyToUdon(sender);
            }
            eventSenders.Clear();
        }

        void AddEntry(UnityObject targetObj, UdonSharpBehaviour dest) {
            if (targetObj == null) return;
            if (targetObj is GameObject go) targetObj = go.GetComponent<UdonSharpEventSender>();
            else if (targetObj is UdonBehaviour ub) targetObj = UdonSharpEditorUtility.GetProxyBehaviour(ub);
            if (!(targetObj is UdonSharpEventSender sender) || sender == null) return;
            if (!eventSenders.TryGetValue(sender, out var list))
                eventSenders[sender] = list = new List<UdonSharpBehaviour>();
            list.Add(dest);
        }
    }
}