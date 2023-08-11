using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using VRC.Udon;
using UdonSharp;
using UdonSharpEditor;

using static UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.I18N.Editors {
    [CustomEditor(typeof(LanguageManager))]
    public class LanguageManagerEditor : Editor {
        static GUIContent textContent;
        SerializedProperty languagePackProperty;
        bool showContent;

        void OnEnable() {
            if (textContent == null) textContent = new GUIContent();
            languagePackProperty = serializedObject.FindProperty("languagePack");
        }
        
        public override void OnInspectorGUI() {
            if (UdonSharpGUI.DrawDefaultUdonSharpBehaviourHeader(target)) return;
            serializedObject.Update();
            var languagePack = languagePackProperty.objectReferenceValue as TextAsset;
            using (new EditorGUILayout.HorizontalScope()) {
                using (new EditorGUI.DisabledScope(Application.isPlaying))
                    EditorGUILayout.PropertyField(languagePackProperty);
                using (new EditorGUI.DisabledScope(languagePack == null))
                    if (GUILayout.Button("Edit File", EditorStyles.miniButton, GUILayout.ExpandWidth(false)))
                        AssetDatabase.OpenAsset(languagePack);
            }
            serializedObject.ApplyModifiedProperties();
            if (languagePack == null) {
                EditorGUILayout.HelpBox("No language pack is assigned.", MessageType.Error);
                showContent = false;
            } else {
                showContent = EditorGUILayout.Foldout(showContent, "Content (For preview only)");
                if (showContent) {
                    var languagePackData = languagePack.text;
                    textContent.text = languagePackData;
                    var textAreaStyle = EditorStyles.textArea;
                    var height = textAreaStyle.CalcHeight(textContent, EditorGUIUtility.currentViewWidth);
                    EditorGUILayout.SelectableLabel(languagePackData, textAreaStyle, GUILayout.Height(height));
                }
            }
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox("Do not add or reference other components or children in this game object, as it will be manupulated on build and may cause unexpected behaviour.", MessageType.Info);
        }
    }

    // Resolve and group all language managers into one game object while building
    public class LanguageManagerUnifier : IProcessSceneWithReport {
        static Action<UdonSharpBehaviour> RunBehaviourSetup;
        Dictionary<TextAsset, LanguageManager> unifiedLanguageManagers = new Dictionary<TextAsset, LanguageManager>();
        Dictionary<TextAsset, UdonBehaviour> unifiedLanguageManagerUdons = new Dictionary<TextAsset, UdonBehaviour>();
        GameObject unifiedLanguageManagerContainer;

        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report) {
            var roots = scene.GetRootGameObjects();
            var languagePackMap = new Dictionary<LanguageManager, TextAsset>();
            var backingUdonBehaviours = new Dictionary<UdonBehaviour, LanguageManager>();
            unifiedLanguageManagers.Clear();
            unifiedLanguageManagerUdons.Clear();
            unifiedLanguageManagerContainer = null;
            // Gather all language managers on scene
            foreach (var languageManager in roots.SelectMany(x => x.GetComponentsInChildren<LanguageManager>(true)))
                using (var so = new SerializedObject(languageManager)) {
                    var languagePack = so.FindProperty("languagePack");
                    if (languagePack.objectReferenceValue is TextAsset textAsset) {
                        languagePackMap[languageManager] = textAsset;
                        // Make the first language manager the unified language manager
                        if (unifiedLanguageManagerContainer == null) {
                            unifiedLanguageManagerContainer = languageManager.gameObject;
                            unifiedLanguageManagers[textAsset] = languageManager;
                        }
                    }
                    backingUdonBehaviours[UdonSharpEditorUtility.GetBackingUdonBehaviour(languageManager)] = languageManager;
                }
            if (languagePackMap.Count == 0) return; // No language manager found
            // Find and remap references to language managers
            foreach (var ub in roots.SelectMany(x => x.GetComponentsInChildren<UdonBehaviour>(true))) {
                if (backingUdonBehaviours.ContainsKey(ub)) continue;
                if (UdonSharpEditorUtility.IsUdonSharpBehaviour(ub)) {
                    var usharpBehaviour = UdonSharpEditorUtility.GetProxyBehaviour(ub);
                    bool hasModified = false;
                    using (var so = new SerializedObject(usharpBehaviour)) {
                        var iterator = so.GetIterator();
                        while (iterator.NextVisible(true)) {
                            if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                            if (iterator.objectReferenceValue is UdonBehaviour udon) {
                                if (!backingUdonBehaviours.TryGetValue(udon, out var languageManager))
                                    continue;
                                if (!languagePackMap.TryGetValue(languageManager, out var languagePack))
                                    continue;
                                GetUnifiedLanguageManager(languagePack, out var _, out udon);
                                if (iterator.objectReferenceValue == udon)
                                    continue;
                                iterator.objectReferenceValue = udon;
                                hasModified = true;
                            } else {
                                if (iterator.objectReferenceValue is LanguageManager languageManager && languageManager != null) {
                                    // We find a language manager reference, further logic appears after the if-else block
                                } else if (iterator.objectReferenceValue == null) {
                                    // If the reference is null, we first firgure out what is the field type of the property.
                                    var field = usharpBehaviour.GetType().GetField(iterator.name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.FlattenHierarchy);
                                    if (field == null || !typeof(LanguageManager).IsAssignableFrom(field.FieldType))
                                        continue;
                                    // If we ensure the field type is LanguageManager, we try to find the closest LanguageManager in the hierarchy.
                                    languageManager = null;
                                    for (Transform transform = ub.transform, lastTransform = null; transform != null; transform = transform.parent) {
                                        if (transform.TryGetComponent(out languageManager)) break;
                                        foreach (Transform child in transform) {
                                            if (lastTransform == child) continue;
                                            languageManager = transform.GetComponentInChildren<LanguageManager>(true);
                                            if (languageManager != null) break;
                                        }
                                        if (languageManager != null) break;
                                        lastTransform = transform;
                                    }
                                    if (languageManager == null) {
                                        foreach (var root in roots) {
                                            languageManager = root.GetComponentInChildren<LanguageManager>(true);
                                            if (languageManager != null) break;
                                        }
                                        if (languageManager == null) {
                                            Debug.LogError($"Cannot find LanguageManager for field {field.Name} in {usharpBehaviour.name}. This should not be happened.", usharpBehaviour);
                                            continue;
                                        }
                                    }
                                } else
                                    continue; // Anything else, skip
                                if (!languagePackMap.TryGetValue(languageManager, out var languagePack))
                                    continue;
                                GetUnifiedLanguageManager(languagePack, out languageManager, out var _);
                                if (iterator.objectReferenceValue == languageManager)
                                    continue;
                                iterator.objectReferenceValue = languageManager;
                                hasModified = true;
                            }
                        }
                        if (hasModified) so.ApplyModifiedPropertiesWithoutUndo();
                    }
                    if (hasModified) UdonSharpEditorUtility.CopyProxyToUdon(usharpBehaviour);
                } else {
                    var programSource = ub.programSource;
                    if (programSource == null) continue;
                    var serializedProgramAsset = programSource.SerializedProgramAsset;
                    if (serializedProgramAsset == null) continue;
                    var program = serializedProgramAsset.RetrieveProgram();
                    if (program == null) continue;
                    var symbolTable = program.SymbolTable;
                    if (symbolTable == null) continue;
                    foreach (var symbolName in symbolTable.GetSymbols()) {
                        if (!ub.TryGetProgramVariable(symbolName, out var variable)) continue;
                        if (!(variable is UdonBehaviour udon)) continue;
                        if (!backingUdonBehaviours.TryGetValue(udon, out var languageManager))
                            continue;
                        if (!languagePackMap.TryGetValue(languageManager, out var languagePack))
                            continue;
                        GetUnifiedLanguageManager(languagePack, out var unifiedLanguageManager, out var _);
                        ub.SetProgramVariable(symbolName, unifiedLanguageManager);
                    }
                }
            }
            // Clean up original (and duplicated) language managers (and their backing Udon behaviours)
            var unifiedLanguageManagerSet = new HashSet<LanguageManager>(unifiedLanguageManagers.Values);
            foreach (var languageManager in languagePackMap.Keys) {
                if (unifiedLanguageManagerSet.Contains(languageManager)) continue;
                var udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(languageManager);
                if (udon != null) DestroyImmediate(udon);
                DestroyImmediate(languageManager);
            }
            // Move unified language manager to the top of the scene
            if (unifiedLanguageManagerContainer != null) {
                var transform = unifiedLanguageManagerContainer.transform;
                if (transform.childCount > 0) {
                    var parent = transform.parent;
                    foreach (Transform child in transform) child.SetParent(parent, true);
                }
                transform.parent = null;
                transform.SetAsFirstSibling();
            }
        }

        // Get or create unified language manager
        void GetUnifiedLanguageManager(TextAsset languagePack, out LanguageManager languageManager, out UdonBehaviour udon) {
            if (unifiedLanguageManagers.TryGetValue(languagePack, out languageManager)) {
                if (unifiedLanguageManagerUdons.TryGetValue(languagePack, out udon)) return;
                unifiedLanguageManagerUdons[languagePack] = udon = UdonSharpEditorUtility.GetBackingUdonBehaviour(languageManager);
                return;
            }
            languageManager = unifiedLanguageManagerContainer.AddComponent<LanguageManager>();
            using (var so = new SerializedObject(languageManager)) {
                so.FindProperty("languagePack").objectReferenceValue = languagePack;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            // UdonSharpEditorUtility.RunBehaviourSetup is internal, so we have to use reflection to call it
            if (RunBehaviourSetup == null) {
                var method = typeof(UdonSharpEditorUtility).GetMethod(
                    "RunBehaviourSetup",
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(UdonSharpBehaviour) },
                    null
                );
                RunBehaviourSetup = (Action<UdonSharpBehaviour>)Delegate.CreateDelegate(typeof(Action<UdonSharpBehaviour>), method);
            }
            RunBehaviourSetup(languageManager);
            unifiedLanguageManagers[languagePack] = languageManager;
            unifiedLanguageManagerUdons[languagePack] = udon = UdonSharpEditorUtility.CreateBehaviourForProxy(languageManager);
        }
    }
}