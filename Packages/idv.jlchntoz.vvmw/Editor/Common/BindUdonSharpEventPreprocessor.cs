using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEditor;

using UdonSharp;
using UdonSharpEditor;

using UnityObject = UnityEngine.Object;
using VRC.Udon;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal sealed class BindUdonSharpEventPreprocessor : UdonSharpPreProcessor {
        readonly Dictionary<UdonSharpEventSender, List<UdonSharpBehaviour>> eventSenders = new Dictionary<UdonSharpEventSender, List<UdonSharpBehaviour>>();

        protected override void ProcessEntry(Type type, UdonSharpBehaviour usharp, UdonBehaviour udon) {
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
        
        public override void OnPreprocess(Scene scene) {
            base.OnPreprocess(scene);
            var remapped = new HashSet<UdonSharpBehaviour>();
            foreach (var kv in eventSenders) {
                var sender = kv.Key;
                if (sender == null) {
                    Debug.LogError("[BindUdonSharpEventPreprocessor] Event sender is null, this should not happen.", sender);
                    continue;
                }
                using (var so = new SerializedObject(sender)) {
                    var prop = so.FindProperty("targets");
                    for (int i = 0, count = prop.arraySize; i < count; i++)
                        if (prop.GetArrayElementAtIndex(i).objectReferenceValue is UdonSharpBehaviour ub && ub != null)
                            remapped.Add(ub);
                    remapped.UnionWith(kv.Value);
                    prop.arraySize = remapped.Count;
                    {
                        int i = 0;
                        foreach (var entry in remapped)
                            prop.GetArrayElementAtIndex(i++).objectReferenceValue = entry;
                    }
                    remapped.Clear();
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