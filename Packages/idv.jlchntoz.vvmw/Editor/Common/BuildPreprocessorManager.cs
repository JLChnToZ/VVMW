using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

using UnityObject = UnityEngine.Object;

namespace JLChnToZ.VRC.VVMW.Editors {
    [InitializeOnLoad]
    public class BuildPreprocessorManager : IProcessSceneWithReport {
        static readonly HashSet<Type> preprocessorTypes;
        List<IPrioritizedPreProcessor> preprocessors;
        public int callbackOrder => -100;

        static BuildPreprocessorManager() {
            preprocessorTypes = new HashSet<Type>();
            var appDomain = AppDomain.CurrentDomain;
            foreach (var assembly in appDomain.GetAssemblies())
                LoadTypeFromAssembly(assembly);
            appDomain.AssemblyLoad += OnAssemblyLoad;
        }

        static void OnAssemblyLoad(object sender, AssemblyLoadEventArgs args) =>
            LoadTypeFromAssembly(args.LoadedAssembly);

        static void LoadTypeFromAssembly(Assembly assembly) {
            foreach (var type in assembly.GetTypes()) {
                if (type.IsInterface || type.IsAbstract)
                    continue;
                if (typeof(IPreprocessor).IsAssignableFrom(type))
                    preprocessorTypes.Add(type);
            }
        }

        public void OnProcessScene(Scene scene, BuildReport report) {
            StartProcess(scene);
        }

        void StartProcess(Scene scene) {
            InitInstances();
            var preprocessors = new List<IPrioritizedPreProcessor>(this.preprocessors);
            foreach (var mb in scene.IterateAllComponents<MonoBehaviour>())
                if (mb is IPrioritizedPreProcessor processor)
                    preprocessors.Add(processor);
            preprocessors.Sort(SortCallbackOrder);
            foreach (var preprocessor in preprocessors)
                try {
                    if (preprocessor is UnityObject unityObj && !unityObj)
                        continue;
                    if (preprocessor is IPreprocessor processor)
                        processor.OnPreprocess(scene);
                    else if (preprocessor is ISelfPreProcess selfProcessor)
                        selfProcessor.PreProcess();
                } catch (Exception e) {
                    Debug.LogError($"Error while processing scene with {preprocessor.GetType().Name}: {e.Message}");
                }
        }

        void InitInstances() {
            if (preprocessors != null) return;
            preprocessors = new List<IPrioritizedPreProcessor>();
            foreach (var type in preprocessorTypes)
                if (Activator.CreateInstance(type) is IPrioritizedPreProcessor preprocessor)
                    preprocessors.Add(preprocessor);
        }

        static int SortCallbackOrder(IPrioritizedPreProcessor a, IPrioritizedPreProcessor b) =>
            a.Priority.CompareTo(b.Priority);
    }

    public interface IPreprocessor : IPrioritizedPreProcessor {
        void OnPreprocess(Scene scene);
    }
}