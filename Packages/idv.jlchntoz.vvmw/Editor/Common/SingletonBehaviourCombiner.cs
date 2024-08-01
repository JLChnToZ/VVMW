using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine.SceneManagement;
using UnityEditor;
using VRC.Udon;
using UdonSharp;
using UdonSharpEditor;
using JLChnToZ.VRC.VVMW.Editors;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    public class SingletonBehviourCombiner : IPreprocessor {
        readonly Dictionary<Type, (MethodInfo method, HashSet<UdonSharpBehaviour> insts)> insts = new Dictionary<Type, (MethodInfo, HashSet<UdonSharpBehaviour>)>();
        readonly HashSet<UdonSharpBehaviour> firsts = new HashSet<UdonSharpBehaviour>();
        readonly Dictionary<UdonSharpBehaviour, UdonSharpBehaviour> masterMap = new Dictionary<UdonSharpBehaviour, UdonSharpBehaviour>();
        readonly Dictionary<(Type, string), UdonSharpBehaviour> fieldMap = new Dictionary<(Type, string), UdonSharpBehaviour>();

        public int CallbackOrder => 0;
        
        public void OnPreprocess(Scene scene) {
            try {
                GatherTypes(scene);
                InvokePreMerge();
                RerouteTypes(scene);
                RemoveDuplicates();
            } finally {
                insts.Clear();
                firsts.Clear();
                masterMap.Clear();
                fieldMap.Clear();
            }
        }

        void GatherTypes(Scene scene) {
            var singletonType = typeof(ISingleton<>);
            foreach (var usb in scene.IterateAllComponents<UdonSharpBehaviour>()) {
                var type = usb.GetType();
                if (insts.TryGetValue(type, out var value)) {
                    if (value.method == null) continue;
                    value.insts.Add(usb);
                    foreach (var instance in value.insts) {
                        masterMap[usb] = instance;
                        break;
                    }
                    continue;
                }
                foreach (var interfaceType in type.GetInterfaces()) {
                    if (!interfaceType.IsGenericType ||
                        interfaceType.GetGenericTypeDefinition() != singletonType) continue;
                    var resolvedType = interfaceType.GetGenericArguments()[0];
                    if (resolvedType.IsAssignableFrom(type)) {
                        value = (interfaceType.GetMethod("Merge"), new HashSet<UdonSharpBehaviour>());
                        value.insts.Add(usb);
                        break;
                    }
                }
                insts[type] = value;
            }
        }

        void InvokePreMerge() {
            foreach (var kv in insts) {
                var (method, sameTypes) = kv.Value;
                if (method == null || sameTypes.Count < 1) continue;
                var array = new UdonSharpBehaviour[sameTypes.Count];
                sameTypes.CopyTo(array);
                var typedArray = Array.CreateInstance(kv.Key, array.Length);
                Array.Copy(array, typedArray, array.Length);
                method.Invoke(array[0], new [] { typedArray });
                firsts.Add(array[0]);
            }
        }

        void RerouteTypes(Scene scene) {
            foreach (var ub in scene.IterateAllComponents<UdonBehaviour>())
                if (RerouteUdonSharpBehaviour(ub) || RerouteUdonBehaviour(ub))
                    continue;
        }

        bool RerouteUdonSharpBehaviour(UdonBehaviour ub) {
            var usb = UdonSharpEditorUtility.GetProxyBehaviour(ub);
            if (usb == null) return false;
            bool hasModified = false;
            using (var so = new SerializedObject(usb)) {
                var iterator = so.GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference)
                        switch (MapCacheField(iterator, out var cacheKey)) {
                            case MatchState.NonCached:
                                if (RerouteFilledField(iterator, cacheKey) ||
                                    RerouteEmptyField(iterator, cacheKey)) {
                                    hasModified = true;
                                }
                                break;
                            case MatchState.Matched:
                                hasModified = true;
                                break;
                        }
                if (hasModified) so.ApplyModifiedPropertiesWithoutUndo();
            }
            if (hasModified) UdonSharpEditorUtility.CopyProxyToUdon(usb);
            return true;
        }

        MatchState MapCacheField(SerializedProperty iterator, out (Type type, string name) key) {
            key = (iterator.serializedObject.targetObject.GetType(), iterator.propertyPath);
            if (!fieldMap.TryGetValue(key, out var usb)) return MatchState.NonCached;
            if (usb == null) return MatchState.Mismatched;
            iterator.objectReferenceValue = usb;
            return MatchState.Matched;
        }

        bool RerouteFilledField(SerializedProperty iterator, (Type type, string name) key) {
            var oldValue = iterator.objectReferenceValue;
            if (!(oldValue is UdonSharpBehaviour usb)) {
                if (!(oldValue is UdonBehaviour ub)) return false; 
                usb = UdonSharpEditorUtility.GetProxyBehaviour(ub);
            }
            if (usb != null && masterMap.TryGetValue(usb, out usb)) {
                fieldMap[key] = usb;
                iterator.objectReferenceValue = usb;
                return true;
            }
            fieldMap[key] = null;
            return false;
        }
        
        bool RerouteEmptyField(SerializedProperty iterator, (Type type, string name) key) {
            var field = Utils.GetFieldInfoFromProperty(iterator, out var _);
            if (field != null)
                if (insts.TryGetValue(field.FieldType, out var value) && value.method != null)
                    foreach (var instance in value.insts)
                        if (firsts.Contains(instance)) {
                            fieldMap[key] = instance;
                            iterator.objectReferenceValue = instance;
                            return true;
                        }
            fieldMap[key] = null;
            return false;
        }

        bool RerouteUdonBehaviour(UdonBehaviour ub) {
            var programSource = ub.programSource;
            if (programSource == null) return false;
            var serializedProgramAsset = programSource.SerializedProgramAsset;
            if (serializedProgramAsset == null) return false;
            var program = serializedProgramAsset.RetrieveProgram();
            if (program == null) return false;
            var symbolTable = program.SymbolTable;
            if (symbolTable == null) return false;
            foreach (var symbolName in symbolTable.GetSymbols()) {
                if (!ub.TryGetProgramVariable(symbolName, out var variable)) continue;
                if (!(variable is UdonBehaviour otherUb)) continue;
                var usb = UdonSharpEditorUtility.GetProxyBehaviour(otherUb);
                if (!masterMap.TryGetValue(usb, out usb)) continue;
                var baseUb = UdonSharpEditorUtility.GetBackingUdonBehaviour(usb);
                ub.SetProgramVariable(symbolName, baseUb);
            }
            return true;
        }

        void RemoveDuplicates() {
            var list = new List<UdonSharpBehaviour>();
            foreach (var (method, sameTypes) in insts.Values)
                if (method != null && sameTypes.Count > 0)
                    try {
                        list.AddRange(sameTypes);
                        foreach (var instance in list) {
                            if (firsts.Contains(instance)) {
                                instance.transform.SetParent(null, false);
                                continue;
                            }
                            DestroyImmediate(instance);
                        }
                    } finally {
                        list.Clear();
                    }
        }

        enum MatchState : byte {
            NonCached,
            Matched,
            Mismatched
        }
    }
}