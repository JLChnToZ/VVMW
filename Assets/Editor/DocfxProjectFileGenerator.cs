using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace JLChnToZ.VRC.VVMW.Editor {
    public static class DocfxProjectFileGenerator {
        const string ProjectGenerationPrefsKey = "unity_project_generation_flag";
        const int RequiredProjectGenerationFlags = 3;

        static readonly string[] RequiredProjectFiles = {
            "JLChnToZ.VVMW.csproj",
            "JLChnToZ.VVMW.Editor.csproj",
        };

        public static void Generate() {
            int originalFlags = EditorPrefs.GetInt(ProjectGenerationPrefsKey, 0);
            try {
                EditorPrefs.SetInt(ProjectGenerationPrefsKey, originalFlags | RequiredProjectGenerationFlags);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!TrySynchronizeProjectFiles())
                    throw BuildFailedDiagnostics("Unable to trigger Unity project file generation for DocFX.");

                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                if (!WaitForRequiredProjectFiles())
                    throw BuildFailedDiagnostics("Unity did not regenerate the expected project files.");

                Debug.Log($"Generated Unity project files for DocFX: {string.Join(", ", RequiredProjectFiles)}");
            } finally {
                EditorPrefs.SetInt(ProjectGenerationPrefsKey, originalFlags);
            }
        }

        static bool TrySynchronizeProjectFiles() {
            return TryInvokeDirectProjectGenerators()
                || TryInvokeCodeEditorSync()
                || TryInvokeLegacySyncVs();
        }

        static bool TryInvokeDirectProjectGenerators() {
            return TryInvokeDirectProjectGenerator("Packages.Rider.Editor.ProjectGeneration.ProjectGeneration", true)
                || TryInvokeDirectProjectGenerator("VSCodeEditor.ProjectGeneration", false)
                || TryInvokeDirectProjectGenerator("Microsoft.Unity.VisualStudio.Editor.ProjectGeneration", true);
        }

        static bool TryInvokeDirectProjectGenerator(string typeName, bool allowParameterlessConstructor) {
            Type generatorType = AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(type => type != null);
            if (generatorType == null) return false;

            object generator = CreateGeneratorInstance(generatorType, allowParameterlessConstructor);
            if (generator == null) return false;

            EnsureGeneratorFlags(generator);

            if (TryInvokeInstanceMethod(generator, "GenerateAndWriteSolutionAndProjects") && WaitForRequiredProjectFiles()) return true;
            if (TryInvokeInstanceMethod(generator, "Sync") && WaitForRequiredProjectFiles()) return true;

            return false;
        }

        static object CreateGeneratorInstance(Type generatorType, bool allowParameterlessConstructor) {
            try {
                if (allowParameterlessConstructor) {
                    ConstructorInfo constructor = generatorType.GetConstructor(
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null,
                        Type.EmptyTypes,
                        null
                    );
                    if (constructor != null)
                        return constructor.Invoke(null);
                }

                ConstructorInfo pathConstructor = generatorType.GetConstructor(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                    null,
                    new[] { typeof(string) },
                    null
                );
                if (pathConstructor != null)
                    return pathConstructor.Invoke(new object[] { Directory.GetCurrentDirectory() });
            } catch (Exception exception) {
                Debug.LogWarning($"Failed to create {generatorType.FullName}: {exception.Message}");
            }

            return null;
        }

        static void EnsureGeneratorFlags(object generator) {
            object provider = GetPropertyValue(generator, "AssemblyNameProvider");
            if (provider == null) return;

            PropertyInfo flagProperty = provider.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(property => property.Name == "ProjectGenerationFlag");
            if (flagProperty != null) {
                MethodInfo getter = flagProperty.GetGetMethod(true);
                MethodInfo setter = flagProperty.GetSetMethod(true);
                if (getter != null && setter != null) {
                    object currentValue = getter.Invoke(provider, null);
                    int flagValue = Convert.ToInt32(currentValue);
                    if ((flagValue & RequiredProjectGenerationFlags) != RequiredProjectGenerationFlags) {
                        object updatedValue = Enum.ToObject(flagProperty.PropertyType, flagValue | RequiredProjectGenerationFlags);
                        setter.Invoke(provider, new[] { updatedValue });
                    }
                }
            }

            TryInvokeInstanceMethod(provider, "ResetPackageInfoCache");
        }

        static bool TryInvokeCodeEditorSync() {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                Type codeEditorType = assembly.GetType("Unity.CodeEditor.CodeEditor");
                if (codeEditorType == null) continue;

                PropertyInfo currentEditorProperty = codeEditorType.GetProperty("CurrentEditor", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                object editor = currentEditorProperty?.GetValue(null);
                if (editor == null) continue;

                if (TryInvokeInstanceMethod(editor, "SyncAll") && WaitForRequiredProjectFiles()) return true;
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

                    if (TryInvokeStaticMethod(syncType, "SyncSolution") && WaitForRequiredProjectFiles()) return true;
                    if (TryInvokeStaticMethod(syncType, "SynchronizeProject") && WaitForRequiredProjectFiles()) return true;
                }
            }
            return false;
        }

        static bool WaitForRequiredProjectFiles() {
            string projectRoot = Directory.GetCurrentDirectory();
            int delayMilliseconds = 0;

            for (int attempt = 0; attempt < 5; attempt++) {
                if (RequiredProjectFiles.All(file => File.Exists(Path.Combine(projectRoot, file))))
                    return true;

                if (attempt < 4) {
                    delayMilliseconds = delayMilliseconds == 0 ? 200 : delayMilliseconds * 2;
                    Thread.Sleep(delayMilliseconds);
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                }
            }

            return false;
        }

        static BuildFailedException BuildFailedDiagnostics(string message) {
            string projectRoot = Directory.GetCurrentDirectory();
            string[] missingFiles = RequiredProjectFiles
                .Where(file => !File.Exists(Path.Combine(projectRoot, file)))
                .ToArray();
            string[] foundProjects = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly)
                .Select(Path.GetFileName)
                .OrderBy(name => name)
                .ToArray();

            string foundText = foundProjects.Length > 0
                ? $" Found root .csproj files: {string.Join(", ", foundProjects)}."
                : " No root .csproj files were found.";

            return new BuildFailedException($"{message} Missing: {string.Join(", ", missingFiles)}.{foundText}");
        }

        static object GetPropertyValue(object instance, string propertyName) {
            PropertyInfo property = instance.GetType().GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .FirstOrDefault(candidate => candidate.Name == propertyName || candidate.Name.EndsWith("." + propertyName, StringComparison.Ordinal));
            return property?.GetGetMethod(true)?.Invoke(instance, null);
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