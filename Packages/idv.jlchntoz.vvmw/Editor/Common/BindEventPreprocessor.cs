/*
The MIT License (MIT)

Copyright (c) 2023 Jeremy Lam aka. Vistanz

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Reflection;

using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

using UnityEditor;
using UnityEditor.Events;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Callbacks;

using VRC.Udon.Editor;
using UdonSharp;
using UdonSharpEditor;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    internal sealed class BindEventPreprocessor : IProcessSceneWithReport {
        static readonly Regex regexCompositeFormat = new Regex(@"\{(\d+)[,:]?[^\}]*\}", RegexOptions.Compiled);
        static readonly Dictionary<Type, MonoScript> scriptMap = new Dictionary<Type, MonoScript>();
        static readonly Dictionary<string, string> typeNameMapping = new Dictionary<string, string>();
        readonly Dictionary<Type, FieldInfo[]> filteredFields = new Dictionary<Type, FieldInfo[]>();
        static bool hasTypeNameMappingInit;

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) {
            foreach (var usharp in scene.IterateAllComponents<UdonSharpBehaviour>()) {
                var type = usharp.GetType();
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(usharp);
                if (udon == null) {
                    Debug.LogError($"[BindEventPreprocessor] `{usharp.name}` is not correctly configured.", usharp);
                    continue;
                }
                UnityAction<string> call = udon.SendCustomEvent;
                ProcessEntry(type, udon, call);
                var fieldInfos = GetFields(type);
                foreach (var field in fieldInfos) {
                    var targetObj = field.GetValue(usharp);
                    if (targetObj is Array array)
                        for (int i = 0, length = array.GetLength(0); i < length; i++)
                            ProcessEntry(array.GetValue(i) as UnityObject, field, call, i);
                    else if (targetObj is UnityObject unityObject)
                        ProcessEntry(unityObject, field, call, 0);
                }
            }
        }

        FieldInfo[] GetFields(Type type) {
            if (!filteredFields.TryGetValue(type, out var fieldInfos)) {
                fieldInfos = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(field => field.IsDefined(typeof(BindEventAttribute), true)).ToArray();
                filteredFields[type] = fieldInfos;
            }
            return fieldInfos;
        }

        static void ProcessEntry(Type type, Component component, UnityAction<string> call) {
            foreach (var attribute in type.GetCustomAttributes<BindEventAttribute>(true)) {
                var srcType = attribute.SourceType;
                if (srcType == null) continue;
                var targetObjs = component.GetComponents(srcType);
                for (int i = 0; i < targetObjs.Length; i++)
                    BindSingleEvent(targetObjs[i], srcType, attribute, call, i);
            }
        }

        static void ProcessEntry(UnityObject targetObj, MemberInfo member, UnityAction<string> call, int index) {
            if (targetObj == null) return;
            var srcType = targetObj.GetType();
            foreach (var attribute in member.GetCustomAttributes<BindEventAttribute>(true))
                BindSingleEvent(targetObj, srcType, attribute, call, index);
        }

        static bool TryGetValue(object source, Type srcType, string fieldName, out object result) {
            var otherProp = srcType.GetProperty(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (otherProp != null) {
                result = otherProp.GetValue(source);
                return true;
            }
            var otherField = srcType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (otherField != null) {
                result = otherField.GetValue(source);
                return true;
            }
            result = null;
            return false;
        }

        static void BindSingleEvent(UnityObject targetObj, Type srcType, BindEventAttribute attribute, UnityAction<string> call, int index) {
            if (TryGetValue(targetObj, srcType, attribute.Source, out var otherObj) && otherObj is UnityEventBase callback) {
                var targetEventName = string.Format(attribute.Destination, index, targetObj.name);
                if (!hasTypeNameMappingInit) {
                    foreach (var def in UdonEditorManager.Instance.GetNodeDefinitions()) {
                        if (!def.fullName.StartsWith("Event_")) continue;
                        typeNameMapping[def.fullName.Substring(6)] = $"_{char.ToLower(def.fullName[6])}{def.fullName.Substring(7)}";
                    }
                    hasTypeNameMappingInit = true;
                }
                if (typeNameMapping.TryGetValue(targetEventName, out var mappedEventName))
                    targetEventName = mappedEventName;
                UnityEventTools.AddStringPersistentListener(callback, call, targetEventName);
            }
        }

        [DidReloadScripts]
        static void Validate() {
            GatherMonoScripts();
            foreach (var type in AppDomain.CurrentDomain.GetAssemblies().SelectMany(assembly => assembly.GetTypes())) {
                foreach (var attribute in type.GetCustomAttributes<BindEventAttribute>(true)) ValidateType(type, attribute);
                foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                foreach (var attribute in field.GetCustomAttributes<BindEventAttribute>(true)) ValidateType(type, attribute, field);
            }
        }

        static void GatherMonoScripts() {
            scriptMap.Clear();
            foreach (var script in AssetDatabase.FindAssets("t:MonoScript").Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<MonoScript>)) {
                var type = script.GetClass();
                if (type != null) scriptMap[type] = script;
            }
        }

        static void ValidateType(Type type, BindEventAttribute attribute, FieldInfo field = null) {
            var messagePrefix = field != null ? $"BindEventAttribute on {type.FullName}.{field.Name}: " : $"BindEventAttribute on {type.FullName}: ";
            scriptMap.TryGetValue(type, out var currentScript);
            if (!type.IsSubclassOf(typeof(UdonSharpBehaviour))) {
                Debug.LogError($"{messagePrefix} Type is not derived from UdonSharpBehaviour, which will not have any effect.", currentScript);
                return;
            }
            var srcName = attribute.Source;
            if (string.IsNullOrEmpty(srcName)) {
                Debug.LogError($"{messagePrefix} Source is empty.", currentScript);
                return;
            }
            var destName = attribute.Destination;
            if (string.IsNullOrEmpty(destName)) {
                Debug.LogError($"{messagePrefix} Destination is empty.", currentScript);
                return;
            }
            Type srcType;
            if (field != null) {
                srcType = field.FieldType;
                if (attribute.SourceType != null) Debug.LogWarning($"{messagePrefix} SourceType is ignored because the attribute is on a field.", currentScript);
            } else {
                srcType = attribute.SourceType;
                if (srcType == null) {
                    Debug.LogError($"{messagePrefix} SourceType is empty.", currentScript);
                    return;
                }
            }
            if (srcType.IsArray) srcType = srcType.GetElementType();
            var srcProperty = srcType.GetProperty(srcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
            if (srcProperty == null) {
                var srcField = srcType.GetField(srcName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.FlattenHierarchy);
                if (srcField == null) {
                    Debug.LogError($"{messagePrefix} Source {srcType.FullName}.{srcName} does not exists.", currentScript);
                    return;
                }
                srcType = srcField.FieldType;
            } else
                srcType = srcProperty.PropertyType;
            if (!srcType.IsSubclassOf(typeof(UnityEventBase))) {
                Debug.LogError($"{messagePrefix} Source {srcType.FullName}.{srcName} is not an Unity event. Only Unity events can be bound.", currentScript);
                return;
            }
            var regex = regexCompositeFormat.IsMatch(destName) ? new Regex(regexCompositeFormat.Replace(destName, ReplaceRegex), RegexOptions.Compiled) : null;
            bool hasAnyMatch = false;
            foreach (var member in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.FlattenHierarchy))
                if (regex != null ? regex.IsMatch(member.Name) : destName == member.Name) {
                    hasAnyMatch = true;
                    if (member.GetParameters().Length > 0)
                        Debug.LogWarning($"{messagePrefix} Destination {member.Name} has parameters, which still can be called, but will cause unexpected behavior.", currentScript);
                    if (!member.IsPublic)
                        Debug.LogError($"{messagePrefix} Destination {member.Name} is not public, which cannot be called from outside.", currentScript);
                }
            if (!hasAnyMatch)
                Debug.LogError($"{messagePrefix} None of destination methods matches {destName}.", currentScript);
            else if (!destName.StartsWith("_"))
                Debug.LogWarning($"{messagePrefix} Destination {destName} does not start with underscore, which can be called over network RPC.\nIf this is intentional, you may safely ignore this warning.", currentScript);
        }

        static string ReplaceRegex(Match match) {
            switch (match.Groups[1].Value) {
                case "0": return @"(?:[1-9]\d*|0)";
                case "1": return @"\w+";
                default: return "";
            }
        }
    }
}
