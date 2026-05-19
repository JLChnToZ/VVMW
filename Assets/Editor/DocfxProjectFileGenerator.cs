using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW.Editor {
    public static class DocfxProjectFileGenerator {
        static readonly string[] RequiredProjectFiles = {
            "JLChnToZ.VVMW.csproj",
            "JLChnToZ.VVMW.Editor.csproj",
        };

        public static void Generate() {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            if (!TrySynchronizeProjectFiles())
                throw new BuildFailedException("Unable to trigger Unity project file generation for DocFX.");

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            string projectRoot = Directory.GetCurrentDirectory();
            string[] missingFiles = RequiredProjectFiles
                .Where(file => !File.Exists(Path.Combine(projectRoot, file)))
                .ToArray();
            if (missingFiles.Length > 0)
                throw new BuildFailedException($"Unity did not regenerate the expected project files: {string.Join(", ", missingFiles)}");

            Debug.Log($"Generated Unity project files for DocFX: {string.Join(", ", RequiredProjectFiles)}");
        }

        static bool TrySynchronizeProjectFiles() {
            return TryInvokeCodeEditorSync() || TryInvokeLegacySyncVs();
        }

        static bool TryInvokeCodeEditorSync() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type codeEditorType = assembly.GetType("Unity.CodeEditor.CodeEditor");
                if (codeEditorType == null) continue;

                PropertyInfo currentEditorProperty = codeEditorType.GetProperty("CurrentEditor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object editor = currentEditorProperty?.GetValue(null);
                if (editor == null) continue;

                if (TryInvokeInstanceMethod(editor, "SyncAll")) return true;
                if (TryInvokeInstanceMethod(editor, "SyncIfNeeded")) return true;
            }
            return false;
        }

        static bool TryInvokeLegacySyncVs() {
            string[] candidateTypes = {
                "UnityEditor.SyncVS",
                "UnityEditor.VisualStudioIntegration.SyncVS",
            };

            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                foreach (string candidateType in candidateTypes) {
                    Type syncType = assembly.GetType(candidateType);
                    if (syncType == null) continue;

                    if (TryInvokeStaticMethod(syncType, "SyncSolution")) return true;
                    if (TryInvokeStaticMethod(syncType, "SynchronizeProject")) return true;
                }
            }
            return false;
        }

        static bool TryInvokeInstanceMethod(object instance, string methodName) {
            MethodInfo method = instance.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (method == null) return false;

            try {
                method.Invoke(instance, null);
                return true;
            } catch (TargetInvocationException exception) {
                Debug.LogWarning($"Failed to invoke {instance.GetType().FullName}.{methodName}: {exception.InnerException?.Message ?? exception.Message}");
                return false;
            }
        }

        static bool TryInvokeStaticMethod(Type type, string methodName) {
            MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, null, Type.EmptyTypes, null);
            if (method == null) return false;

            try {
                method.Invoke(null, null);
                return true;
            } catch (TargetInvocationException exception) {
                Debug.LogWarning($"Failed to invoke {type.FullName}.{methodName}: {exception.InnerException?.Message ?? exception.Message}");
                return false;
            }
        }
    }
}